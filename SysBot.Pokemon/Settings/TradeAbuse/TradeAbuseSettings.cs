using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = nameof(Monitoring);

    [Category(Monitoring), Description("IDs en línea baneados que provocarán la cancelacion del trade o el bloqueo en el juego."), DisplayName("IDs Baneados")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [Category(Monitoring), Description("Cuando se detecta abuso de una persona que utiliza el intercambio de apodos de Ledy, el mensaje de eco incluirá su ID de cuenta Nintendo."), DisplayName("Mostrar ID de Nintendo")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [Category(Monitoring), Description("Si no está vacía, la cadena proporcionada se añadirá a las alertas de Eco para notificar a quien especifiques cuando un usuario infrinja las reglas de comercio de Ledy. Para Discord, utilice <@userIDnumber> para mencionar.")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("Cuando una persona aparece con una cuenta de Discord/Twitch diferente en menos del valor de esta configuración (minutos), se enviará una notificación."), DisplayName("Expiración de TradeAbuse")]
    public double TradeAbuseExpiration { get; set; } = 10;

    [Category(Monitoring), Description("Cuando una persona vuelve a aparecer en menos del valor de esta configuración (minutos), se enviará una notificación.."), DisplayName("Enfriamiento del Trade")]
    public double TradeCooldown { get; set; }

    public override string ToString() => "Configuración de monitoreo de abuso comercial";
}
