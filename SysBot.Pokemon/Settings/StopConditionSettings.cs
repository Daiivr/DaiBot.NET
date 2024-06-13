using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class StopConditionSettings
{
    private const string StopConditions = nameof(StopConditions);

    [Category(StopConditions), Description("Mantiene presionado el botón Capturar para grabar un clip de 30 segundos cuando Encounter Bot o Fossilbot encuentra un Pokémon coincidente.")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("Tiempo extra en milisegundos para esperar después de que un encuentro coincida antes de presionar Capturar para Encounter Bot o Fossilbot.")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("Detente sólo en Pokémon que tengan una marca.")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), Description("Si no está vacía, la cadena proporcionada se antepondrá al mensaje de registro de resultados encontrados para las alertas de Echo para quien usted especifique. Para Discord, use <@número de ID de usuario> para mencionar.")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions), Description("Si se establece en VERDADERO, coincide con la configuración de Shiny Target y Target IVs. De lo contrario, busca coincidencias entre Shiny Target o Target IV.")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), Description("Selecciona el tipo brillante en el que detenerse.")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), Description("Solo se detiene en Pokémon con este ID de formulario. No hay restricciones si se deja en blanco.")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), Description("Se detiene solo en Pokémon de esta especie. No hay restricciones si se configura en \"Ninguno\".")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), Description("IV máximos aceptados en el formato HP/Atk/Def/Sp A/Sp D/Spe. Utilice \"x\" para IV no marcados y \"/\" como separador.")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopConditions), Description("IV mínimos aceptados en el formato HP/Atk/Def/Sp A/Sp D/Spe. Utilice \"x\" para IV no marcados y \"/\" como separador.")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopConditions), Description("Detente sólo en Pokémon de la naturaleza especificada.")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopConditions), Description("Lista de marcas a ignorar separadas por comas. Utilice el nombre completo, p. \"Marca poco común, Marca del amanecer, Marca orgullosa\".")]
    public string UnwantedMarks { get; set; } = "";

    public static bool EncounterFound<T>(T pk, int[] targetminIVs, int[] targetmaxIVs, StopConditionSettings settings, IReadOnlyList<string>? marklist) where T : PKM
    {
        // Match Nature and Species if they were specified.
        if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
            return false;

        if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
            return false;

        if (settings.TargetNature != Nature.Random && settings.TargetNature != (Nature)pk.Nature)
            return false;

        // Return if it doesn't have a mark, or it has an unwanted mark.
        var unmarked = pk is IRibbonIndex m && !HasMark(m);
        var unwanted = marklist is not null && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), marklist);
        if (settings.MarkOnly && (unmarked || unwanted))
            return false;

        if (settings.ShinyTarget != TargetShinyType.DisableOption)
        {
            bool shinymatch = settings.ShinyTarget switch
            {
                TargetShinyType.AnyShiny => pk.IsShiny,
                TargetShinyType.NonShiny => !pk.IsShiny,
                TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                TargetShinyType.DisableOption => true,
                _ => throw new ArgumentException(nameof(TargetShinyType)),
            };

            // If we only needed to match one of the criteria and it shiny match'd, return true.
            // If we needed to match both criteria, and it didn't shiny match, return false.
            if (!settings.MatchShinyAndIV && shinymatch)
                return true;
            if (settings.MatchShinyAndIV && !shinymatch)
                return false;
        }

        // Reorder the speed to be last.
        Span<int> pkIVList = stackalloc int[6];
        pk.GetIVs(pkIVList);
        (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);

        for (int i = 0; i < 6; i++)
        {
            if (targetminIVs[i] > pkIVList[i] || targetmaxIVs[i] < pkIVList[i])
                return false;
        }
        return true;
    }

    public static string GetMarkName(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return RibbonStrings.GetName($"Ribbon{mark}");
        }
        return "";
    }

    public static string GetPrintName(PKM pk)
    {
        var set = ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetMarkName(r);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon encontrados con **{GetMarkName(r)}**!";
        }
        return set;
    }

    public static void InitializeTargetIVs(PokeTradeHubConfig config, out int[] min, out int[] max)
    {
        min = ReadTargetIVs(config.StopConditions, true);
        max = ReadTargetIVs(config.StopConditions, false);
    }

    public static void ReadUnwantedMarks(StopConditionSettings settings, out IReadOnlyList<string> marks) =>
        marks = settings.UnwantedMarks.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) => marklist.Contains(mark);

    public override string ToString() => "Configuración de condición de parada";

    private static bool HasMark(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return true;
        }
        return false;
    }

    private static int[] ReadTargetIVs(StopConditionSettings settings, bool min)
    {
        int[] targetIVs = new int[6];
        char[] split = ['/'];

        string[] splitIVs = min
            ? settings.TargetMinIVs.Split(split, StringSplitOptions.RemoveEmptyEntries)
            : settings.TargetMaxIVs.Split(split, StringSplitOptions.RemoveEmptyEntries);

        // Only accept up to 6 values.  Fill it in with default values if they don't provide 6.
        // Anything that isn't an integer will be a wild card.
        for (int i = 0; i < 6; i++)
        {
            if (i < splitIVs.Length)
            {
                var str = splitIVs[i];
                if (int.TryParse(str, out var val))
                {
                    targetIVs[i] = val;
                    continue;
                }
            }
            targetIVs[i] = min ? 0 : 31;
        }
        return targetIVs;
    }
}

public enum TargetShinyType
{
    DisableOption,  // Doesn't care

    NonShiny,       // Match nonshiny only

    AnyShiny,       // Match any shiny regardless of type

    StarOnly,       // Match star shiny only

    SquareOnly,     // Match square shiny only
}
