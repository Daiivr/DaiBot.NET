using System.ComponentModel;

namespace SysBot.Pokemon;

public class TimingSettings
{
    private const string CloseGame = nameof(CloseGame);

    private const string Misc = nameof(Misc);

    private const string OpenGame = nameof(OpenGame);

    private const string Raid = nameof(Raid);

    [Category(Misc), Description("Habilite esta opción para rechazar las actualizaciones entrantes del sistema.")]
    public bool AvoidSystemUpdate { get; set; }

    [Category(Misc), Description("Tiempo adicional en milisegundos para esperar entre intentos de reconectarse. El tiempo base es de 30 segundos.")]
    public int ExtraReconnectDelay { get; set; }

    [Category(Raid), Description("[Raid Bot] Tiempo extra en milisegundos para esperar después de aceptar a un amigo.")]
    public int ExtraTimeAddFriend { get; set; }

    [Category(OpenGame), Description("Tiempo extra en milisegundos para esperar y comprobar si el DLC se puede utilizar.")]
    public int ExtraTimeCheckDLC { get; set; }

    [Category(CloseGame), Description("Tiempo extra en milisegundos de espera después de hacer clic para cerrar el juego.")]
    public int ExtraTimeCloseGame { get; set; }

    // Miscellaneous settings.
    [Category(Misc), Description("[SWSH/SV] Tiempo adicional de espera en milisegundos después de hacer clic en + para conectarse a Y-Comm (SWSH) o L para conectarse en línea (SV).")]
    public int ExtraTimeConnectOnline { get; set; }

    [Category(Raid), Description("[Raid Bot] Tiempo extra en milisegundos para esperar después de eliminar a un amigo.")]
    public int ExtraTimeDeleteFriend { get; set; }

    [Category(Raid), Description("[Raid Bot] Tiempo extra en milisegundos para esperar antes de cerrar el juego para reiniciar el raid.")]
    public int ExtraTimeEndRaid { get; set; }

    [Category(Misc), Description("[BDSP] Tiempo adicional en milisegundos para esperar a que se cargue Union Room antes de intentar solicitar un intercambio.")]
    public int ExtraTimeJoinUnionRoom { get; set; } = 500;

    [Category(Misc), Description("[BDSP] Tiempo extra en milisegundos para esperar a que se cargue el supramundo después de salir de Union Room.")]
    public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

    [Category(OpenGame), Description("Tiempo extra en milisegundos para esperar antes de hacer clic en A en la pantalla de título.")]
    public int ExtraTimeLoadGame { get; set; } = 5000;

    [Category(OpenGame), Description("[BDSP] Tiempo extra en milisegundos para esperar a que se cargue el mundo exterior después de la pantalla de título.")]
    public int ExtraTimeLoadOverworld { get; set; } = 3000;

    [Category(Misc), Description("[SV] Tiempo extra en milisegundos para esperar a que se cargue el Poké Portal.")]
    public int ExtraTimeLoadPortal { get; set; } = 1000;

    // Opening the game.
    [Category(OpenGame), Description("Tiempo extra en milisegundos para esperar a que se carguen los perfiles al iniciar el juego.")]
    public int ExtraTimeLoadProfile { get; set; }

    // Raid-specific timings.
    [Category(Raid), Description("[Raid Bot] Tiempo extra en milisegundos para esperar a que se cargue la incursión después de hacer clic en la guarida.")]
    public int ExtraTimeLoadRaid { get; set; }

    [Category(Misc), Description("Tiempo extra en milisegundos para esperar a que se cargue la caja después de encontrar una operación.")]
    public int ExtraTimeOpenBox { get; set; } = 1000;

    [Category(Misc), Description("Es hora de esperar después de abrir el teclado para ingresar el código durante las operaciones.")]
    public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

    [Category(Raid), Description("[Raid Bot] Tiempo extra en milisegundos para esperar después de hacer clic en \"Invitar a otros\" antes de bloquear un Pokémon.")]
    public int ExtraTimeOpenRaid { get; set; }

    [Category(Misc), Description("[BDSP] Tiempo adicional en milisegundos para esperar a que se cargue el menú Y al inicio de cada ciclo comercial.")]
    public int ExtraTimeOpenYMenu { get; set; } = 500;

    // Closing the game.
    [Category(CloseGame), Description("Tiempo extra en milisegundos para esperar después de presionar HOME para minimizar el juego.")]
    public int ExtraTimeReturnHome { get; set; }

    [Category(Misc), Description("Tiempo de espera después de cada pulsación de tecla al navegar por los menús de Switch o ingresar el código de enlace.")]
    public int KeypressTime { get; set; } = 200;

    [Category(Misc), Description("Número de veces que se intenta volver a conectar a una conexión de socket después de que se pierde una conexión. Establezca esto en -1 para intentarlo indefinidamente.")]
    public int ReconnectAttempts { get; set; } = 30;

    public override string ToString() => "Configuración de tiempo adicional";
}
