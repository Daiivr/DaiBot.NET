using System.ComponentModel;
using System.IO;

namespace SysBot.Pokemon;

public class FolderSettings : IDumper
{
    private const string FeatureToggle = nameof(FeatureToggle);
    private const string Files = nameof(Files);
    public override string ToString() => "Configuración de carpeta/dump";

    [Category(FeatureToggle), Description("Cuando está habilitado, vuelca todos los archivos PKM recibidos (resultados comerciales) en la carpeta de volcado."), DisplayName("Habilitar el Volcado de Archivos (Dump)")]
    public bool Dump { get; set; }

    [Category(Files), Description("Carpeta de origen: desde donde se seleccionan los archivos PKM a distribuir."), DisplayName("Carpeta de Distribución")]
    public string DistributeFolder { get; set; } = string.Empty;

    [Category(Files), Description("Carpeta de destino: donde se descargan todos los archivos PKM recibidos."), DisplayName("Carpeta de Volcado (Dump)")]
    public string DumpFolder { get; set; } = string.Empty;

    public void CreateDefaults(string path)
    {
        var dump = Path.Combine(path, "dump");
        Directory.CreateDirectory(dump);
        DumpFolder = dump;
        Dump = true;

        var distribute = Path.Combine(path, "distribute");
        Directory.CreateDirectory(distribute);
        DistributeFolder = distribute;
    }
}
