using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettings
{
    private const string Counts = nameof(Counts);

    private const string Fossil = nameof(Fossil);

    /// <summary>
    /// Toggle for injecting fossil pieces.
    /// </summary>
    [Category(Fossil), Description("Alternar para inyectar piezas fósiles.")]
    public bool InjectWhenEmpty { get; set; }

    [Category(Fossil), Description("Especies de Pokémon fósiles para cazar.")]
    public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

    public override string ToString() => "Configuración del Bot fósil";
}
