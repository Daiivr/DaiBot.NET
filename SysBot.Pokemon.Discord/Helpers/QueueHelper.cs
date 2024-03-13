using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Discord.Commands.Bots;
using System.Collections.Generic;
using System;
using System.Drawing;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using SysBot.Pokemon.Helpers;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    // A dictionary to hold batch trade file paths and their deletion status
    private static Dictionary<int, List<string>> batchTradeFiles = new Dictionary<int, List<string>>();
    private static Dictionary<ulong, int> userBatchTradeMaxDetailId = new Dictionary<ulong, int>();

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, int formArgument = 0, bool isMysteryEgg = false, List<Pictocodes> lgcode = null)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync($"<a:warning:1206483664939126795> {context.User.Mention} El código de tradeo debe ser un numero entre: **00000000-99999999**!").ConfigureAwait(false);
            return;
        }

        try
        {
            if (!isBatchTrade || batchTradeNumber == 1)
            {
                const string helper = "<a:yes:1206485105674166292> Te he añadido a la __lista__! Te enviaré un __mensaje__ aquí cuando comience tu operación...";
                IUserMessage test = await trader.SendMessageAsync(helper).ConfigureAwait(false);
                if (trade is PB7 && lgcode != null)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, $"Tu código de tradeo sera: ", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await trader.SendMessageAsync($"Tu código de tradeo sera: **{code:0000 0000}**").ConfigureAwait(false);
                }
            }

            // Add to trade queue and get the result
            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, formArgument, isMysteryEgg, lgcode).ConfigureAwait(false);
            // Delete the user's join message for privacy
            if(!isBatchTrade && !context.IsPrivate)
                await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades, int formArgument = 0, bool isMysteryEgg = false, List<Pictocodes> lgcode = null)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades, isMysteryEgg, lgcode);
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored, lgcode, batchTradeNumber, totalBatchTrades, isMysteryEgg);
        var trade = new TradeEntry<T>(detail, userID, type, name);
        var strings = GameInfo.GetStrings(1);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var canAddMultiple = isBatchTrade || sig == RequestSignificance.Owner;
        var added = Info.AddToTradeQueue(trade, userID, canAddMultiple);

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            return new TradeQueueResult(false);
        }

        var position = Info.CheckPosition(userID, type);
        var botct = Info.Hub.Bots.Count;
        var etaMessage = "";
        if (position.Position > botct)
        {
            var baseEta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
            // Increment ETA by 1 minute for each batch trade
            var adjustedEta = baseEta + (batchTradeNumber - 1);
            etaMessage = $"Estimado: {adjustedEta:F1} min(s) para el tradeo {batchTradeNumber}/{totalBatchTrades}.";
        }
        else
        {
            var adjustedEta = (batchTradeNumber - 1); // Add 1 minute for each subsequent batch trade
            etaMessage = $"Estimado: {adjustedEta:F1} minuto(s) para el tradeo {batchTradeNumber}/{totalBatchTrades}.";
        }

        Dictionary<string, string> scaleEmojis = new Dictionary<string, string>
                {
                    { "XXXS", "<:minimark:1158632782013136946>" }, // Emoji for XXXS
                    { "XXXL", "<:jumbomark:1158632783380492318>" }  // Emoji for XXXL
                };
        string scale = "";

        if (pk is PA8 fin8a)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8a.Scale).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8a.Scale})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8a.Scale})";
            }
        }
        else if (pk is PB7 fin7b)
        {
            // For PB7 type, do nothing to exclude the scale from the embed
        }
        if (pk is PB8 fin8b)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8b.HeightScalar).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8b.HeightScalar})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8b.HeightScalar})";
            }
        }
        if (pk is PK8 fin8)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8.HeightScalar).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8.HeightScalar})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8.HeightScalar})";
            }
        }
        if (pk is PK9 fin9)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin9.Scale).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin9.Scale})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin9.Scale})";
            }
        }

        // Format IVs for display
        int[] ivs = pk.IVs;
        string ivsDisplay = $"{ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}";

        var moveNamesList = new List<string>();

        //Remueve el None si no encuentra un movimiento
        if (pk.Move1 != 0)
            moveNamesList.Add($"{(Move)pk.Move1}");
        if (pk.Move2 != 0)
            moveNamesList.Add($"{(Move)pk.Move2}");
        if (pk.Move3 != 0)
            moveNamesList.Add($"{(Move)pk.Move3}");
        if (pk.Move4 != 0)
            moveNamesList.Add($"{(Move)pk.Move4}");

        // Add emojis for specific moves
        var waterEmoji = "<:move_water:1135381853675716728>"; // Water emoji
        var fireEmoji = "<:move_fire:1135381665028522025>"; // Fire emoji
        var electricEmoji = "<:move_electric:1135381611748270211>"; // Electric emoji
        var bugEmoji = "<:move_bug:1135381533750984794>"; // Bug Emoji
        var darkEmoji = "<:move_dark:1135381573588496414>"; // Dark Emoji
        var ghostEmoji = "<:move_ghost:1135381691465203733>"; //Ghost emoji
        var poisonEmoji = "<:move_poison:1135381791788765255>"; //Poison emoji
        var iceEmoji = "<:move_ice:1135381764223799356>"; //Ice emoji
        var steelEmoji = "<:move_steel:1135381836823011408>"; //steel emoji
        var rockEmoji = "<:move_rock:1135381815889252432>"; //rock emoji
        var groundEmoji = "<:move_ground:1135381748360954027>"; //ground emoji
        var fairyEmoji = "<:move_fairy:1135381627053297704>"; //fairy emoji
        var grassEmoji = "<:move_grass:1135381703796469780>"; //grass emoji
        var fightingEmoji = "<:move_fighting:1135381642878398464>"; //fighting emoji
        var normalEmoji = "<:move_normal:1135381779247804447>"; //normal emoji
        var dragonEmoji = "<:move_dragon:1135381595935752375>"; //dragon emoji
        var flyingEmoji = "<:move_flying:1135381678429315262>"; //flying emoji
        var psychicEmoji = "<:move_psychic:1135381805290229770>"; //pyschic emoji


        // move list
        var waterMoves = new List<string> { "WaterGun", "HydroPump", "Surf", "BubbleBeam", "Withdraw", "Waterfall", "Clamp", "Bubble", "Crabhammer", "Octazooka", "RainDance", "Whirlpool", "Dive", "HydroCannon", "WaterSpout", "MuddyWater", "WaterSport", "WaterPulse", "Brine", "AquaRing", "AquaTail", "AquaJet", "Soak", "Scald", "WaterPledge", "RazorShell", "SteamEruption", "WaterShuriken", "OriginPulse", "HydroVortex", "HydroVortex", "SparklingAria", "OceanicOperetta", "Liquidation", "SplishySplash", "BouncyBubble", "SnipeShot", "FishiousRend", "MaxGeyser", "LifeDew", "FlipTurn", "SurgingStrikes", "WaveCrash", "JetPunch", "TripleDive", "AquaStep", "HydroSteam", "ChillingWater", "AquaCutter" };
        var fireMoves = new List<string> { "WillOWisp", "FirePunch", "Ember", "Flamethrower", "FireSpin", "FireBlast", "FlameWheel", "SacredFire", "SunnyDay", "HeatWave", "Eruption", "BlazeKick", "BlastBurn", "Overheat", "FlareBlitz", "FireFang", "LavaPlume", "MagmaStorm", "FlameBurst", "FlameCharge", "Incinerate", "Inferno", "FirePledge", "HeatCrash", "SearingShot", "BlueFlare", "FieryDance", "V-create", "FusionFlare", "MysticalFire", "InfernoOverdrive", "InfernoOverdrive", "FireLash", "BurnUp", "ShellTrap", "MindBlown", "SizzlySlide", "MaxFlare", "PyroBall", "BurningJealousy", "RagingFury", "TorchSong", "ArmorCannon", "BitterBlade", "BlazingTorque", "BurningBulwark", "TemperFlare" };
        var electricMoves = new List<string> { "ThunderPunch", "ThunderShock", "Thunderbolt", "ThunderWave", "Thunder", "ZapCannon", "Spark", "Charge", "VoltTackle", "ShockWave", "MagnetRise", "ThunderFang", "Discharge", "ChargeBeam", "ElectroBall", "VoltSwitch", "Electroweb", "WildCharge", "BoltStrike", "FusionBolt", "IonDeluge", "ParabolicCharge", "Electrify", "EerieImpulse", "MagneticFlux", "ElectricTerrain", "Nuzzle", "GigavoltHavoc", "GigavoltHavoc", "Catastropika", "StokedSparksurfer", "ZingZap", "10,000,000VoltThunderbolt", "PlasmaFists", "ZippyZap", "PikaPapow", "BuzzyBuzz", "BoltBeak", "MaxLightning", "AuraWheel", "Overdrive", "RisingVoltage", "ThunderCage", "WildboltStorm", "ElectroDrift", "DoubleShock", "ElectroShot", "Thunderclap", "SupercellSlam" };
        var bugEmojiMoves = new List<string> { "XScissor", "Uturn", "Twineedle", "PinMissile", "StringShot", "LeechLife", "SpiderWeb", "FuryCutter", "Megahorn", "TailGlow", "SilverWind", "SignalBeam", "U-turn", "X-Scissor", "BugBuzz", "BugBite", "AttackOrder", "DefendOrder", "HealOrder", "RagePowder", "QuiverDance", "StruggleBug", "Steamroller", "StickyWeb", "FellStinger", "Powder", "Infestation", "SavageSpin-Out", "SavageSpin-Out", "FirstImpression", "PollenPuff", "Lunge", "MaxFlutterby", "SkitterSmack", "SilkTrap", "Pounce", };
        var darkMoves = new List<string> { "Bite", "Thief", "FeintAttack", "Pursuit", "Crunch", "BeatUp", "Torment", "Flatter", "Memento", "Taunt", "KnockOff", "Snatch", "FakeTears", "Payback", "Assurance", "Embargo", "Fling", "Punishment", "SuckerPunch", "DarkPulse", "NightSlash", "Switcheroo", "NastyPlot", "DarkVoid", "HoneClaws", "FoulPlay", "Quash", "NightDaze", "Snarl", "PartingShot", "Topsy-Turvy", "HyperspaceFury", "BlackHoleEclipse", "BlackHoleEclipse", "DarkestLariat", "ThroatChop", "PowerTrip", "BrutalSwing", "MaliciousMoonsault", "BaddyBad", "JawLock", "MaxDarkness", "Obstruct", "FalseSurrender", "LashOut", "WickedBlow", "FieryWrath", "CeaselessEdge", "KowtowCleave", "Ruination", "Comeuppance", "WickedTorque", "TopsyTurvy" };
        var ghostMoves = new List<string> { "NightShade", "ConfuseRay", "Lick", "Nightmare", "Curse", "Spite", "DestinyBond", "ShadowBall", "Grudge", "Astonish", "ShadowPunch", "ShadowClaw", "ShadowSneak", "OminousWind", "ShadowForce", "Hex", "PhantomForce", "Trick-or-Treat", "Never-EndingNightmare", "Never-EndingNightmare", "SpiritShackle", "SinisterArrowRaid", "Soul-Stealing7-StarStrike", "ShadowBone", "SpectralThief", "MoongeistBeam", "MenacingMoonrazeMaelstrom", "MaxPhantasm", "Poltergeist", "AstralBarrage", "BitterMalice", "InfernalParade", "LastRespects", "RageFist" };
        var poisonMoves = new List<string> { "PoisonSting", "Acid", "PoisonPowder", "Toxic", "Smog", "Sludge", "PoisonGas", "AcidArmor", "SludgeBomb", "PoisonFang", "PoisonTail", "GastroAcid", "ToxicSpikes", "PoisonJab", "CrossPoison", "GunkShot", "Venoshock", "SludgeWave", "Coil", "AcidSpray", "ClearSmog", "Belch", "VenomDrench", "AcidDownpour", "AcidDownpour", "BanefulBunker", "ToxicThread", "Purify", "MaxOoze", "ShellSideArm", "CorrosiveGas", "DireClaw", "BarbBarrage", "MortalSpin", "NoxiousTorque", "MalignantChain" };
        var iceMoves = new List<string> { "FreezeDry", "IcePunch", "Mist", "IceBeam", "Blizzard", "AuroraBeam", "Haze", "PowderSnow", "IcyWind", "Hail", "IceBall", "SheerCold", "IcicleSpear", "Avalanche", "IceShard", "IceFang", "FrostBreath", "Glaciate", "FreezeShock", "IceBurn", "IcicleCrash", "Freeze-Dry", "SubzeroSlammer", "SubzeroSlammer", "IceHammer", "AuroraVeil", "FreezyFrost", "MaxHailstorm", "TripleAxel", "GlacialLance", "MountainGale", "IceSpinner", "ChillyReception", "Snowscape" };
        var steelMoves = new List<string> { "SteelWing", "IronTail", "MetalClaw", "MeteorMash", "MetalSound", "IronDefense", "DoomDesire", "GyroBall", "MetalBurst", "BulletPunch", "MirrorShot", "FlashCannon", "IronHead", "MagnetBomb", "Autotomize", "HeavySlam", "ShiftGear", "GearGrind", "King'sShield", "CorkscrewCrash", "CorkscrewCrash", "GearUp", "AnchorShot", "SmartStrike", "SunsteelStrike", "SearingSunrazeSmash", "DoubleIronBash", "MaxSteelspike", "BehemothBlade", "BehemothBash", "SteelBeam", "SteelRoller", "Shelter", "SpinOut", "MakeItRain", "GigatonHammer", "TachyonCutter", "HardPress" };
        var rockMoves = new List<string> { "RockThrow", "RockSlide", "Sandstorm", "Rollout", "AncientPower", "RockTomb", "RockBlast", "RockPolish", "PowerGem", "RockWrecker", "StoneEdge", "StealthRock", "HeadSmash", "WideGuard", "SmackDown", "DiamondStorm", "ContinentalCrush", "ContinentalCrush", "Accelerock", "SplinteredStormshards", "TarShot", "MaxRockfall", "MeteorBeam", "StoneAxe", "SaltCure", "MightyCleave" };
        var groundMoves = new List<string> { "MudSlap", "SandAttack", "Earthquake", "Fissure", "Dig", "BoneClub", "Bonemerang", "Mud-Slap", "Spikes", "BoneRush", "Magnitude", "MudSport", "SandTomb", "MudShot", "EarthPower", "MudBomb", "Bulldoze", "DrillRun", "Rototiller", "ThousandArrows", "ThousandWaves", "Land'sWrath", "PrecipiceBlades", "TectonicRage", "TectonicRage", "ShoreUp", "HighHorsepower", "StompingTantrum", "MaxQuake", "ScorchingSands", "HeadlongRush", "SandsearStorm" };
        var fairyMoves = new List<string> { "BabyDollEyes", "SweetKiss", "Charm", "Moonlight", "DisarmingVoice", "DrainingKiss", "CraftyShield", "FlowerShield", "MistyTerrain", "PlayRough", "FairyWind", "Moonblast", "FairyLock", "AromaticMist", "Geomancy", "DazzlingGleam", "Baby-DollEyes", "LightofRuin", "TwinkleTackle", "TwinkleTackle", "FloralHealing", "GuardianofAlola", "FleurCannon", "Nature'sMadness", "Let'sSnuggleForever", "SparklySwirl", "MaxStarfall", "Decorate", "SpiritBreak", "StrangeSteam", "MistyExplosion", "SpringtideStorm", "MagicalTorque", "AlluringVoice" };
        var grassMoves = new List<string> { "VineWhip", "Absorb", "MegaDrain", "LeechSeed", "RazorLeaf", "SolarBeam", "StunSpore", "SleepPowder", "PetalDance", "Spore", "CottonSpore", "GigaDrain", "Synthesis", "Ingrain", "NeedleArm", "Aromatherapy", "GrassWhistle", "BulletSeed", "FrenzyPlant", "MagicalLeaf", "LeafBlade", "WorrySeed", "SeedBomb", "EnergyBall", "LeafStorm", "PowerWhip", "GrassKnot", "WoodHammer", "SeedFlare", "GrassPledge", "HornLeech", "LeafTornado", "CottonGuard", "Forest'sCurse", "PetalBlizzard", "GrassyTerrain", "SpikyShield", "BloomDoom", "BloomDoom", "StrengthSap", "SolarBlade", "Leafage", "TropKick", "SappySeed", "MaxOvergrowth", "DrumBeating", "SnapTrap", "BranchPoke", "AppleAcid", "GravApple", "GrassyGlide", "JungleHealing", "Chloroblast", "SpicyExtract", "FlowerTrick", "Trailblaze", "MatchaGotcha", "SyrupBomb", "IvyCudgel" };
        var fightingMoves = new List<string> { "KarateChop", "DoubleKick", "JumpKick", "RollingKick", "Submission", "LowKick", "Counter", "SeismicToss", "HighJumpKick", "TripleKick", "Reversal", "MachPunch", "Detect", "DynamicPunch", "VitalThrow", "CrossChop", "RockSmash", "FocusPunch", "Superpower", "Revenge", "BrickBreak", "ArmThrust", "SkyUppercut", "BulkUp", "Wake-UpSlap", "HammerArm", "CloseCombat", "ForcePalm", "AuraSphere", "DrainPunch", "VacuumWave", "FocusBlast", "StormThrow", "LowSweep", "QuickGuard", "CircleThrow", "FinalGambit", "SacredSword", "SecretSword", "FlyingPress", "MatBlock", "Power-UpPunch", "All-OutPummeling", "All-OutPummeling", "NoRetreat", "Octolock", "MaxKnuckle", "BodyPress", "MeteorAssault", "Coaching", "ThunderousKick", "VictoryDance", "TripleArrows", "AxeKick", "CollisionCourse", "CombatTorque", "UpperHand", "PowerUpPunch" };
        var normalMoves = new List<string> { "SelfDestruct", "SoftBoiled", "LockOn", "DoubleEdge", "Pound", "DoubleSlap", "CometPunch", "MegaPunch", "PayDay", "Scratch", "ViseGrip", "Guillotine", "RazorWind", "SwordsDance", "Cut", "Whirlwind", "Bind", "Slam", "Stomp", "MegaKick", "Headbutt", "HornAttack", "FuryAttack", "HornDrill", "Tackle", "BodySlam", "Wrap", "TakeDown", "Thrash", "Double-Edge", "TailWhip", "Leer", "Growl", "Roar", "Sing", "Supersonic", "SonicBoom", "Disable", "HyperBeam", "Strength", "Growth", "QuickAttack", "Rage", "Mimic", "Screech", "DoubleTeam", "Recover", "Harden", "Minimize", "Smokescreen", "DefenseCurl", "FocusEnergy", "Bide", "Metronome", "Self-Destruct", "EggBomb", "Swift", "SkullBash", "SpikeCannon", "Constrict", "Soft-Boiled", "Glare", "Barrage", "LovelyKiss", "Transform", "DizzyPunch", "Flash", "Splash", "Explosion", "FurySwipes", "HyperFang", "Sharpen", "Conversion", "TriAttack", "SuperFang", "Slash", "Substitute", "Struggle", "Sketch", "MindReader", "Snore", "Flail", "Conversion2", "Protect", "ScaryFace", "BellyDrum", "Foresight", "PerishSong", "Lock-On", "Endure", "FalseSwipe", "Swagger", "MilkDrink", "MeanLook", "Attract", "SleepTalk", "HealBell", "Return", "Present", "Frustration", "Safeguard", "PainSplit", "BatonPass", "Encore", "RapidSpin", "SweetScent", "MorningSun", "HiddenPower", "PsychUp", "ExtremeSpeed", "FakeOut", "Uproar", "Stockpile", "SpitUp", "Swallow", "Facade", "SmellingSalts", "FollowMe", "NaturePower", "HelpingHand", "Wish", "Assist", "Recycle", "Yawn", "Endeavor", "Refresh", "SecretPower", "Camouflage", "TeeterDance", "SlackOff", "HyperVoice", "CrushClaw", "WeatherBall", "OdorSleuth", "Tickle", "Block", "Howl", "Covet", "NaturalGift", "Feint", "Acupressure", "TrumpCard", "WringOut", "LuckyChant", "MeFirst", "Copycat", "LastResort", "GigaImpact", "RockClimb", "Captivate", "Judgment", "DoubleHit", "CrushGrip", "SimpleBeam", "Entrainment", "AfterYou", "Round", "EchoedVoice", "ChipAway", "ShellSmash", "ReflectType", "Retaliate", "Bestow", "WorkUp", "TailSlap", "HeadCharge", "TechnoBlast", "RelicSong", "NobleRoar", "Boomburst", "PlayNice", "Confide", "HappyHour", "Celebrate", "HoldHands", "HoldBack", "BreakneckBlitz", "BreakneckBlitz", "Spotlight", "LaserFocus", "RevelationDance", "PulverizingPancake", "ExtremeEvoboost", "TearfulLook", "Multi-Attack", "VeeveeVolley", "MaxGuard", "StuffCheeks", "Teatime", "CourtChange", "MaxStrike", "TerrainPulse", "PowerShift", "TeraBlast", "PopulationBomb", "RevivalBlessing", "Doodle", "FilletAway", "RagingBull", "ShedTail", "TidyUp", "HyperDrill", "BloodMoon", "TeraStarstorm" };
        var dragonMoves = new List<string> { "DragonRage", "Outrage", "DragonBreath", "Twister", "DragonClaw", "DragonDance", "DragonPulse", "DragonRush", "DracoMeteor", "RoarofTime", "SpacialRend", "DragonTail", "DualChop", "DevastatingDrake", "DevastatingDrake", "CoreEnforcer", "ClangingScales", "DragonHammer", "ClangorousSoulblaze", "", "DynamaxCannon", "DragonDarts", "MaxWyrmwind", "ClangorousSoul", "BreakingSwipe", "Eternabeam", "ScaleShot", "DragonEnergy", "OrderUp", "GlaiveRush", "FickleBeam", "DragonCheer" };
        var flyingMoves = new List<string> { "Gust", "WingAttack", "Fly", "Peck", "DrillPeck", "MirrorMove", "SkyAttack", "Aeroblast", "FeatherDance", "AirCutter", "AerialAce", "Bounce", "Roost", "Pluck", "Tailwind", "AirSlash", "BraveBird", "Defog", "Chatter", "SkyDrop", "Acrobatics", "Hurricane", "OblivionWing", "DragonAscent", "SupersonicSkystrike", "SupersonicSkystrike", "BeakBlast", "FloatyFall", "MaxAirstream", "DualWingbeat", "BleakwindStorm" };
        var psychicMoves = new List<string> { "Psybeam", "Confusion", "Psychic", "Hypnosis", "Meditate", "Agility", "Teleport", "Barrier", "LightScreen", "Reflect", "Amnesia", "Kinesis", "DreamEater", "Psywave", "Rest", "MirrorCoat", "FutureSight", "Trick", "RolePlay", "MagicCoat", "SkillSwap", "Imprison", "LusterPurge", "MistBall", "CosmicPower", "Extrasensory", "CalmMind", "PsychoBoost", "Gravity", "MiracleEye", "HealingWish", "PsychoShift", "HealBlock", "PowerTrick", "PowerSwap", "GuardSwap", "HeartSwap", "PsychoCut", "ZenHeadbutt", "TrickRoom", "LunarDance", "GuardSplit", "PowerSplit", "WonderRoom", "Psyshock", "Telekinesis", "MagicRoom", "Synchronoise", "StoredPower", "AllySwitch", "HealPulse", "HeartStamp", "Psystrike", "HyperspaceHole", "ShatteredPsyche", "ShatteredPsyche", "PsychicTerrain", "SpeedSwap", "Instruct", "GenesisSupernova", "PsychicFangs", "PrismaticLaser", "PhotonGeyser", "LightThatBurnstheSky", "GlitzyGlow", "MagicPowder", "MaxMindstorm", "ExpandingForce", "FreezingGlare", "EerieSpell", "PsyshieldBash", "MysticalPower", "EsperWing", "LunarBlessing", "TakeHeart", "LuminaCrash", "Psyblade", "TwinBeam", "PsychicNoise" };

        for (int i = 0; i < moveNamesList.Count; i++)
        {
            foreach (var move in waterMoves)
            {
                var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = waterEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in fireMoves)
            {
                var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = fireEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in electricMoves)
            {
                var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = electricEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in bugEmojiMoves)
            {
                var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = bugEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in darkMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = darkEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in ghostMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = ghostEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in poisonMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = poisonEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in iceMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = iceEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in steelMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = steelEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in rockMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = rockEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in groundMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = groundEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in fightingMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = fightingEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in dragonMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = dragonEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in flyingMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = flyingEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in psychicMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = psychicEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
            foreach (var move in grassMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = grassEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in fairyMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = fairyEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }

            foreach (var move in normalMoves)
            {
                if (moveNamesList[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                {
                    moveNamesList[i] = normalEmoji + moveNamesList[i];
                    moveNamesList[i] = $"- {Regex.Replace(moveNamesList[i], "(\\p{Lu})", " $1")}";
                    break;
                }
            }
        }

        string movesDisplay = string.Join("\n", moveNamesList);
        string abilityName = GameInfo.AbilityDataSource.FirstOrDefault(a => a.Value == pk.Ability)?.Text ?? "";
        string natureName = GameInfo.NatureDataSource.FirstOrDefault(n => n.Value == (int)pk.Nature)?.Text ?? "";
        string teraTypeString;
        if (pk is PK9 pk9)
        {
            teraTypeString = pk9.TeraTypeOverride == (MoveType)99 ? "Stellar" : pk9.TeraType.ToString();
        }
        else
        {
            teraTypeString = ""; // or another default value as needed
        }
        int level = pk.CurrentLevel;
        string speciesName = GameInfo.GetStrings(1).Species[pk.Species];
        string formName = ShowdownParsing.GetStringFromForm(pk.Form, strings, pk.Species, pk.Context);
        string speciesAndForm = $"{speciesName}{(string.IsNullOrEmpty(formName) ? "" : $"-{formName}")}";
        string heldItemName = strings.itemlist[pk.HeldItem];
        string ballName = strings.balllist[pk.Ball];

        string formDecoration = "";
        if (pk.Species == (int)Species.Alcremie && formArgument != 0)
        {
            formDecoration = $"{(AlcremieDecoration)formArgument}";
        }

        // Determine if this is a clone or dump request
        bool isCloneRequest = type == PokeRoutineType.Clone;
        bool isDumpRequest = type == PokeRoutineType.Dump;
        bool FixOT = type == PokeRoutineType.FixOT;
        bool isSpecialRequest = type == PokeRoutineType.SeedCheck;

        // Check if the Pokémon is shiny and prepend the shiny emoji
        string shinyEmoji = pk.IsShiny ? "✨ " : "";
        string pokemonDisplayName = pk.IsNicknamed ? pk.Nickname : GameInfo.GetStrings(1).Species[pk.Species];
        string tradeTitle;

        if (isMysteryEgg)
        {
            tradeTitle = "✨ Huevo Misterioso Shiny ✨ de";
        }
        else if (isBatchTrade)
        {
            tradeTitle = $"Comercio por lotes #{batchTradeNumber} - {shinyEmoji}{pokemonDisplayName} de";
        }
        else if (FixOT)
        {
            tradeTitle = $"Solicitud de FixOT de ";
        }
        else if (isSpecialRequest)
        {
            tradeTitle = $"Solicitud Especial de";
        }
        else if (isCloneRequest)
        {
            tradeTitle = "Capsula de Clonación activada para";
        }
        else if (isDumpRequest)
        {
            tradeTitle = "Solicitud de Dump de";
        }
        else
        {
            tradeTitle = $"";
        }

        // Get the Pokémon's image URL and dominant color
        (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(context, pk, isCloneRequest || isDumpRequest, formName, formArgument);

        // Adjust the image URL for dump request
        if (isMysteryEgg)
        {
            embedImageUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/mysteryegg2.png"; // URL for mystery egg
        }
        else if (isDumpRequest)
        {
            embedImageUrl = "https://i.imgur.com/9wfEHwZ.png"; // URL for dump request
        }
        else if (isCloneRequest)
        {
            embedImageUrl = "https://i.imgur.com/aSTCjUn.png"; // URL for clone request
        }
        else if (isSpecialRequest)
        {
            embedImageUrl = "https://i.imgur.com/EI1BHr5.png"; // URL for clone request
        }
        else if (FixOT)
        {
            embedImageUrl = "https://i.imgur.com/gRZGFIi.png"; // URL for fixot request
        }
        string heldItemUrl = string.Empty;

        if (!string.IsNullOrWhiteSpace(heldItemName))
        {
            // Convert to lowercase and remove spaces
            heldItemName = heldItemName.ToLower().Replace(" ", "");
            heldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
        }
        // Check if the image URL is a local file path
        bool isLocalFile = File.Exists(embedImageUrl);
        string userName = user.Username;
        var gender = pk.Gender == 0 ? "<:Males:1134568420843728917> " : pk.Gender == 1 ? "<:Females:1134568421787435069> " : "";
        string isPkmShiny = pk.IsShiny ? "✨" : "";
        // Build the embed with the author title image
        string authorName;

        // Determine the author's name based on trade type
        if (isMysteryEgg || FixOT || isCloneRequest || isDumpRequest || isSpecialRequest || isBatchTrade)
        {
            authorName = $"{tradeTitle} {userName}";
        }
        else // Normal trade
        {
            authorName = $"{isPkmShiny} {pokemonDisplayName} de {userName} ";
        }
        var embedBuilder = new EmbedBuilder()
            .WithColor(embedColor)
            .WithImageUrl(embedImageUrl)
            .WithFooter($"Posición actual: {position.Position}\n{etaMessage}")
            .WithAuthor(new EmbedAuthorBuilder()
                .WithName(authorName)
                .WithIconUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()));

        // Add the additional text at the top as its own field
        string additionalText = string.Join("\n", SysCordSettings.Settings.AdditionalEmbedText);
        if (!string.IsNullOrEmpty(additionalText))
        {
            embedBuilder.AddField("\u200B", additionalText, inline: false); // '\u200B' is a zero-width space to create an empty title
        }

        // Conditionally add the 'Trainer' and 'Moves' fields based on trade type
        if (!isMysteryEgg && !isCloneRequest && !isDumpRequest && !FixOT && !isSpecialRequest)
        {
            // Prepare the left side content
            string leftSideContent = $"**Entrenador**: {user.Mention}\n" +
                                     $"**Pokémon**: {gender}{speciesAndForm}\n" +
                                     $"**Nivel**: {level}\n";

            // Add Tera Type information if the Pokémon is PK9 and the game version supports it
            if (pk is PK9 pk9Instance && (pk.Version == GameVersion.SL || pk.Version == GameVersion.VL))
            {
                var tera = pk9Instance.TeraType.ToString();
                Dictionary<string, string> teraEmojis = new Dictionary<string, string>
    {
        { "Normal", "<:Normal:1134575677648162886>" },
        { "Fire", "<:Fire:1134576993799766197>" },
        { "Water", "<:Water:1134575004038742156>" },
        { "Grass", "<:Grass:1134574800057139331>" },
        { "Flying", "<:Flying:1134573296734711918>" },
        { "Poison", "<:Poison:1134575188403564624>" },
        { "Electric", "<:Electric:1134576561991995442>" },
        { "Ground", "<:Ground:1134573701766058095>" },
        { "Psychic", "<:Psychic:1134576746298089575>" },
        { "Fighting", "<:Fighting:1134573062881300551>" },
        { "Rock", "<:Rock:1134574024542912572>" },
        { "Ice", "<:Ice:1134576183787409531>" },
        { "Bug", "<:Bug:1134574602908073984>" },
        { "Dragon", "<:Dragon:1134576015973294221>" },
        { "Ghost", "<:Ghost:1134574276628975626>" },
        { "Dark", "<:Dark:1134575488598294578>" },
        { "Steel", "<:Steel:1134576384191254599>" },
        { "Fairy", "<:Fairy:1134575841523814470>" },
        { "Stellar", "<:Stellar:1186199337177468929>" },
    };

                if (tera == "99") // Special case for Stellar
                {
                    leftSideContent += $"**Tera Tipo**: <:Stellar:1186199337177468929> Stellar\n";
                }
                else
                {
                    // Check if the Tera Type has a corresponding emoji
                    if (teraEmojis.TryGetValue(tera, out string? emojiID))
                    {
                        var emoji = new Emoji(emojiID); // Get emoji from the server using the ID
                        leftSideContent += $"**Tera Tipo**: {emoji} {tera}\n"; // Add emoji to the message
                    }
                    else
                    {
                        // If no corresponding emoji found, just display the Tera Type
                        leftSideContent += $"**Tera Tipo**: {tera}\n";
                    }
                }
            }
            leftSideContent += $"**Habilidad**: {abilityName}\n";
            if (!(pk is PB7)) // Exclude scale for PB7 type
            {
                leftSideContent += $"{scale}\n";
            };
            leftSideContent += $"**Naturaleza**: {natureName}\n" +
                               $"**IVs**: {ivsDisplay}\n";
            var evs = new List<string>();

            // Agregar los EVs no nulos al listado
            if (pk.EV_HP != 0)
                evs.Add($"{pk.EV_HP} HP");

            if (pk.EV_ATK != 0)
                evs.Add($"{pk.EV_ATK} Atk");

            if (pk.EV_DEF != 0)
                evs.Add($"{pk.EV_DEF} Def");

            if (pk.EV_SPA != 0)
                evs.Add($"{pk.EV_SPA} SpA");

            if (pk.EV_SPD != 0)
                evs.Add($"{pk.EV_SPD} SpD");

            if (pk.EV_SPE != 0)
                evs.Add($"{pk.EV_SPE} Spe");

            // Comprobar si hay EVs para agregarlos al mensaje
            if (evs.Any())
            {
                leftSideContent += "**EVs**: " + string.Join(" / ", evs) + "\n";
            }
            // Add the field to the embed
            embedBuilder.AddField("**__Información__**", leftSideContent, inline: true);
            // Add a blank field to align with the 'Trainer' field on the left
            embedBuilder.AddField("\u200B", "\u200B", inline: true); // First empty field for spacing
            // 'Moves' as another inline field, ensuring it's aligned with the content on the left
            embedBuilder.AddField("**__Movimientos__**", movesDisplay, inline: true);
        }
        else
        {
            // For special cases, add only the special description
            string specialDescription = $"**Entrenador**: {user.Mention}\n" +
                                        (isMysteryEgg ? "Huevo Misterioso" : isSpecialRequest ? "Solicitud Especial" : isCloneRequest ? "Solicitud de clonación" : FixOT ? "Solicitud de FixOT" : "Solicitud de Dump");
            embedBuilder.AddField("\u200B", specialDescription, inline: false);
        }

        // Set thumbnail images
        if (isCloneRequest || isSpecialRequest || isDumpRequest || FixOT)
        {
            embedBuilder.WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png");
        }
        else if (!string.IsNullOrEmpty(heldItemUrl))
        {
            embedBuilder.WithThumbnailUrl(heldItemUrl);
        }

        // If the image is a local file, set the image URL to the attachment reference
        if (isLocalFile)
        {
            embedBuilder.WithImageUrl($"attachment://{Path.GetFileName(embedImageUrl)}");
        }

        var embed = embedBuilder.Build();
        if (embed == null)
        {
            Console.WriteLine("Error: Embed is null.");
            await context.Channel.SendMessageAsync("Se produjo un error al preparar los detalles del trade.");
            return new TradeQueueResult(false);
        }

        if (isLocalFile)
        {
            // Send the message with the file and embed, referencing the file in the embed
            await context.Channel.SendFileAsync(embedImageUrl, embed: embed);

            if (isBatchTrade)
            {
                // Update the highest detail.ID for this user's batch trades
                if (!userBatchTradeMaxDetailId.ContainsKey(userID) || userBatchTradeMaxDetailId[userID] < detail.ID)
                {
                    userBatchTradeMaxDetailId[userID] = detail.ID;
                }

                // Schedule file deletion for batch trade
                await ScheduleFileDeletion(embedImageUrl, 0, detail.ID);

                // Check if this is the last trade in the batch for the user
                if (detail.ID == userBatchTradeMaxDetailId[userID] && batchTradeNumber == totalBatchTrades)
                {
                    DeleteBatchTradeFiles(detail.ID);
                }
            }
            else
            {
                // For non-batch trades, just schedule file deletion normally
                await ScheduleFileDeletion(embedImageUrl, 0);
            }
        }
        else
        {
            await context.Channel.SendMessageAsync(embed: embed);
        }

        return new TradeQueueResult(true);
    }

    private static string GetImageFolderPath()
    {
        // Get the base directory where the executable is located
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Define the path for the images subfolder
        string imagesFolder = Path.Combine(baseDirectory, "Images");

        // Check if the folder exists, if not, create it
        if (!Directory.Exists(imagesFolder))
        {
            Directory.CreateDirectory(imagesFolder);
        }

        return imagesFolder;
    }

    private static string SaveImageLocally(System.Drawing.Image image)
    {
        // Get the path to the images folder
        string imagesFolderPath = GetImageFolderPath();

        // Create a unique filename for the image
        string filePath = Path.Combine(imagesFolderPath, $"image_{Guid.NewGuid()}.png");

        // Save the image to the specified path
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        return filePath;
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetails(SocketCommandContext context, T pk, bool isCloneRequest, string formName, int formArgument = 0)
    {
        string embedImageUrl;
        string speciesImageUrl;

        if (pk.IsEgg)
        {
            string eggImageUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/egg.png";
            speciesImageUrl = AbstractTrade<T>.PokeImg(pk, false, true);
            System.Drawing.Image combinedImage = await OverlaySpeciesOnEgg(eggImageUrl, speciesImageUrl);
            embedImageUrl = SaveImageLocally(combinedImage);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            speciesImageUrl = AbstractTrade<T>.PokeImg(pk, canGmax, false);
            embedImageUrl = speciesImageUrl;
        }

        // Determine ball image URL
        var strings = GameInfo.GetStrings(1);
        string ballName = strings.balllist[pk.Ball];

        // Check for "(LA)" in the ball name
        if (ballName.Contains("(LA)"))
        {
            ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
        }
        else
        {
            ballName = ballName.Replace(" ", "").ToLower();
        }

        string ballImgUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/AltBallImg/28x28/{ballName}.png";

        // Check if embedImageUrl is a local file or a web URL
        if (Uri.TryCreate(embedImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
            // Load local image directly
            using (var localImage = System.Drawing.Image.FromFile(uri.LocalPath))
            using (var ballImage = await LoadImageFromUrl(ballImgUrl))
            {
                if (ballImage != null)
                {
                    using (var graphics = Graphics.FromImage(localImage))
                    {
                        var ballPosition = new Point(localImage.Width - ballImage.Width, localImage.Height - ballImage.Height);
                        graphics.DrawImage(ballImage, ballPosition);
                    }
                    embedImageUrl = SaveImageLocally(localImage);
                }
            }
        }
        else
        {
            // Load web image and overlay ball
            (System.Drawing.Image finalCombinedImage, bool ballImageLoaded) = await OverlayBallOnSpecies(speciesImageUrl, ballImgUrl);
            embedImageUrl = SaveImageLocally(finalCombinedImage);

            if (!ballImageLoaded)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImgUrl}");
               // await context.Channel.SendMessageAsync($"Ball image could not be loaded: {ballImgUrl}");
            }
        }

        (int R, int G, int B) = await GetDominantColorAsync(embedImageUrl);
        return (embedImageUrl, new DiscordColor(R, G, B));
    }

    private static async Task<(System.Drawing.Image, bool)> OverlayBallOnSpecies(string speciesImageUrl, string ballImageUrl)
    {
        using (var speciesImage = await LoadImageFromUrl(speciesImageUrl))
        {
            if (speciesImage == null)
            {
                Console.WriteLine("Species image could not be loaded.");
                return (null, false);
            }

            var ballImage = await LoadImageFromUrl(ballImageUrl);
            if (ballImage == null)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImageUrl}");
                return ((System.Drawing.Image)speciesImage.Clone(), false); // Return false indicating failure
            }

            using (ballImage)
            {
                using (var graphics = Graphics.FromImage(speciesImage))
                {
                    var ballPosition = new Point(speciesImage.Width - ballImage.Width, speciesImage.Height - ballImage.Height);
                    graphics.DrawImage(ballImage, ballPosition);
                }

                return ((System.Drawing.Image)speciesImage.Clone(), true); // Return true indicating success
            }
        }
    }
    private static async Task<System.Drawing.Image> OverlaySpeciesOnEgg(string eggImageUrl, string speciesImageUrl)
    {
        // Load both images
        System.Drawing.Image eggImage = await LoadImageFromUrl(eggImageUrl);
        System.Drawing.Image speciesImage = await LoadImageFromUrl(speciesImageUrl);

        // Calculate the ratio to scale the species image to fit within the egg image size
        double scaleRatio = Math.Min((double)eggImage.Width / speciesImage.Width, (double)eggImage.Height / speciesImage.Height);

        // Create a new size for the species image, ensuring it does not exceed the egg dimensions
        Size newSize = new Size((int)(speciesImage.Width * scaleRatio), (int)(speciesImage.Height * scaleRatio));

        // Resize species image
        System.Drawing.Image resizedSpeciesImage = new Bitmap(speciesImage, newSize);

        // Create a graphics object for the egg image
        using (Graphics g = Graphics.FromImage(eggImage))
        {
            // Calculate the position to center the species image on the egg image
            int speciesX = (eggImage.Width - resizedSpeciesImage.Width) / 2;
            int speciesY = (eggImage.Height - resizedSpeciesImage.Height) / 2;

            // Draw the resized and centered species image over the egg image
            g.DrawImage(resizedSpeciesImage, speciesX, speciesY, resizedSpeciesImage.Width, resizedSpeciesImage.Height);
        }

        // Dispose of the species image and the resized species image if they're no longer needed
        speciesImage.Dispose();
        resizedSpeciesImage.Dispose();

        // Calculate scale factor for resizing while maintaining aspect ratio
        double scale = Math.Min(128.0 / eggImage.Width, 128.0 / eggImage.Height);

        // Calculate new dimensions
        int newWidth = (int)(eggImage.Width * scale);
        int newHeight = (int)(eggImage.Height * scale);

        // Create a new 128x128 bitmap
        Bitmap finalImage = new Bitmap(128, 128);

        // Draw the resized egg image onto the new bitmap, centered
        using (Graphics g = Graphics.FromImage(finalImage))
        {
            // Calculate centering position
            int x = (128 - newWidth) / 2;
            int y = (128 - newHeight) / 2;

            // Draw the image
            g.DrawImage(eggImage, x, y, newWidth, newHeight);
        }

        // Dispose of the original egg image if it's no longer needed
        eggImage.Dispose();

        // The finalImage now contains the overlay, is resized, and maintains aspect ratio
        return finalImage;
    }

    private static async Task<System.Drawing.Image> LoadImageFromUrl(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to load image from {url}. Status code: {response.StatusCode}");
                return null;
            }

            Stream stream = await response.Content.ReadAsStreamAsync();
            if (stream == null || stream.Length == 0)
            {
                Console.WriteLine($"No data or empty stream received from {url}");
                return null;
            }

            try
            {
                return System.Drawing.Image.FromStream(stream);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Failed to create image from stream. URL: {url}, Exception: {ex}");
                return null;
            }
        }
    }

    private static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds, int batchTradeId = -1)
    {
        if (batchTradeId != -1)
        {
            // If this is part of a batch trade, add the file path to the dictionary
            if (!batchTradeFiles.ContainsKey(batchTradeId))
            {
                batchTradeFiles[batchTradeId] = new List<string>();
            }

            batchTradeFiles[batchTradeId].Add(filePath);
        }
        else
        {
            // If this is not part of a batch trade, delete the file after the delay
            await Task.Delay(delayInMilliseconds);
            DeleteFile(filePath);
        }
    }

    private static void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }

    // Call this method after the last trade in a batch is completed
    private static void DeleteBatchTradeFiles(int batchTradeId)
    {
        if (batchTradeFiles.TryGetValue(batchTradeId, out var files))
        {
            foreach (var filePath in files)
            {
                DeleteFile(filePath);
            }
            batchTradeFiles.Remove(batchTradeId);
        }
    }

    public enum AlcremieDecoration
    {
        Strawberry = 0,
        Berry = 1,
        Love = 2,
        Star = 3,
        Clover = 4,
        Flower = 5,
        Ribbon = 6,
    }

    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixelColor = image.GetPixel(x, y);

                    if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                    var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                    var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                    var combinedFactor = brightnessFactor + saturationFactor;

                    var quantizedColor = Color.FromArgb(
                        pixelColor.R / 10 * 10,
                        pixelColor.G / 10 * 10,
                        pixelColor.B / 10 * 10
                    );

                    if (colorCount.ContainsKey(quantizedColor))
                    {
                        colorCount[quantizedColor] += combinedFactor;
                    }
                    else
                    {
                        colorCount[quantizedColor] = combinedFactor;
                    }
                }
            }

            image.Dispose();

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"Error processing image from {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            using var stream = await response.Content.ReadAsStreamAsync();
            return new Bitmap(stream);
        }
        else
        {
            return new Bitmap(imagePath);
        }
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        // Nag the owner in logs.
                        message = "¡Debes otorgarme permisos para \"Enviar mensajes\"!";
                        Base.LogUtil.LogError(message, "QueueHelper");
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> ¡Debes otorgarme permisos de \"Administrar mensajes\"!";
                    }
                }
                break;
            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    // The user either has DMs turned off, or Discord thinks they do.
                    message = context.User == trader ? "Debes habilitar los mensajes privados para estar en la cola.!" : "El usuario mencionado debe habilitar los mensajes privados para que estén en cola.!";
                }
                break;
            default:
                {
                    // Send a generic error message.
                    message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    public static (string, Embed) CreateLGLinkCodeSpriteEmbed(List<Pictocodes> lgcode)
    {
        int codecount = 0;
        List<System.Drawing.Image> spritearray = new();
        foreach (Pictocodes cd in lgcode)
        {


            var showdown = new ShowdownSet(cd.ToString());
            var sav = SaveUtil.GetBlankSAV(EntityContext.Gen7b, "pip");
            PKM pk = sav.GetLegalFromSet(showdown).Created;
            System.Drawing.Image png = pk.Sprite();
            var destRect = new Rectangle(-40, -65, 137, 130);
            var destImage = new Bitmap(137, 130);

            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);

            }
            png = destImage;
            spritearray.Add(png);
            codecount++;
        }
        int outputImageWidth = spritearray[0].Width + 20;

        int outputImageHeight = spritearray[0].Height - 65;

        Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
        }
        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png";
        finalembedpic.Save(filename);
        filename = System.IO.Path.GetFileName($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }
}
