using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static SysBot.Pokemon.TradeSettings;
using static MovesTranslationDictionary;

namespace SysBot.Pokemon.Discord;

public static class DetailsExtractor<T> where T : PKM, new()
{
    public static void AddAdditionalText(EmbedBuilder embedBuilder)
    {
        string additionalText = string.Join("\n", SysCordSettings.Settings.AdditionalEmbedText);
        if (!string.IsNullOrEmpty(additionalText))
        {
            embedBuilder.AddField("\u200B", additionalText, inline: false);
        }
    }

    public static void AddNormalTradeFields(EmbedBuilder embedBuilder, EmbedData embedData, string trainerMention, T pk)
    {
        string leftSideContent = (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowLevel ? $"**Nivel:** {embedData.Level}\n" : "");
        leftSideContent +=
            (pk.Version is GameVersion.SL or GameVersion.VL && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowTeraType ? $"**Tera Tipo:** {embedData.TeraType}\n" : "") +
                        (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowAbility ? $"**Habilidad:** {embedData.Ability}\n" : "") +
            (pk.Version is GameVersion.SL or GameVersion.VL && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowScale ? $"**Tamaño:** {embedData.Scale.Item1}\n" : "") +
                        (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowNature ? $"**Naturaleza:** {embedData.Nature}\n" : "") +
            (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowMetDate ? $"{embedData.MetDate}\n" : "") +
            (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowIVs ? $"**IVs**: {embedData.IVsDisplay}\n" : "") +
            (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShowEVs && !string.IsNullOrWhiteSpace(embedData.EVsDisplay) ? $"**EVs**: {embedData.EVsDisplay}\n" : "");
        leftSideContent += $"\n{trainerMention}\nAgregado a la cola de tradeo.";

        leftSideContent = leftSideContent.TrimEnd('\n');
        string shinySymbol = GetShinySymbol(pk);
        embedBuilder.AddField($"**{shinySymbol}{embedData.SpeciesName}{(string.IsNullOrEmpty(embedData.FormName) ? "" : $"-{embedData.FormName}")} {embedData.SpecialSymbols}**", leftSideContent, inline: true);
        embedBuilder.AddField("\u200B", "\u200B", inline: true); // Spacer
        embedBuilder.AddField("**Movimientos:**", embedData.MovesDisplay, inline: true);
    }

    public static void AddSpecialTradeFields(EmbedBuilder embedBuilder, bool isMysteryEgg, bool isSpecialRequest, bool isCloneRequest, bool isFixOTRequest, string trainerMention)
    {
        string specialDescription = $"**Entrenador:** {trainerMention}\n" +
                                    (isMysteryEgg ? "Huevo Misterioso" : isSpecialRequest ? "Solicitud Especial" : isCloneRequest ? "Solicitud de clonación" : isFixOTRequest ? "FixOT Request" : "Solicitud de Dump");
        embedBuilder.AddField("\u200B", specialDescription, inline: false);
    }

    public static void AddThumbnails(EmbedBuilder embedBuilder, bool isCloneRequest, bool isSpecialRequest, bool isDumpRequest, bool isFixOTRequest, string heldItemUrl, T pk, PokeTradeType tradeType)
    {
        if (isCloneRequest || isSpecialRequest || isDumpRequest || isFixOTRequest)
        {
            embedBuilder.WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png");
        }
        else if (tradeType == PokeTradeType.Item)
        {
            // Usa la imagen del Pokémon como thumbnail cuando el tipo de intercambio es 'Item'
            var speciesImageUrl = TradeExtensions<T>.PokeImg(pk, false, true, null); // Asume que tienes acceso a 'pk' aquí
            embedBuilder.WithThumbnailUrl(speciesImageUrl);
        }
        else if (!string.IsNullOrEmpty(heldItemUrl))
        {
            embedBuilder.WithThumbnailUrl(heldItemUrl);
        }
    }

