using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Console agnostic settings
/// </summary>
public abstract class BaseConfig
{
    protected const string FeatureToggle = nameof(FeatureToggle);
    protected const string Operation = nameof(Operation);
    [Browsable(false)]
    private const string Debug = nameof(Debug);

    [Category(FeatureToggle), Description("Cuando está habilitado, el bot presionará el botón B ocasionalmente cuando no esté procesando nada (para evitar suspenderse)."), DisplayName("Modo Anti Suspenso")]
    public bool AntiIdle { get; set; }

    [Category(FeatureToggle), Description("Cuando esté habilitado, el bot ingresará el código comercial del trade a través del teclado (más rápido)."), DisplayName("Usar Teclado?")]
    public bool UseKeyboard { get; set; } = true;

    [Category(FeatureToggle), Description("Habilita registros de texto. Reinicie para aplicar los cambios."), DisplayName("Habilitar registros?")]
    public bool LoggingEnabled { get; set; } = true;

    [Category(FeatureToggle), Description("Número máximo de archivos de registro de texto antiguos que se conservarán. Establezca esto en <= 0 para deshabilitar la limpieza de registros. Reinicie para aplicar los cambios."), DisplayName("Maximo de Archivos de Registro")]
    public int MaxArchiveFiles { get; set; } = 14;

    [Browsable(false)]
    [Category(Debug), Description("Skips creating bots when the program is started; helpful for testing integrations.")]
    public bool SkipConsoleBotCreation { get; set; }

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public LegalitySettings Legality { get; set; } = new();

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FolderSettings Folder { get; set; } = new();

    public abstract bool Shuffled { get; }
}
