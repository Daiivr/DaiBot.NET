using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class TwitchSettings
{
    private const string Messages = nameof(Messages);

    private const string Operation = nameof(Operation);

    private const string Startup = nameof(Startup);

    [Category(Operation), Description("Cuando esté habilitado, el bot procesará los comandos enviados al canal.")]
    public bool AllowCommandsViaChannel { get; set; } = true;

    [Category(Operation), Description("Cuando esté habilitado, el bot permitirá a los usuarios enviar comandos mediante susurros (evite el modo lento)")]
    public bool AllowCommandsViaWhisper { get; set; }

    [Category(Startup), Description("Canal al que enviar mensajes")]
    public string Channel { get; set; } = string.Empty;

    [Category(Startup), Description("Prefijo de comando de bot")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("Enlace del servidor de discord.")]
    public string DiscordLink { get; set; } = string.Empty;

    [Category(Messages), Description("Alterna si las operaciones de distribución cuentan hacia atrás antes de comenzar.")]
    public bool DistributionCountDown { get; set; } = true;

    [Category(Operation), Description("Enlace de donación.")]
    public string DonationLink { get; set; } = string.Empty;

    [Category(Operation), Description("Mensaje enviado cuando se libera la Barrera.")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Messages), Description("Determina dónde se envían las notificaciones genéricas.")]
    public TwitchMessageDestination NotifyDestination { get; set; }

    [Category(Operation), Description("Nombres de usuario de Sudo")]
    public string SudoList { get; set; } = string.Empty;

    [Category(Operation), Description("Evitar que el bot envíe mensajes si se han enviado X mensajes en los últimos Y segundos.")]
    public int ThrottleMessages { get; set; } = 100;

    // Messaging
    [Category(Operation), Description("Limite el envío de mensajes del bot si se han enviado X mensajes en los últimos Y segundos.")]
    public double ThrottleSeconds { get; set; } = 30;

    [Category(Operation), Description("Limite el envío de susurros al bot si se han enviado X mensajes en los últimos Y segundos.")]
    public int ThrottleWhispers { get; set; } = 100;

    [Category(Operation), Description("Limite el envío de susurros al bot si se han enviado X mensajes en los últimos Y segundos.")]
    public double ThrottleWhispersSeconds { get; set; } = 60;

    [Category(Startup), Description("Token de inicio de sesión de bot")]
    public string Token { get; set; } = string.Empty;

    [Category(Messages), Description("Determina dónde se envían las notificaciones de transacciones canceladas.")]
    public TwitchMessageDestination TradeCanceledDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Messages), Description("Determina dónde se envían las notificaciones de Trade Finish.")]
    public TwitchMessageDestination TradeFinishDestination { get; set; }

    [Category(Messages), Description("Determina dónde se envían las notificaciones de búsqueda comercial.")]
    public TwitchMessageDestination TradeSearchDestination { get; set; }

    // Message Destinations
    [Category(Messages), Description("Determina dónde se envían las notificaciones de Trade Start.")]
    public TwitchMessageDestination TradeStartDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Operation), Description("Enlace al tutorial de uso del bot.")]
    public string TutorialLink { get; set; } = string.Empty;

    [Category(Operation), Description("Texto del tutorial sobre el uso de bots.")]
    public string TutorialText { get; set; } = string.Empty;

    // Operation
    [Category(Operation), Description("Los usuarios con estos nombres de usuario no pueden utilizar el bot.")]
    public string UserBlacklist { get; set; } = string.Empty;

    // Startup
    [Category(Startup), Description("Nombre de usuario del robot")]
    public string Username { get; set; } = string.Empty;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }

    public override string ToString() => "Configuración de integración de Twitch";
}

public enum TwitchMessageDestination
{
    Disabled,

    Channel,

    Whisper,
}