    public static EmbedData ExtractPokemonDetails(T pk, SocketUser user, bool isMysteryEgg, bool isCloneRequest, bool isDumpRequest, bool isFixOTRequest, bool isSpecialRequest, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades, PokeTradeType type)
    {
        var strings = GameInfo.GetStrings(1);
        var embedData = new EmbedData
        {
            // Basic Pokémon details
            Moves = GetMoveNames(pk),
            Level = pk.CurrentLevel
        };

        // Pokémon appearance and type details
        if (pk is PK9 pk9)
        {
            embedData.TeraType = GetTeraTypeString(pk9);
            embedData.Scale = GetScaleDetails(pk9);
        }

        // Pokémon identity and special attributes
        embedData.Ability = GetTranslatedAbilityName(pk);
        embedData.Nature = GetTranslatedNatureName(pk);
        embedData.SpeciesName = GameInfo.GetStrings(1).Species[pk.Species];
        embedData.SpecialSymbols = GetSpecialSymbols(pk);
        embedData.FormName = ShowdownParsing.GetStringFromForm(pk.Form, strings, pk.Species, pk.Context);
        embedData.HeldItem = strings.itemlist[pk.HeldItem];
        embedData.Ball = strings.balllist[pk.Ball];

        // Display elements
        int[] ivs = pk.IVs;
        string ivsDisplay;
        if (ivs.All(iv => iv == 31))
        {
            ivsDisplay = "Máximos";
        }
        else
        {
            ivsDisplay = string.Join("/", [
                ivs[0].ToString(),
                ivs[1].ToString(),
                ivs[2].ToString(),
                ivs[4].ToString(),
                ivs[5].ToString(),
                ivs[3].ToString()
            ]);
        }
        embedData.IVsDisplay = ivsDisplay;

        int[] evs = GetEVs(pk);
        embedData.EVsDisplay = string.Join(" / ", new[] {
            (evs[0] != 0 ? $"{evs[0]} HP" : ""),
            (evs[1] != 0 ? $"{evs[1]} Atk" : ""),
            (evs[2] != 0 ? $"{evs[2]} Def" : ""),
            (evs[4] != 0 ? $"{evs[4]} SpA" : ""),
            (evs[5] != 0 ? $"{evs[5]} SpD" : ""),
            (evs[3] != 0 ? $"{evs[3]} Spe" : "") // correct pkhex/ALM ordering of stats
        }.Where(s => !string.IsNullOrEmpty(s)));
        if (pk.FatefulEncounter)
        {
            embedData.MetDate = "**Obtenido:** " + pk.MetDate.ToString();
        }
        else
        {
            embedData.MetDate = "**Atrapado:** " + pk.MetDate.ToString();
        }
        embedData.MovesDisplay = string.Join("\n", embedData.Moves);
        embedData.PokemonDisplayName = pk.IsNicknamed ? pk.Nickname : embedData.SpeciesName;

        // Trade title
        embedData.TradeTitle = GetTradeTitle(isMysteryEgg, isCloneRequest, isDumpRequest, isFixOTRequest, isSpecialRequest, isBatchTrade, batchTradeNumber, embedData.PokemonDisplayName, pk.IsShiny);

        // Author name
#pragma warning disable CS8604 // Possible null reference argument.
        embedData.AuthorName = GetAuthorName(user.Username, user.GlobalName, embedData.TradeTitle, isMysteryEgg, isFixOTRequest, isCloneRequest, isDumpRequest, isSpecialRequest, isBatchTrade, embedData.NickDisplay, pk.IsShiny, type);
#pragma warning restore CS8604 // Possible null reference argument.

        return embedData;
    }

