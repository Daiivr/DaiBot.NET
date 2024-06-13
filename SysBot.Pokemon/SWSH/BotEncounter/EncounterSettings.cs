using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);

    private const string Encounter = nameof(Encounter);

    private const string Settings = nameof(Settings);

    private int _completedEggs;

    private int _completedFossils;

    private int _completedLegend;

    private int _completedWild;

    [Category(Counts), Description("Huevos recuperados")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), Description("Pokémon salvajes encontrados")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("Pokémon fósiles revividos")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("Pokémon legendarios encontrados")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Encounter), Description("Cuando esté habilitado, el bot continuará después de encontrar una coincidencia adecuada.")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Counts), Description("Cuando está habilitado, los recuentos se emitirán cuando se solicite una verificación de estado.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(Encounter), Description("El método utilizado por los Bots Line y Reset para encontrar Pokémon.")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Encounter), Description("Cuando está habilitado, la pantalla se apagará durante la operación normal del bucle del bot para ahorrar energía.")]
    public bool ScreenOff { get; set; }

    public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);

    public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"Encuentros salvajes: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"Encuentros legendarios: {CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"Huevos recibidos: {CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"Fósiles completados: {CompletedFossils}";
    }

    public override string ToString() => "Configuración SWSH del robot de encuentro";
}
