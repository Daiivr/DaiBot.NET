using PKHeX.Core;
using System;

namespace SysBot.Pokemon.Helpers.ShowdownHelpers
{
    public class GameInfoHelpers<T> where T : PKM, new()
    {
        public static GameStrings GetGameStrings()
        {
            if (typeof(T) == typeof(PK8))
                return GameInfo.GetStrings(GetLanguageIndex(GameVersion.SWSH));
            if (typeof(T) == typeof(PB8))
                return GameInfo.GetStrings(GetLanguageIndex(GameVersion.BDSP));
            if (typeof(T) == typeof(PA8))
                return GameInfo.GetStrings(GetLanguageIndex(GameVersion.PLA));
            if (typeof(T) == typeof(PK9))
                return GameInfo.GetStrings(GetLanguageIndex(GameVersion.SV));
            if (typeof(T) == typeof(PB7))
                return GameInfo.GetStrings(GetLanguageIndex(GameVersion.GE));

            throw new ArgumentException("El tipo no tiene cadenas de juego reconocidas.", typeof(T).Name);
        }

        public static IPersonalAbility12 GetPersonalInfo(ushort speciesIndex)
        {
            if (typeof(T) == typeof(PK8))
                return PersonalTable.SWSH.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PB8))
                return PersonalTable.BDSP.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PA8))
                return PersonalTable.LA.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PK9))
                return PersonalTable.SV.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PB7))
                return PersonalTable.GG.GetFormEntry(speciesIndex, 0);

            throw new ArgumentException("El tipo no tiene una tabla personal reconocida.", typeof(T).Name);
        }

        public static IPersonalFormInfo GetPersonalFormInfo(ushort speciesIndex)
        {
            if (typeof(T) == typeof(PK8))
                return PersonalTable.SWSH.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PB8))
                return PersonalTable.BDSP.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PA8))
                return PersonalTable.LA.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PK9))
                return PersonalTable.SV.GetFormEntry(speciesIndex, 0);
            if (typeof(T) == typeof(PB7))
                return PersonalTable.GG.GetFormEntry(speciesIndex, 0);

            throw new ArgumentException("El tipo no tiene una tabla de formulario personal reconocida.", typeof(T).Name);
        }

        public static EntityContext GetGeneration()
        {
            if (typeof(T) == typeof(PK8))
                return EntityContext.Gen8;
            if (typeof(T) == typeof(PB8))
                return EntityContext.Gen8b;
            if (typeof(T) == typeof(PA8))
                return EntityContext.Gen8a;
            if (typeof(T) == typeof(PK9))
                return EntityContext.Gen9;
            if (typeof(T) == typeof(PB7))
                return EntityContext.Gen7b;

            throw new ArgumentException("El tipo no tiene una generación reconocida.", typeof(T).Name);
        }

        public static int GetLanguageIndex(GameVersion version)
        {
            const string language = GameLanguage.DefaultLanguage;
            return GameLanguage.GetLanguageIndex(language);
        }

        public static ILearnSource GetLearnSource(PKM pk)
        {
            if (pk is PK9)
                return LearnSource9SV.Instance;
            if (pk is PB8)
                return LearnSource8BDSP.Instance;
            if (pk is PA8)
                return LearnSource8LA.Instance;
            if (pk is PK8)
                return LearnSource8SWSH.Instance;
            if (pk is PB7)
                return LearnSource7GG.Instance;
            throw new ArgumentException("Tipo PKM no admitido.", nameof(pk));
        }
    }
}
