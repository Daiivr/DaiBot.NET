using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon.Helpers
{
    public abstract class TradeExtensions<T> where T : PKM, new()
    {
        public static readonly string[] MarkTitle =
        [
            "el Hambriento",
            "el Somnoliento",
            "el Adormilado",
            "el Madrugador",
            "el Obnubilado",
            "el Empapado",
            "el Atronador",
            "el Níveo",
            "el Aterido",
            "el Sediento",
            "el Arenoso",
            "el Errante de la Niebla",
            "el Predestinado",
            "el Recién Pescado",
            "el Entusiasta del Curri",
            "el Sociable",
            "el Ermitaño",
            "el Travieso",
            "el Despreocupado",
            "el Nervioso",
            "el Ilusionado",
            "el Carismático",
            "el Sereno",
            "el Apasionado",
            "el Distraído",
            "el Feliz",
            "el Colérico",
            "el Sonriente",
            "el Llorón",
            "el Bienhumorado",
            "el Malhumorado",
            "el Intelectual",
            "el Impulsivo",
            "el Astuto",
            "el Amenazante",
            "el Amable",
            "el Aturullado",
            "el Motivado",
            "el Desidioso",
            "el Confiado",
            "el Inseguro",
            "el Humilde",
            "el Pretencioso",
            "el Vigoroso",
            "el Extenuado",
            "el Viajero del Pasado",
            "el Rutilante",
            "el Campeón de Paldea",
            "el Gigante",
            "el Diminuto",
            "el Recolector",
            "el Compañero Leal",
            "el Sibarita",
            "el Excepcional",
            "el Antiguo Alfa",
            "el Imbatible",
            "el Antiguo Dominante",
        ];

        public static readonly ushort[] ShinyLock = [  (ushort)Species.Victini, (ushort)Species.Keldeo, (ushort)Species.Volcanion, (ushort)Species.Cosmog, (ushort)Species.Cosmoem, (ushort)Species.Magearna, (ushort)Species.Marshadow, (ushort)Species.Eternatus,
                                                    (ushort)Species.Kubfu, (ushort)Species.Urshifu, (ushort)Species.Zarude, (ushort)Species.Glastrier, (ushort)Species.Spectrier, (ushort)Species.Calyrex ];

        public static T CherishHandler(MysteryGift mg, ITrainerInfo info)
        {
            var result = EntityConverterResult.None;
            var mgPkm = mg.ConvertToPKM(info);
            bool canConvert = EntityConverter.IsConvertibleToFormat(mgPkm, info.Generation);
            mgPkm = canConvert ? EntityConverter.ConvertToType(mgPkm, typeof(T), out result) : mgPkm;

            if (mgPkm is not null && result is EntityConverterResult.Success)
            {
                var enc = new LegalityAnalysis(mgPkm).EncounterMatch;
                mgPkm.SetHandlerandMemory(info, enc);

                if (mgPkm.TID16 is 0 && mgPkm.SID16 is 0)
                {
                    mgPkm.TID16 = info.TID16;
                    mgPkm.SID16 = info.SID16;
                }

                mgPkm.CurrentLevel = mg.LevelMin;
                if (mgPkm.Species is (ushort)Species.Giratina && mgPkm.Form > 0)
                    mgPkm.HeldItem = 112;
                else if (mgPkm.Species is (ushort)Species.Silvally && mgPkm.Form > 0)
                    mgPkm.HeldItem = mgPkm.Form + 903;
                else mgPkm.HeldItem = 0;
            }
            else
            {
                return new();
            }

            mgPkm = TrashBytes((T)mgPkm);
            var la = new LegalityAnalysis(mgPkm);
            if (!la.Valid)
            {
                mgPkm.SetRandomIVs(6);
                var text = ShowdownParsing.GetShowdownText(mgPkm);
                var set = new ShowdownSet(text);
                var template = AutoLegalityWrapper.GetTemplate(set);
                var pk = AutoLegalityWrapper.GetLegal(info, template, out _);
                pk.SetAllTrainerData(info);
                return (T)pk;
            }
            else
            {
                return (T)mgPkm;
            }
        }

        public static void DittoTrade(PKM pkm)
        {
            var dittoStats = new string[] { "atk", "spe", "spa" };
            var nickname = pkm.Nickname.ToLower();
            pkm.StatNature = pkm.Nature;
            pkm.MetLocation = pkm switch
            {
                PB8 => 400,
                PK9 => 28,
                _ => 162, // PK8
            };

            pkm.MetLevel = pkm switch
            {
                PB8 => 29,
                PK9 => 34,
                _ => pkm.MetLevel,
            };

            if (pkm is PK9 pk9)
            {
                pk9.ObedienceLevel = pk9.MetLevel;
                pk9.TeraTypeOriginal = PKHeX.Core.MoveType.Normal;
                pk9.TeraTypeOverride = (PKHeX.Core.MoveType)19;
            }

            pkm.Ball = 21;
            pkm.IVs = [31, nickname.Contains(dittoStats[0]) ? 0 : 31, 31, nickname.Contains(dittoStats[1]) ? 0 : 31, nickname.Contains(dittoStats[2]) ? 0 : 31, 31];
            pkm.ClearHyperTraining();
            TrashBytes(pkm, new LegalityAnalysis(pkm));
        }

        // https://github.com/Koi-3088/ForkBot.NET/blob/KoiTest/SysBot.Pokemon/Helpers/TradeExtensions.cs
        public static void EggTrade(PKM pk, IBattleTemplate template, bool nicknameEgg = true)
        {
            if (nicknameEgg)
            {
                pk.IsNicknamed = true;
                pk.Nickname = pk.Language switch
                {
                    1 => "タマゴ",
                    3 => "Œuf",
                    4 => "Uovo",
                    5 => "Ei",
                    7 => "Huevo",
                    8 => "알",
                    9 or 10 => "蛋",
                    _ => "Egg",
                };
            }
            else
            {
                pk.IsNicknamed = false;
                pk.Nickname = "";
            }

            pk.IsEgg = true;
            pk.EggLocation = pk switch
            {
                PB8 => 60010,
                PK9 => 30023,
                _ => 60002, //PK8
            };

            pk.MetDate = DateOnly.FromDateTime(DateTime.Now);
            pk.EggMetDate = pk.MetDate;
            pk.HeldItem = 0;
            pk.CurrentLevel = 1;
            pk.EXP = 0;
            pk.MetLevel = 1;
            pk.MetLocation = pk switch
            {
                PB8 => 65535,
                PK9 => 0,
                _ => 30002, //PK8
            };

            pk.CurrentHandler = 0;
            pk.OriginalTrainerFriendship = 1;
            pk.HandlingTrainerName = "";
            ClearHandlingTrainerTrash(pk);
            pk.HandlingTrainerFriendship = 0;
            pk.ClearMemories();
            pk.StatNature = pk.Nature;
            pk.SetEVs([0, 0, 0, 0, 0, 0]);

            MarkingApplicator.SetMarkings(pk);

            pk.ClearRelearnMoves();

            if (pk is PK8 pk8)
            {
                pk8.HandlingTrainerLanguage = 0;
                pk8.HandlingTrainerGender = 0;
                pk8.HandlingTrainerMemory = 0;
                pk8.HandlingTrainerMemoryFeeling = 0;
                pk8.HandlingTrainerMemoryIntensity = 0;
                pk8.DynamaxLevel = pk8.GetSuggestedDynamaxLevel(pk8, 0);
            }
            else if (pk is PB8 pb8)
            {
                pb8.HandlingTrainerLanguage = 0;
                pb8.HandlingTrainerGender = 0;
                pb8.HandlingTrainerMemory = 0;
                pb8.HandlingTrainerMemoryFeeling = 0;
                pb8.HandlingTrainerMemoryIntensity = 0;
                pb8.DynamaxLevel = pb8.GetSuggestedDynamaxLevel(pb8, 0);
                ClearNicknameTrash(pk);
            }
            else if (pk is PK9 pk9)
            {
                pk9.HandlingTrainerLanguage = 0;
                pk9.HandlingTrainerGender = 0;
                pk9.HandlingTrainerMemory = 0;
                pk9.HandlingTrainerMemoryFeeling = 0;
                pk9.HandlingTrainerMemoryIntensity = 0;
                pk9.ObedienceLevel = 1;
                pk9.Version = 0;
                pk9.BattleVersion = 0;
                pk9.TeraTypeOverride = (PKHeX.Core.MoveType)19;
            }

            var la = new LegalityAnalysis(pk);
            var enc = la.EncounterMatch;
            pk.MaximizeFriendship();

            Span<ushort> relearn = stackalloc ushort[4];
            la.GetSuggestedRelearnMoves(relearn, enc);
            pk.SetRelearnMoves(relearn);
            if (pk is ITechRecord t)
            {
                t.ClearRecordFlags();
            }
            pk.SetSuggestedMoves();

            pk.Move1_PPUps = pk.Move2_PPUps = pk.Move3_PPUps = pk.Move4_PPUps = 0;
            pk.SetMaximumPPCurrent(pk.Moves);
            pk.SetSuggestedHyperTrainingData();
            pk.SetSuggestedRibbons(template, enc, true);
        }

        public static string FormOutput(ushort species, byte form, out string[] formString)
        {
            var strings = GameInfo.GetStrings("en");
            formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(T) == typeof(PK8) ? EntityContext.Gen8 : EntityContext.Gen4);
            if (formString.Length is 0)
                return string.Empty;

            formString[0] = "";
            if (form >= formString.Length)
                form = (byte)(formString.Length - 1);

            return formString[form].Contains('-') ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
        }

        public static bool HasAdName(T pk, out string ad)
        {
            const string pattern = @"(YT$)|(YT\w*$)|(Lab$)|(\.\w*$|\.\w*\/)|(TV$)|(PKHeX)|(FB:)|(AuSLove)|(ShinyMart)|(Blainette)|(\ com)|(\ org)|(\ net)|(2DOS3)|(PPorg)|(Tik\wok$)|(YouTube)|(IG:)|(TTV\ )|(Tools)|(JokersWrath)|(bot$)|(PKMGen)|(TheHighTable)";
            bool ot = Regex.IsMatch(pk.OriginalTrainerName, pattern, RegexOptions.IgnoreCase);
            bool nick = Regex.IsMatch(pk.Nickname, pattern, RegexOptions.IgnoreCase);
            ad = ot ? pk.OriginalTrainerName : nick ? pk.Nickname : "";
            return ot || nick;
        }

        public static bool HasMark(IRibbonIndex pk, out RibbonIndex result, out string markTitle)
        {
            result = default;
            markTitle = string.Empty;

            if (pk is IRibbonSetMark9 ribbonSetMark)
            {
                if (ribbonSetMark.RibbonMarkMightiest)
                {
                    result = RibbonIndex.MarkMightiest;
                    markTitle = "el Imbatible";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkAlpha)
                {
                    result = RibbonIndex.MarkAlpha;
                    markTitle = "el Antiguo Alfa";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkTitan)
                {
                    result = RibbonIndex.MarkTitan;
                    markTitle = "el Antiguo Dominante";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkJumbo)
                {
                    result = RibbonIndex.MarkJumbo;
                    markTitle = "el Gigante";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkMini)
                {
                    result = RibbonIndex.MarkMini;
                    markTitle = "el Diminuto";
                    return true;
                }
            }

            for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
            {
                if (pk.GetRibbon((int)mark))
                {
                    result = mark;
                    markTitle = MarkTitle[(int)mark - (int)RibbonIndex.MarkLunchtime];
                    return true;
                }
            }

            return false;
        }

        public static string PokeImg(PKM pkm, bool canGmax, bool fullSize, ImageSize? preferredImageSize = null)
        {
            bool md = false;
            bool fd = false;
            string[] baseLink;

            if (fullSize)
            {
                baseLink = "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
            }
            else if (preferredImageSize.HasValue)
            {
                baseLink = preferredImageSize.Value switch
                {
                    ImageSize.Size256x256 => "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/256x256/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_'),
                    ImageSize.Size128x128 => "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/128x128/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_'),
                    _ => "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/256x256/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_'),
                };
            }
            else
            {
                baseLink = "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/256x256/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
            }

            if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
            {
                if (pkm.Gender == 0 && pkm.Species != (int)Species.Torchic)
                    md = true;
                else fd = true;
            }

            int form = pkm.Species switch
            {
                (int)Species.Sinistea or (int)Species.Polteageist or (int)Species.Rockruff or (int)Species.Mothim => 0,
                (int)Species.Alcremie when pkm.IsShiny || canGmax => 0,
                _ => pkm.Form,
            };

            if (pkm.Species is (ushort)Species.Sneasel)
            {
                if (pkm.Gender is 0)
                    md = true;
                else fd = true;
            }

            if (pkm.Species is (ushort)Species.Basculegion)
            {
                if (pkm.Gender is 0)
                {
                    md = true;
                    pkm.Form = 0;
                }
                else
                {
                    pkm.Form = 1;
                }

                string s = pkm.IsShiny ? "r" : "n";
                string g = md && pkm.Gender is not 1 ? "md" : "fd";
                return "https://raw.githubusercontent.com/bdawg1989/HomeImages/master/256x256/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
            }

            baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
            baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
            baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
            baseLink[5] = canGmax ? "g" : "n";
            baseLink[6] = "0000000" + ((pkm.Species == (int)Species.Alcremie && !canGmax) ? ((IFormArgument)pkm).FormArgument.ToString() : "0");
            baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
            return string.Join("_", baseLink);
        }

        public static bool ShinyLockCheck(ushort species, string form, string ball = "")
        {
            if (ShinyLock.Contains(species))
                return true;
            else if (form is not "" && (species is (ushort)Species.Zapdos or (ushort)Species.Moltres or (ushort)Species.Articuno))
                return true;
            else if (ball.Contains("Beast") && (species is (ushort)Species.Poipole or (ushort)Species.Naganadel))
                return true;
            else if (typeof(T) == typeof(PB8) && (species is (ushort)Species.Manaphy or (ushort)Species.Mew or (ushort)Species.Jirachi))
                return true;
            else if (species is (ushort)Species.Pikachu && form is not "" && form is not "-Partner")
                return true;
            else if ((species is (ushort)Species.Zacian or (ushort)Species.Zamazenta) && !ball.Contains("Cherish"))
                return true;
            return false;
        }

        public static PKM TrashBytes(PKM pkm, LegalityAnalysis? la = null)
        {
            var pkMet = (T)pkm.Clone();
            if (pkMet.Version is not GameVersion.GO)
                pkMet.MetDate = DateOnly.FromDateTime(DateTime.Now);

            var analysis = new LegalityAnalysis(pkMet);
            var pkTrash = (T)pkMet.Clone();
            if (analysis.Valid)
            {
                pkTrash.IsNicknamed = true;
                pkTrash.Nickname = "UwU";
                pkTrash.SetDefaultNickname(la ?? new LegalityAnalysis(pkTrash));
            }

            if (new LegalityAnalysis(pkTrash).Valid)
                pkm = pkTrash;
            else if (analysis.Valid)
                pkm = pkMet;
            return pkm;
        }
        private static void ClearNicknameTrash(PKM pokemon)
        {
            switch (pokemon)
            {
                case PK9 pk9:
                    ClearTrash(pk9.NicknameTrash, pk9.Nickname);
                    break;
                case PA8 pa8:
                    ClearTrash(pa8.NicknameTrash, pa8.Nickname);
                    break;
                case PB8 pb8:
                    ClearTrash(pb8.NicknameTrash, pb8.Nickname);
                    break;
                case PB7 pb7:
                    ClearTrash(pb7.NicknameTrash, pb7.Nickname);
                    break;
                case PK8 pk8:
                    ClearTrash(pk8.NicknameTrash, pk8.Nickname);
                    break;
            }
        }

        private static void ClearTrash(Span<byte> trash, string name)
        {
            trash.Clear();
            int maxLength = trash.Length / 2;
            int actualLength = Math.Min(name.Length, maxLength);
            for (int i = 0; i < actualLength; i++)
            {
                char value = name[i];
                trash[i * 2] = (byte)value;
                trash[(i * 2) + 1] = (byte)(value >> 8);
            }
            if (actualLength < maxLength)
            {
                trash[actualLength * 2] = 0x00;
                trash[(actualLength * 2) + 1] = 0x00;
            }
        }

        private static void ClearHandlingTrainerTrash(PKM pk)
        {
            switch (pk)
            {
                case PK8 pk8:
                    ClearTrash(pk8.HandlingTrainerTrash, "");
                    break;
                case PB8 pb8:
                    ClearTrash(pb8.HandlingTrainerTrash, "");
                    break;
                case PK9 pk9:
                    ClearTrash(pk9.HandlingTrainerTrash, "");
                    break;
            }
        }

        public static bool IsEggCheck(string showdownSet)
        {
            // Get the first line of the showdown set
            var firstLine = showdownSet.Split('\n').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return false;
            }
            var atIndex = firstLine.IndexOf('@');
            if (atIndex > 0)
            {
                return firstLine[..atIndex].Contains("Egg", StringComparison.OrdinalIgnoreCase);
            }
            return firstLine.Contains("Egg", StringComparison.OrdinalIgnoreCase);
        }

        // Correct Met Dates for 7 Star Raids w/ Mightiest Mark
        private static readonly Dictionary<int, List<(DateOnly Start, DateOnly End)>> UnrivaledDateRanges = new()
        {
            // Generation 1
            [(int)Species.Charizard] = [(new(2022, 12, 02), new(2022, 12, 04)), (new(2022, 12, 16), new(2022, 12, 18)), (new(2024, 03, 13), new(2024, 03, 17))], // Charizard
            [(int)Species.Venusaur] = [(new(2024, 02, 28), new(2024, 03, 05))], // Venusaur
            [(int)Species.Blastoise] = [(new(2024, 03, 06), new(2024, 03, 12))], // Blastoise

            // Generation 2
            [(int)Species.Meganium] = [(new(2024, 04, 05), new(2024, 04, 07)), (new(2024, 04, 12), new(2024, 04, 14))], // Meganium
            [(int)Species.Typhlosion] = [(new(2023, 04, 14), new(2023, 04, 16)), (new(2023, 04, 21), new(2023, 04, 23))], // Typhlosion

            // Generation 3
            [(int)Species.Sceptile] = [(new(2024, 06, 28), new(2024, 06, 30)), (new(2024, 07, 05), new(2024, 07, 07))], // Sceptile
            [(int)Species.Blaziken] = [(new(2024, 01, 12), new(2024, 01, 14)), (new(2024, 01, 19), new(2024, 01, 21))], // Blaziken
            [(int)Species.Swampert] = [(new(2024, 05, 31), new(2024, 06, 02)), (new(2024, 06, 07), new(2024, 06, 09))], // Swampert

            // Generation 4
            [(int)Species.Empoleon] = [(new(2024, 02, 02), new(2024, 02, 04)), (new(2024, 02, 09), new(2024, 02, 11))], // Empoleon

            // Generation 5
            [(int)Species.Emboar] = [(new(2024, 06, 14), new(2024, 06, 16)), (new(2024, 06, 21), new(2024, 06, 23))], // Emboar

            // Generation 6
            [(int)Species.Chesnaught] = [(new(2023, 05, 12), new(2023, 05, 14)), (new(2023, 06, 16), new(2023, 06, 18))], // Chesnaught
            [(int)Species.Delphox] = [(new(2023, 07, 07), new(2023, 07, 09)), (new(2023, 07, 14), new(2023, 07, 16))], // Delphox

            // Generation 7
            [(int)Species.Decidueye] = [(new(2023, 03, 17), new(2023, 03, 19)), (new(2023, 03, 24), new(2023, 03, 26))], // Decidueye
            [(int)Species.Primarina] = [(new(2024, 05, 10), new(2024, 05, 12)), (new(2024, 05, 17), new(2024, 05, 19))], // Primarina

            // Generation 8
            [(int)Species.Rillaboom] = [(new(2023, 07, 28), new(2023, 07, 30)), (new(2023, 08, 04), new(2023, 08, 06))], // Rillaboom
            [(int)Species.Cinderace] = [(new(2022, 12, 30), new(2023, 01, 01)), (new(2023, 01, 13), new(2023, 01, 15))], // Cinderace
            [(int)Species.Inteleon] = [(new(2023, 04, 28), new(2023, 04, 30)), (new(2023, 05, 05), new(2023, 05, 07))], // Inteleon

            // Others
            [(int)Species.Pikachu] = [(new(2023, 02, 24), new(2023, 02, 27)), (new(2024, 07, 12), new(2024, 07, 25))], // Pikachu
            [(int)Species.Eevee] = [(new(2023, 11, 17), new(2023, 11, 20))], // Eevee
            [(int)Species.Mewtwo] = [(new(2023, 09, 01), new(2023, 09, 17))], // Mewtwo
            [(int)Species.Greninja] = [(new(2023, 01, 27), new(2023, 01, 29)), (new(2023, 02, 10), new(2023, 02, 12))], // Greninja
            [(int)Species.Samurott] = [(new(2023, 03, 31), new(2023, 04, 02)), (new(2023, 04, 07), new(2023, 04, 09))], // Samurott
            [(int)Species.IronBundle] = [(new(2023, 12, 22), new(2023, 12, 24))], // Iron Bundle
            [(int)Species.Dondozo] = [(new(2024, 07, 26), new(2024, 08, 08))], // Dondozo
            [(int)Species.Dragonite] = [(new(2024, 08, 23), new(2024, 09, 01))], // Dragonite
        };

        public static void CheckAndSetUnrivaledDate(PKM pk)
        {
            if (pk is not IRibbonSetMark9 ribbonSetMark || !ribbonSetMark.RibbonMarkMightiest)
                return;

            List<(DateOnly Start, DateOnly End)> dateRanges;

            if (UnrivaledDateRanges.TryGetValue(pk.Species, out var ranges))
            {
                dateRanges = ranges;
            }
            else if (pk.Species is (int)Species.Decidueye or (int)Species.Typhlosion or (int)Species.Samurott && pk.Form == 1)
            {
                // Special handling for Hisuian forms
                dateRanges = pk.Species switch
                {
                    (int)Species.Decidueye => [(new(2023, 10, 06), new(2023, 10, 08)), (new(2023, 10, 13), new(2023, 10, 15))],
                    (int)Species.Typhlosion => [(new(2023, 11, 03), new(2023, 11, 05)), (new(2023, 11, 10), new(2023, 11, 12))],
                    (int)Species.Samurott => [(new(2023, 11, 24), new(2023, 11, 26)), (new(2023, 12, 01), new(2023, 12, 03))],
                    _ => []
                };
            }
            else
            {
                return;
            }

            if (!pk.MetDate.HasValue || !IsDateInRanges(pk.MetDate.Value, dateRanges))
            {
                SetRandomDateFromRanges(pk, dateRanges);
            }
        }

        private static bool IsDateInRanges(DateOnly date, List<(DateOnly Start, DateOnly End)> ranges)
            => ranges.Any(range => date >= range.Start && date <= range.End);

        private static void SetRandomDateFromRanges(PKM pk, List<(DateOnly Start, DateOnly End)> ranges)
        {
            var (Start, End) = ranges[Random.Shared.Next(ranges.Count)];
            int rangeDays = End.DayNumber - Start.DayNumber + 1;
            pk.MetDate = Start.AddDays(Random.Shared.Next(rangeDays));
        }
    }
}