    public static string GetUserDetails(int totalTradeCount, TradeCodeStorage.TradeCodeDetails? tradeDetails)
    {
        string userDetailsText = "";
        if (totalTradeCount > 0)
        {
            userDetailsText = $"Trades: {totalTradeCount}";
        }
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes && tradeDetails != null)
        {
            if (!string.IsNullOrEmpty(tradeDetails?.OT))
            {
                userDetailsText += $" | OT: {tradeDetails?.OT}";
            }
            if (tradeDetails?.TID != null)
            {
                userDetailsText += $" | SID: {tradeDetails?.SID}";
            }
            if (tradeDetails?.TID != null)
            {
                userDetailsText += $" | TID: {tradeDetails?.TID}";
            }
        }
        return userDetailsText;
    }

    private static string GetAuthorName(string username, string globalname, string tradeTitle, bool isMysteryEgg, bool isFixOTRequest, bool isCloneRequest, bool isDumpRequest, bool isSpecialRequest, bool isBatchTrade, string NickDisplay, bool isShiny, PokeTradeType tradeType)
    {
        string userName = string.IsNullOrEmpty(globalname) ? username : globalname;
        string isPkmShiny = isShiny ? " Shiny" : "";

        // Agregar manejo para el caso de PokeTradeType es Item
        if (tradeType == PokeTradeType.Item)
        {
            return $"Item solicitado por {userName}";
        }

        if (isMysteryEgg || isFixOTRequest || isCloneRequest || isDumpRequest || isSpecialRequest || isBatchTrade)
        {
            return $"{tradeTitle} {username}";
        }
        else
        {
            // Verifica si el Pokémon tiene un apodo para usarlo, de lo contrario mantiene el formato estándar.
            return !string.IsNullOrEmpty(NickDisplay) ?
                   $"{NickDisplay} solicitado por {userName}" :
                   $"Pokémon{isPkmShiny} solicitado por {userName}";
        }
    }

    private static int[] GetEVs(T pk)
    {
        int[] evs = new int[6];
        pk.GetEVs(evs);
        return evs;
    }

    private static List<string> GetMoveNames(T pk)
    {
        ushort[] moves = new ushort[4];
        pk.GetMoves(moves.AsSpan());
        List<int> movePPs = [pk.Move1_PP, pk.Move2_PP, pk.Move3_PP, pk.Move4_PP];
        var moveNames = new List<string>();

        var typeEmojis = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.CustomTypeEmojis
            .Where(e => !string.IsNullOrEmpty(e.EmojiCode))
            .ToDictionary(e => (PKHeX.Core.MoveType)e.MoveType, e => $"{e.EmojiCode}");

        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == 0) continue;
            string moveName = GameInfo.MoveDataSource.FirstOrDefault(m => m.Value == moves[i])?.Text ?? "";
            string translatedMoveName = MovesTranslation.ContainsKey(moveName) ? MovesTranslation[moveName] : moveName;
            byte moveTypeId = MoveInfo.GetType(moves[i], default);
            PKHeX.Core.MoveType moveType = (PKHeX.Core.MoveType)moveTypeId;
            string formattedMove = $"{translatedMoveName}";
            if (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MoveTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
            {
                formattedMove = $"{moveEmoji} {formattedMove}";
            }
            moveNames.Add($"\u200B{formattedMove}");
        }

        return moveNames;
    }

    private static string GetTranslatedAbilityName(T pk)
    {
        string abilityName = GameInfo.AbilityDataSource.FirstOrDefault(a => a.Value == pk.Ability)?.Text ?? "";
        return AbilityTranslationDictionary.AbilityTranslation.TryGetValue(abilityName, out var translatedName) ? translatedName : abilityName;
    }

    private static string GetTranslatedNatureName(T pk)
    {
        string natureName = GameInfo.NatureDataSource.FirstOrDefault(n => n.Value == (int)pk.Nature)?.Text ?? "";
        return NatureTranslations.TraduccionesNaturalezas.TryGetValue(natureName, out var translatedName) ? translatedName : natureName;
    }

    private static (string, byte) GetScaleDetails(PK9 pk9)
    {
        string scaleText = $"{PokeSizeDetailedUtil.GetSizeRating(pk9.Scale)}";
        byte scaleNumber = pk9.Scale;

        // Formato inicial que incluye siempre el número de escala
        string scaleTextWithNumber = $"{scaleText} ({scaleNumber})";

        // Aplica los emojis si están habilitados
        if (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseScaleEmojis)
        {
            if (scaleText == "XXXS")
            {
                scaleTextWithNumber = $"{SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ScaleEmojis.ScaleXXXSEmoji} {scaleTextWithNumber}";
            }
            else if (scaleText == "XXXL")
            {
                scaleTextWithNumber = $"{SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ScaleEmojis.ScaleXXXLEmoji} {scaleTextWithNumber}";
            }
        }

        // Retorna el texto completo de la escala y el número de escala como un tuple
        return (scaleTextWithNumber, scaleNumber);
    }

    private static string GetShinySymbol(T pk)
    {
        var shinySettings = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ShinyEmojis;

        if (pk.ShinyXor == 0)
        {
            string shinySquareEmoji = string.IsNullOrEmpty(shinySettings.ShinySquareEmoji.EmojiString) ? "◼ " : shinySettings.ShinySquareEmoji.EmojiString + " ";
            return shinySquareEmoji;
        }
        else if (pk.IsShiny)
        {
            string shinyNormalEmoji = string.IsNullOrEmpty(shinySettings.ShinyNormalEmoji.EmojiString) ? "★ " : shinySettings.ShinyNormalEmoji.EmojiString + " ";
            return shinyNormalEmoji;
        }
        return string.Empty;
    }

    private static string GetSpecialSymbols(T pk)
    {
        string alphaMarkSymbol = string.Empty;
        string mightyMarkSymbol = string.Empty;
        string markTitle = string.Empty;
        if (pk is IRibbonSetMark9 ribbonSetMark)
        {
            alphaMarkSymbol = ribbonSetMark.RibbonMarkAlpha ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.SpecialMarksEmojis.AlphaMarkEmoji.EmojiString : string.Empty;
            mightyMarkSymbol = ribbonSetMark.RibbonMarkMightiest ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.SpecialMarksEmojis.MightiestMarkEmoji.EmojiString : string.Empty;
        }
        if (pk is IRibbonIndex ribbonIndex)
        {
            TradeExtensions<T>.HasMark(ribbonIndex, out RibbonIndex result, out markTitle);
        }
        string alphaSymbol = (pk is IAlpha alpha && alpha.IsAlpha) ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.SpecialMarksEmojis.AlphaPLAEmoji.EmojiString : string.Empty;
        string genderSymbol = GameInfo.GenderSymbolASCII[pk.Gender];
        string maleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.GenderEmojis.MaleEmoji.EmojiString;
        string femaleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.GenderEmojis.FemaleEmoji.EmojiString;
        string displayGender = genderSymbol switch
        {
            "M" => !string.IsNullOrEmpty(maleEmojiString) ? maleEmojiString : "(M) ",
            "F" => !string.IsNullOrEmpty(femaleEmojiString) ? femaleEmojiString : "(F) ",
            _ => ""
        };
        string mysteryGiftEmoji = pk.FatefulEncounter ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.SpecialMarksEmojis.MysteryGiftEmoji.EmojiString : "";

        return (!string.IsNullOrEmpty(markTitle) ? $"{markTitle} " : "") + displayGender + alphaSymbol + mightyMarkSymbol + alphaMarkSymbol + mysteryGiftEmoji;
    }

    private static string GetTeraTypeString(PK9 pk9)
    {
        string teraTypeEmoji = "";
        string teraTypeName = pk9.TeraType.ToString();  // Obtiene el nombre del tipo Tera

        if (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseTeraEmojis)
        {
            var teraType = pk9.TeraTypeOverride == (PKHeX.Core.MoveType)TeraTypeUtil.Stellar || (int)pk9.TeraType == 99 ? TradeSettings.MoveType.Stellar : (TradeSettings.MoveType)pk9.TeraType;
            var emojiInfo = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.TeraTypeEmojis.Find(e => e.MoveType == teraType);
            if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.EmojiCode))
            {
                teraTypeEmoji = emojiInfo.EmojiCode + " ";  // Asegúrate de agregar un espacio para separar el emoji del nombre
            }
        }

        // Utiliza el diccionario de traducciones para obtener la cadena traducida del tipo Tera
        if (TeraTypeDictionaries.TeraTranslations.TryGetValue(teraTypeName, out var translatedType))
        {
            teraTypeName = translatedType;
        }

        return teraTypeEmoji + teraTypeName;  // Combina el emoji y el nombre del tipo Tera
    }

    private static string GetTradeTitle(bool isMysteryEgg, bool isCloneRequest, bool isDumpRequest, bool isFixOTRequest, bool isSpecialRequest, bool isBatchTrade, int batchTradeNumber, string pokemonDisplayName, bool isShiny)
    {
        string shinyEmoji = isShiny ? "✨ " : "";
        return isMysteryEgg ? "✨ Huevo Misterioso Shiny ✨ de" :
               isBatchTrade ? $"Comercio por lotes #{batchTradeNumber} - {shinyEmoji}{pokemonDisplayName} de" :
               isFixOTRequest ? "Solicitud de FixOT de" :
               isSpecialRequest ? "Solicitud Especial de" :
               isCloneRequest ? "Capsula de Clonación activada para" :
               isDumpRequest ? "Solicitud de Dump de" :
               "";
    }
}

public class EmbedData
{
    public string? Ability { get; set; }

    public string? AuthorName { get; set; }

    public string? Ball { get; set; }

    public string? EmbedImageUrl { get; set; }

    public string? EVsDisplay { get; set; }

    public string? FormName { get; set; }

    public string? HeldItem { get; set; }

    public string? HeldItemUrl { get; set; }

    public bool IsLocalFile { get; set; }

    public string? IVsDisplay { get; set; }

    public int Level { get; set; }

    public string? MetDate { get; set; }

    public List<string>? Moves { get; set; }

    public string? MovesDisplay { get; set; }

    public string? Nature { get; set; }

    public string? PokemonDisplayName { get; set; }

    public string? NickDisplay { get; set; }

    public (string, byte) Scale { get; set; }

    public string? SpecialSymbols { get; set; }

    public string? SpeciesName { get; set; }

    public string? TeraType { get; set; }

    public string? TradeTitle { get; set; }
}
