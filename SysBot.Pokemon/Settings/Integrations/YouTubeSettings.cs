using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class YouTubeSettings
{
    private const string Messages = nameof(Messages);

    private const string Operation = nameof(Operation);

    private const string Startup = nameof(Startup);

    [Category(Startup), Description("ID del canal al que enviar mensajes")]
    public string ChannelID { get; set; } = string.Empty;

    [Category(Startup), Description("ID de cliente del bot")]
    public string ClientID { get; set; } = string.Empty;

    // Startup
    [Category(Startup), Description("Cliente Secreto del bot")]
    public string ClientSecret { get; set; } = string.Empty;

    [Category(Startup), Description("Prefijo de comando de bot")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("Mensaje enviado cuando se libera la Barrera.")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Operation), Description("Nombres de usuario de Sudo")]
    public string SudoList { get; set; } = string.Empty;

    // Operation
    [Category(Operation), Description("Los usuarios con estos nombres de usuario no pueden usar el bot.")]
    public string UserBlacklist { get; set; } = string.Empty;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }

    public override string ToString() => "Configuración de integración de YouTube";
}

public enum YouTubeMessageDestination
{
    Disabled,

    Channel,
}
