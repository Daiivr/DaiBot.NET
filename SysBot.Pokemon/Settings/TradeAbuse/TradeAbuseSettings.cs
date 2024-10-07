using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = nameof(Monitoring);
    public override string ToString() => "Configuración de Monitoreo de Abuso de Intercambios";

    [Category(Monitoring), Description("Cuando una persona reaparece en menos tiempo que el valor de esta configuración (minutos), se enviará una notificación.")]
    public double TradeCooldown { get; set; }

    [Category(Monitoring), Description("Cuando una persona ignora el enfriamiento de intercambio, el mensaje de eco incluirá su Nintendo Account ID.")]
    public bool EchoNintendoOnlineIDCooldown { get; set; } = true;

    [Category(Monitoring), Description("Si no está vacío, la cadena proporcionada se agregará a las alertas de eco para notificar a quien especifiques cuando un usuario viole el enfriamiento de intercambio. Para Discord, usa <@userIDnumber> para mencionar.")]
    public string CooldownAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("Cuando una persona aparece con una cuenta diferente de Discord/Twitch en menos tiempo que el valor de esta configuración (minutos), se enviará una notificación.")]
    public double TradeAbuseExpiration { get; set; } = 120;

    [Category(Monitoring), Description("Cuando se detecta a una persona usando múltiples cuentas de Discord/Twitch, el mensaje de eco incluirá su Nintendo Account ID.")]
    public bool EchoNintendoOnlineIDMulti { get; set; } = true;

    [Category(Monitoring), Description("Cuando se detecta a una persona enviando a múltiples cuentas del juego, el mensaje de eco incluirá su Nintendo Account ID.")]
    public bool EchoNintendoOnlineIDMultiRecipients { get; set; } = true;

    [Category(Monitoring), Description("Cuando se detecta a una persona usando múltiples cuentas de Discord/Twitch, se tomará esta acción.")]
    public TradeAbuseAction TradeAbuseAction { get; set; } = TradeAbuseAction.Quit;

    [Category(Monitoring), Description("Cuando una persona es bloqueada en el juego por usar múltiples cuentas, su ID en línea se agregará a BannedIDs.")]
    public bool BanIDWhenBlockingUser { get; set; } = true;

    [Category(Monitoring), Description("Si no está vacío, la cadena proporcionada se agregará a las alertas de eco para notificar a quien especifiques cuando se detecte a un usuario usando múltiples cuentas. Para Discord, usa <@userIDnumber> para mencionar.")]
    public string MultiAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("Si no está vacío, la cadena proporcionada se agregará a las alertas de eco para notificar a quien especifiques cuando se detecte a un usuario enviando a múltiples jugadores en el juego. Para Discord, usa <@userIDnumber> para mencionar.")]
    public string MultiRecipientEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("IDs en línea baneados que provocarán la cancelacion del trade o el bloqueo en el juego."), DisplayName("IDs Baneados")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [Category(Monitoring), Description("Cuando se detecta a una persona con una ID prohibida, bloquearla en el juego antes de salir del intercambio.")]
    public bool BlockDetectedBannedUser { get; set; } = true;

    [Category(Monitoring), Description("Si no está vacío, la cadena proporcionada se añadirá a las alertas de eco para notificar a quien especifiques cuando un usuario coincida con una ID prohibida. Para Discord, usa <@userIDnumber> para mencionar.")]
    public string BannedIDMatchEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("Cuando se detecta abuso de una persona que utiliza el intercambio de apodos de Ledy, el mensaje de eco incluirá su ID de cuenta Nintendo."), DisplayName("Mostrar ID de Nintendo")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [Category(Monitoring), Description("Si no está vacía, la cadena proporcionada se añadirá a las alertas de Eco para notificar a quien especifiques cuando un usuario infrinja las reglas de comercio de Ledy. Para Discord, utilice <@userIDnumber> para mencionar.")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;
}
