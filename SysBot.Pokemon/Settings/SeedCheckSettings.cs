using System.ComponentModel;

namespace SysBot.Pokemon;

public class SeedCheckSettings
{
    private const string FeatureToggle = nameof(FeatureToggle);

    [Category(FeatureToggle), Description("Permite devolver solo el cuadro brillante más cercano, los primeros cuadros brillantes de estrella y cuadrados, o los primeros tres cuadros brillantes.")]
    public SeedCheckResults ResultDisplayMode { get; set; }

    [Category(FeatureToggle), Description("Cuando está habilitada, las comprobaciones de semillas devolverán todos los resultados posibles en lugar de la primera coincidencia válida.")]
    public bool ShowAllZ3Results { get; set; }

    public override string ToString() => "Configuración de verificación de semillas";
}

public enum SeedCheckResults
{
    ClosestOnly,            // Only gets the first shiny

    FirstStarAndSquare,     // Gets the first star shiny and first square shiny

    FirstThree,             // Gets the first three frames
}
