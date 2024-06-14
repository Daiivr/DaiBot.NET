using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon.Helpers
{
    /// <summary>
    /// Pokémon abstract transaction class
    /// This class needs to implement SendMessage and also implement a multi-parameter constructor.
    /// The parameters should include information about the message sent by this type of robot so that SendMessage can be used
    /// Note that SetPokeTradeTrainerInfo and SetTradeQueueInfo must be called in the constructor of the abstract class.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class TradeExtensions<T> where T : PKM, new()
    {
        public static readonly string[] MarkTitle =
        [
            " el Hambriento",
            " el Somnoliento",
            " el Adormilado",
            " el Madrugador",
            " el Obnubilado",
            " el Empapado",
            " el Atronador",
            " el Níveo",
            " el Aterido",
            " el Sediento",
            " el Arenoso",
            " el Errante de la Niebla",
            " el Predestinado",
            " el Recién Pescado",
            " el Entusiasta del Curri",
            " el Sociable",
            " el Ermitaño",
            " el Travieso",
            " el Despreocupado",
            " el Nervioso",
            " el Ilusionado",
            " el Carismático",
            " el Sereno",
            " el Apasionado",
            " el Distraído",
            " el Feliz",
            " el Colérico",
            " el Sonriente",
            " el Llorón",
            " el Bienhumorado",
            " el Malhumorado",
            " el Intelectual",
            " el Impulsivo",
            " el Astuto",
            " el Amenazante",
            " el Amable",
            " el Aturullado",
            " el Motivado",
            " el Desidioso",
            " el Confiado",
            " el Inseguro",
            " el Humilde",
            " el Pretencioso",
            " el Vigoroso",
            " el Extenuado",
            " el Viajero del Pasado",
            " el Rutilante",
            " el Campeón de Paldea",
            " el Gigante",
            " el Diminuto",
            " el Recolector",
            " el Compañero Leal",
            " el Sibarita",
            " el Excepcional",
            " el Antiguo Alfa",
            " el Imbatible",
            " el Antiguo Dominante",
        ];

        public static readonly ushort[] ShinyLock = [  (ushort)Species.Victini, (ushort)Species.Keldeo, (ushort)Species.Volcanion, (ushort)Species.Cosmog, (ushort)Species.Cosmoem, (ushort)Species.Magearna, (ushort)Species.Marshadow, (ushort)Species.Eternatus,
                                                    (ushort)Species.Kubfu, (ushort)Species.Urshifu, (ushort)Species.Zarude, (ushort)Species.Glastrier, (ushort)Species.Spectrier, (ushort)Species.Calyrex ];

        protected PokeTradeTrainerInfo userInfo = default!;

        private TradeQueueInfo<T> queueInfo = default!;

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
            pk.CurrentFriendship = enc is IHatchCycle s ? s.EggCycles : pk.PersonalInfo.HatchCycles;

            Span<ushort> relearn = stackalloc ushort[4];
            la.GetSuggestedRelearnMoves(relearn, enc);
            pk.SetRelearnMoves(relearn);

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
                    markTitle = " el Imbatible";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkAlpha)
                {
                    result = RibbonIndex.MarkAlpha;
                    markTitle = " el Antiguo Alfa";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkTitan)
                {
                    result = RibbonIndex.MarkTitan;
                    markTitle = " el Antiguo Dominante";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkJumbo)
                {
                    result = RibbonIndex.MarkJumbo;
                    markTitle = " el Gigante";
                    return true;
                }
                else if (ribbonSetMark.RibbonMarkMini)
                {
                    result = RibbonIndex.MarkMini;
                    markTitle = " el Diminuto";
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

        public bool Check(T pkm, out string msg)
        {
            try
            {
                if (!pkm.CanBeTraded())
                {
                    msg = "Cancelando el intercambio, ¡el intercambio de este Pokémon está prohibido!";
                    return false;
                }
                if (pkm is T pk)
                {
                    var la = new LegalityAnalysis(pkm);
                    var valid = la.Valid;
                    if (valid)
                    {
                        msg = "Ya añadido a la cola de espera. Si seleccionas un Pokémon demasiado lentamente, ¡tu solicitud de entrega será cancelada!";
                        return true;
                    }
                    LogUtil.LogInfo($"Razón ilegal:\n{la.Report()}", nameof(TradeExtensions<T>));
                }
                LogUtil.LogInfo($"pkm type:{pkm.GetType()}, T:{typeof(T)}", nameof(TradeExtensions<T>));
                const string reason = "No puedo crear Pokémon ilegales.";
                msg = $"{reason}";
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeExtensions<T>));
                msg = "Cancelar entrega, ocurrió un error.";
            }
            return false;
        }

        public bool CheckAndGetPkm(string setstring, out string msg, out T outPkm)
        {
            outPkm = new T();
            if (!queueInfo.GetCanQueue())
            {
                msg = "¡Lo siento, no acepto solicitudes!";
                return false;
            }
            var set = ShowdownUtil.ConvertToShowdown(setstring);
            if (set == null)
            {
                msg = "La entrega se cancela y el apodo del Pokémon está vacío.";
                return false;
            }
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (template.Species < 1)
            {
                msg = "Para cancelar la entrega, utilice el código correcto del Showdown Set";
                return false;
            }
            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                GenerationFix(sav);
                var pkm = sav.GetLegal(template, out var result);
                if (string.Equals(pkm.Nickname, "egg", StringComparison.OrdinalIgnoreCase) && Breeding.CanHatchAsEgg(pkm.Species))
                {
                    EggTrade(pkm, template);
                }
                if (Check((T)pkm, out msg))
                {
                    outPkm = (T)pkm;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeExtensions<T>));
                msg = "Cancelar entrega, ocurrió un error.";
            }
            return false;
        }

        public bool CheckPkm(T pkm, out string msg)
        {
            if (!queueInfo.GetCanQueue())
            {
                msg = "¡Lo siento, no acepto solicitudes!";
                return false;
            }
            return Check(pkm, out msg);
        }

        public abstract IPokeTradeNotifier<T> GetPokeTradeNotifier(T pkm, int code);

        public abstract void SendMessage(string message);//完善此方法以实现发送消息功能

        //完善此方法以实现消息通知功能
        public void SetPokeTradeTrainerInfo(PokeTradeTrainerInfo pokeTradeTrainerInfo)
        {
            userInfo = pokeTradeTrainerInfo;
        }

        public void SetTradeQueueInfo(TradeQueueInfo<T> queueInfo)
        {
            this.queueInfo = queueInfo;
        }

        public void StartDump()
        {
            var code = queueInfo.GetRandomTradeCode(12345);
            var __ = AddToTradeQueue(new T(), code, false,
                PokeRoutineType.Dump, out string message);
            SendMessage(message);
        }

        public void StartTradeChinesePs(string chinesePs)
        {
            var ps = ShowdownTranslator<T>.Chinese2Showdown(chinesePs);
            LogUtil.LogInfo($"Código PS después de la conversión china:\n{ps}", nameof(TradeExtensions<T>));
            StartTradePs(ps);
        }

        public void StartTradeMultiChinesePs(string chinesePssString)
        {
            var chinesePsList = chinesePssString.Split('+').ToList();
            if (!JudgeMultiNum(chinesePsList.Count)) return;

            List<T> pkms = GetPKMsFromPsList(chinesePsList, true, out int invalidCount, out List<bool> skipAutoOTList);

            if (!JudgeInvalidCount(invalidCount, chinesePsList.Count)) return;

            var code = queueInfo.GetRandomTradeCode(12345);
            var __ = AddToTradeQueue(pkms, code, skipAutoOTList,
                PokeRoutineType.LinkTrade, out string message);
            SendMessage(message);
        }

        public void StartTradeMultiPKM(List<T> rawPkms)
        {
            if (!JudgeMultiNum(rawPkms.Count)) return;

            List<T> pkms = [];
            List<bool> skipAutoOTList = [];
            int invalidCount = 0;
            for (var i = 0; i < rawPkms.Count; i++)
            {
                var _ = CheckPkm(rawPkms[i], out var msg);
                if (!_)
                {
                    LogUtil.LogInfo($"Hay un problema con el {i + 1}º Pokémon del lote:{msg}", nameof(TradeExtensions<T>));
                    invalidCount++;
                }
                else
                {
                    LogUtil.LogInfo($"El lote {i + 1}: {GameInfo.GetStrings("en").Species[rawPkms[i].Species]}", nameof(TradeExtensions<T>));
                    skipAutoOTList.Add(false);
                    pkms.Add(rawPkms[i]);
                }
            }

            if (!JudgeInvalidCount(invalidCount, rawPkms.Count)) return;

            var code = queueInfo.GetRandomTradeCode(12345);
            var __ = AddToTradeQueue(pkms, code, skipAutoOTList,
                PokeRoutineType.LinkTrade, out string message);
            SendMessage(message);
        }

        public void StartTradeMultiPs(string pss)
        {
            var psList = pss.Split("\n\n").ToList();
            if (!JudgeMultiNum(psList.Count)) return;

            var pkms = GetPKMsFromPsList(psList, isChinesePS: false, out int invalidCount, out List<bool> skipAutoOTList);

            if (!JudgeInvalidCount(invalidCount, psList.Count)) return;

            var code = queueInfo.GetRandomTradeCode(12345);
            var __ = AddToTradeQueue(pkms, code, skipAutoOTList,
                PokeRoutineType.LinkTrade, out string message);
            SendMessage(message);
        }

        public void StartTradePKM(T pkm)
        {
            var _ = CheckPkm(pkm, out var msg);
            if (!_)
            {
                SendMessage(msg);
                return;
            }

            StartTradeWithoutCheck(pkm);
        }

        public void StartTradePs(string ps)
        {
            var _ = CheckAndGetPkm(ps, out var msg, out var pkm);
            if (!_)
            {
                SendMessage(msg);
                return;
            }
            var foreign = ps.Contains("Language: ");
            StartTradeWithoutCheck(pkm, foreign);
        }

        public void StartTradeWithoutCheck(T pkm, bool foreign = false)
        {
            var code = queueInfo.GetRandomTradeCode(12345);
            var __ = AddToTradeQueue(pkm, code, foreign,
                PokeRoutineType.LinkTrade, out string message);
            SendMessage(message);
        }

        private static int GenerateUniqueTradeID()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int randomValue = new Random().Next(1000);
            return ((int)(timestamp % int.MaxValue) * 1000) + randomValue;
        }

        private static void GenerationFix(ITrainerInfo sav)
        {
            if (typeof(T) == typeof(PK8) || typeof(T) == typeof(PB8) || typeof(T) == typeof(PA8)) sav.GetType().GetProperty("Generation")?.SetValue(sav, 8);
        }

        private bool AddToTradeQueue(T pk, int code, bool skipAutoOT,
                    PokeRoutineType type, out string msg)
        {
            return AddToTradeQueue([pk], code, [skipAutoOT], type, out msg);
        }

        private bool AddToTradeQueue(List<T> pks, int code, List<bool> skipAutoOTList,
                    PokeRoutineType type, out string msg)
        {
            if (pks == null || pks.Count == 0)
            {
                msg = "Los datos de Pokémon están vacíos.";
                return false;
            }
            T pk = pks.First();
            var trainer = userInfo;
            var notifier = GetPokeTradeNotifier(pk, code);
            var tt = type == PokeRoutineType.SeedCheck
                ? PokeTradeType.Seed
                : (type == PokeRoutineType.Dump ? PokeTradeType.Dump : PokeTradeType.Specific);
            var detail =
                new PokeTradeDetail<T>(pk, trainer, notifier, tt, code, true);
            detail.Context.Add("skipAutoOTList", skipAutoOTList);
            if (pks.Count > 0)
            {
                detail.Context.Add("batch", pks);
            }
            var uniqueTradeID = GenerateUniqueTradeID();
            var trade = new TradeEntry<T>(detail, userInfo.ID, type, userInfo.TrainerName, uniqueTradeID);

            var added = queueInfo.AddToTradeQueue(trade, userInfo.ID, false);

            if (added == QueueResultAdd.AlreadyInQueue)
            {
                msg = "Ya estás en la cola, por favor no vuelvas a enviar.";
                return false;
            }

            var position = queueInfo.CheckPosition(userInfo.ID, uniqueTradeID, type);

            //msg = $"@{name}: Added to the {type} queue, unique ID: {detail.ID}. Current Position: {position.Position}";
            msg = $"Agregado a la cola {type}, ID único: {detail.ID}. Posición actual: {position.Position}";

            var botct = queueInfo.Hub.Bots.Count;
            if (position.Position > botct)
            {
                var eta = queueInfo.Hub.Config.Queues.EstimateDelay(position.Position, botct);

                //msg += $". Estimated: {eta:F1} minutes.";
                msg += $". Estimado: {eta:F1} minutos.";
            }

            return true;
        }

        /// <summary>
        /// Generate the corresponding version of the PKM file based on the pokemon showdown code
        /// </summary>
        /// <param name="psList">ps code</param>
        /// <param name="isChinesePS">Is it Chinese ps</param>
        /// <param name="invalidCount">Number of illegal Pokémon</param>
        /// <param name="skipAutoOTList">List that needs to skip self-id</param>
        /// <returns></returns>
        private List<T> GetPKMsFromPsList(List<string> psList, bool isChinesePS, out int invalidCount, out List<bool> skipAutoOTList)
        {
            List<T> pkms = [];
            skipAutoOTList = [];
            invalidCount = 0;
            for (var i = 0; i < psList.Count; i++)
            {
                var ps = isChinesePS ? ShowdownTranslator<T>.Chinese2Showdown(psList[i]) : psList[i];
                var _ = CheckAndGetPkm(ps, out var msg, out var pkm);
                if (!_)
                {
                    LogUtil.LogInfo($"Hay un problema con el {i + 1}º Pokémon del lote:{msg}", nameof(TradeExtensions<T>));
                    invalidCount++;
                }
                else
                {
                    LogUtil.LogInfo($"Código PS después de la conversión al chino:\n{ps}", nameof(TradeExtensions<T>));
                    skipAutoOTList.Add(ps.Contains("Language: "));
                    pkms.Add(pkm);
                }
            }
            return pkms;
        }

        /// <summary>
        /// 判断无效数量
        /// </summary>
        /// <param name="invalidCount"></param>
        /// <param name="totalCount"></param>
        /// <returns></returns>
        private bool JudgeInvalidCount(int invalidCount, int totalCount)
        {
            if (invalidCount == totalCount)
            {
                SendMessage("Ninguno de ellos es legal, inténtalo de nuevo.");
                return false;
            }
            else if (invalidCount != 0)
            {
                SendMessage($"Entre los {totalCount} Pokémon que se espera intercambiar, {invalidCount} son ilegales. Solo se negociarán {totalCount - invalidCount} legales.");
            }
            return true;
        }

        /// <summary>
        /// 判断是否符合批量规则
        /// </summary>
        /// <param name="multiNum">待计算的数量</param>
        /// <returns></returns>
        private bool JudgeMultiNum(int multiNum)
        {
            var maxPkmsPerTrade = queueInfo.Hub.Config.Trade.TradeConfiguration.MaxPkmsPerTrade;
            if (maxPkmsPerTrade <= 1)
            {
                SendMessage("Comuníquese con el propietario del bot para cambiar la configuración de comercio/Pkms máximos por comercio a mayor que 1.");
                return false;
            }
            else if (multiNum > maxPkmsPerTrade)
            {
                SendMessage($"La cantidad de Pokémon intercambiados en lotes debe ser menor o igual a {maxPkmsPerTrade}.");
                return false;
            }
            return true;
        }
    }
}