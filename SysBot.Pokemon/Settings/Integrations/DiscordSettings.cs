using System.ComponentModel;
using System;
using System.Collections.Generic;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon;

public enum StreamIconOption
{
    Twitch,
    Youtube,
    Facebook,
    Kick
}

public class DiscordSettings
{
    private const string Startup = nameof(Startup);
    private const string Operation = nameof(Operation);
    private const string Channels = nameof(Channels);
    private const string Roles = nameof(Roles);
    private const string Users = nameof(Users);
    private const string Servers = nameof(Servers);

    public override string ToString() => "Configuración de integración de Discord";

    // Startup

    [Category(Startup), Description("Token de inicio de sesión del bot.")]
    public string Token { get; set; } = string.Empty;

    [Category(Startup), Description("Prefijo de comando del bot.")]
    public string CommandPrefix { get; set; } = "$";

    [Category(Startup), Description("Enviará un estado embed para cuando el bot este online/offline a todos los canales incluidos en la lista blanca.")]
    public bool BotEmbedStatus { get; set; } = true;

    [Category(Startup), Description("Configuraciones relacionadas con el estado del canal.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public ChannelStatusSettings ChannelStatusConfig { get; set; } = new ChannelStatusSettings();

    public class ChannelStatusSettings
    {
        public override string ToString() => "Configuraciones relacionadas con el estado del canal.";

        [Description("Añadirá emoji online/offline al nombre del canal en función de su estado actual. Solo canales en lista blanca."), DisplayName("Activar el estado del canal")]
        public bool EnableChannelStatus { get; set; } = false;

        [Description("Emoji personalizado para usar cuando el bot está online.")]
        public string OnlineEmoji { get; set; } = "✅";

        [Description("Emoji personalizado para usar cuando el bot está offline.")]
        public string OfflineEmoji { get; set; } = "❌";
    }

    [Category(Startup), Description("Lista de módulos que no se cargarán cuando se inicie el bot (separados por comas).")]
    public string ModuleBlacklist { get; set; } = string.Empty;

    [Category(Startup), Description("Alternar para manejar comandos de forma asincrónica o sincrónica.")]
    public bool AsyncCommands { get; set; }

    [Category(Startup), Description("Estado personalizado del bot.")]
    public string BotGameStatus { get; set; } = "SysBot.NET: Pokémon";

    [Category(Startup), Description("Indica el color del estado de presencia de Discord solo considerando los bots que son de tipo Trade.")]
    public bool BotColorStatusTradeOnly { get; set; } = true;

    [Category(Operation), Description("Texto adicional para agregar al comienzo del Embed.")]
    public string[] AdditionalEmbedText { get; set; } = Array.Empty<string>();

    [Category(Operation), Description("Mensaje personalizado con el que el bot responderá cuando un usuario lo salude. Utilice formato de cadena para mencionar al usuario en la respuesta.")]
    public string HelloResponse { get; set; } = "Hi {0}!";

    [Category(Operation), Description("Enlace de transmisión.")]
    public string StreamLink { get; set; } = string.Empty;

    [Category(Operation), Description("Opción de icono para la transmisión.")]
    public StreamIconOption StreamIcon { get; set; } = StreamIconOption.Twitch;

    // URLs for the stream icons
    public static readonly Dictionary<StreamIconOption, string> StreamIconUrls = new()
        {
            { StreamIconOption.Twitch, "https://i.imgur.com/zD95Rzy.png" },
            { StreamIconOption.Youtube, "https://i.imgur.com/VzFGPdo.png" },
            { StreamIconOption.Facebook, "https://i.imgur.com/YYkD2fe.png" },
            { StreamIconOption.Kick, "https://i.imgur.com/HH8AAJY.jpg" }
        };

    [Category(Operation), Description("Enlace de donación.")]
    public string DonationLink { get; set; } = string.Empty;

    // Whitelists

    [Category(Roles), Description("Los usuarios con este rol pueden ingresar a la cola de Trade.")]
    public RemoteControlAccessList RoleCanTrade { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con esta función pueden utilizar las funciones Trade Adicionales.")]
    public RemoteControlAccessList RoleCanTradePlus { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con este rol pueden ingresar a la cola de verificación de semillas/solicitudes especiales.")]
    public RemoteControlAccessList RoleCanSeedCheckorSpecialRequest { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con este rol pueden ingresar a la cola de clonación.")]
    public RemoteControlAccessList RoleCanClone { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con esta función pueden ingresar a la cola de Dump.")]
    public RemoteControlAccessList RoleCanDump { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con este rol pueden ingresar a la cola Fix OT.")]
    public RemoteControlAccessList RoleCanFixOT { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con este rol pueden controlar de forma remota la consola (si la ejecutan como Remote Control Bot.")]
    public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("Los usuarios con este rol pueden omitir las restricciones de comandos.")]
    public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

    // Operation

    [Category(Roles), Description("Los usuarios con este rol pueden unirse a la cola con una mejor posición.")]
    public RemoteControlAccessList RoleFavored { get; set; } = new() { AllowIfEmpty = false };

    [Category(Servers), Description("Los servidores con estos ID no podrán utilizar el bot abandonará el servidor.")]
    public RemoteControlAccessList ServerBlacklist { get; set; } = new() { AllowIfEmpty = false };

    [Category(Users), Description("Los usuarios con estos ID de usuario no pueden utilizar el bot.")]
    public RemoteControlAccessList UserBlacklist { get; set; } = new();

    [Category(Channels), Description("Los canales con estos ID son los únicos canales donde el bot reconoce comandos.")]
    public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

    [Category(Users), Description("ID de usuario de Discord separados por comas que tendrán acceso sudo al Bot Hub.")]
    public RemoteControlAccessList GlobalSudoList { get; set; } = new();

    [Category(Users), Description("Deshabilitar esto eliminará la compatibilidad global con sudo.")]
    public bool AllowGlobalSudo { get; set; } = true;

    [Category(Channels), Description("ID de canal que harán eco de los datos del bot de registro.")]
    public RemoteControlAccessList LoggingChannels { get; set; } = new();

    [Category(Channels), Description("Canales de registro que registrarán mensajes de inicio de operaciones.")]
    public RemoteControlAccessList TradeStartingChannels { get; set; } = new();

    [Category(Channels), Description("Channels that will log special messages, like announcements.")]
    public RemoteControlAccessList AnnouncementChannels { get; set; } = new();

    public AnnouncementSettingsCategory AnnouncementSettings { get; set; } = new();

    [Category(Operation), TypeConverter(typeof(CategoryConverter<AnnouncementSettingsCategory>))]
    public class AnnouncementSettingsCategory
    {
        public override string ToString() => "Announcement Settings";
        [Category("Embed Settings"), Description("Thumbnail option for announcements.")]
        public ThumbnailOption AnnouncementThumbnailOption { get; set; } = ThumbnailOption.Gengar;

        [Category("Embed Settings"), Description("Custom thumbnail URL for announcements.")]
        public string CustomAnnouncementThumbnailUrl { get; set; } = string.Empty;
        public EmbedColorOption AnnouncementEmbedColor { get; set; } = EmbedColorOption.Purple;
        [Category("Embed Settings"), Description("Enable random thumbnail selection for announcements.")]
        public bool RandomAnnouncementThumbnail { get; set; } = false;

        [Category("Embed Settings"), Description("Enable random color selection for announcements.")]
        public bool RandomAnnouncementColor { get; set; } = false;
    }

    [Category(Operation), Description("Devuelve al usuario los archivos PKM de Pokémon mostrados en el intercambio.")]
    public bool ReturnPKMs { get; set; } = true;

    [Category(Operation), Description("Responde a los usuarios si no se les permite utilizar un comando determinado en el canal. Cuando es falso, el bot los ignorará silenciosamente.")]
    public bool ReplyCannotUseCommandInChannel { get; set; } = true;

    [Category(Operation), Description("Bot escucha los mensajes del canal para responder con un Showdown Set cada vez que se adjunta un archivo PKM (no con un comando).")]
    public bool ConvertPKMToShowdownSet { get; set; } = true;

    [Category(Operation), Description("El bot puede responder con un conjunto de showdown en cualquier canal que el bot pueda ver, en lugar de solo los canales en los que el bot ha sido incluido en la lista blanca para ejecutarse. Haga esto solo si desea que el bot tenga más utilidad en canales que no son de bot.")]
    public bool ConvertPKMReplyAnyChannel { get; set; }

    public enum EmbedColorOption
    {
        Blue,
        Green,
        Red,
        Gold,
        Purple,
        Teal,
        Orange,
        Magenta,
        LightGrey,
        DarkGrey
    }

    public enum ThumbnailOption
    {
        Gengar,
        Pikachu,
        Umbreon,
        Sylveon,
        Charmander,
        Jigglypuff,
        Flareon,
        Custom
    }
}
