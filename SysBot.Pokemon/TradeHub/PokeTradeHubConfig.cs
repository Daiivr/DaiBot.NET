using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public sealed class PokeTradeHubConfig : BaseConfig
{
    [Browsable(false)]
    private const string BotEncounter = nameof(BotEncounter);

    private const string BotTrade = nameof(BotTrade);

    private const string Integration = nameof(Integration);

    [Browsable(false)]
    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public BilibiliSettings Bilibili { get; set; } = new();

    [Category(BotTrade), Description("Nombre del Bot de Discord que está ejecutando el programa. Esto titulará la ventana para facilitar el reconocimiento. Requiere reinicio del programa."), DisplayName("Nombre del Bot")]
    public string BotName { get; set; } = string.Empty;

    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DiscordSettings Discord { get; set; } = new();

    [Category(BotTrade), Description("Configuraciones para operaciones de distribución inactivas.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DistributionSettings Distribution { get; set; } = new();

    [Browsable(false)]
    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DodoSettings Dodo { get; set; } = new();

    // Encounter Bots - For finding or hosting Pokémon in-game.
    [Browsable(false)]
    [Category(BotEncounter)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public EncounterSettings EncounterSWSH { get; set; } = new();

    [Category(Integration), Description("Permite a los usuarios favorecidos unirse a la cola con una posición más favorable que los usuarios normales.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FavoredPrioritySettings Favoritism { get; set; } = new();

    [Browsable(false)]
    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public QQSettings QQ { get; set; } = new();

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public QueueSettings Queues { get; set; } = new();

    [Browsable(false)]
    [Category(BotEncounter)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public RaidSettings RaidSWSH { get; set; } = new();

    [Browsable(false)]
    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public SeedCheckSettings SeedCheckSWSH { get; set; } = new();

    [Browsable(false)]
    public override bool Shuffled => Distribution.Shuffled;

    [Browsable(false)]
    [Category(BotEncounter), Description("Condiciones de parada para Encounter Bot.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StopConditionSettings StopConditions { get; set; } = new();

    [Category(Integration), Description("Configurar la generación de activos para streaming.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public StreamSettings Stream { get; set; } = new();

    [Browsable(false)]
    [Category(Integration), Description("Elección de opción de tema de usuarios.")]
    public string ThemeOption { get; set; } = string.Empty;

    [Category(Operation), Description("Agregue tiempo adicional para Switches más lentos.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TimingSettings Timings { get; set; } = new();

    // Trade Bots

    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeSettings Trade { get; set; } = new();

    [Category(BotTrade)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TradeAbuseSettings TradeAbuse { get; set; } = new();

    // Integration
    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public TwitchSettings Twitch { get; set; } = new();

    [Category(Integration)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public YouTubeSettings YouTube { get; set; } = new();
}
