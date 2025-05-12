using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class TimingSettings
    {
        private const string TimingsCategory = "Tiempos";

        [Category(TimingsCategory), TypeConverter(typeof(ExpandableObjectConverter))]
        public MiscellaneousSettingsCategory MiscellaneousSettings { get; set; } = new();

        [Category(TimingsCategory), TypeConverter(typeof(ExpandableObjectConverter))]
        public OpeningGameSettingsCategory OpeningGameSettings { get; set; } = new();

        [Category(TimingsCategory), TypeConverter(typeof(ExpandableObjectConverter))]
        public RaidSettingsCategory RaidSettings { get; set; } = new();

        [Category(TimingsCategory), TypeConverter(typeof(ExpandableObjectConverter))]
        public ClosingGameSettingsCategory ClosingGameSettings { get; set; } = new();

        public override string ToString() => "Configuración de Tiempos";
    }

    // Categoría de configuración variada
    public class MiscellaneousSettingsCategory
    {
        public override string ToString() => "Configuración Variada";

        [Description("Activa esto para rechazar actualizaciones del sistema entrantes.")]
        public bool AvoidSystemUpdate { get; set; }

        [Description("Tiempo extra en milisegundos para esperar entre intentos de reconexión. El tiempo base es de 30 segundos.")]
        public int ExtraReconnectDelay { get; set; }

        [Description("[SWSH/SV] Tiempo extra en milisegundos para esperar después de presionar + para conectarse a Y-Comm (SWSH) o L para conectarse en línea (SV).")]
        public int ExtraTimeConnectOnline { get; set; }

        [Description("[BDSP] Tiempo extra en milisegundos para esperar a que cargue la Sala Unión antes de intentar llamar a un intercambio.")]
        public int ExtraTimeJoinUnionRoom { get; set; } = 500;

        [Description("[BDSP] Tiempo extra en milisegundos para esperar a que cargue el mundo exterior después de salir de la Sala Unión.")]
        public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

        [Description("[SV] Tiempo extra en milisegundos para esperar a que cargue el Poké Portal.")]
        public int ExtraTimeLoadPortal { get; set; } = 1000;

        [Description("Tiempo extra en milisegundos para esperar a que cargue la caja después de encontrar un intercambio.")]
        public int ExtraTimeOpenBox { get; set; } = 1000;

        [Description("Tiempo de espera después de abrir el teclado para ingresar el código durante los intercambios.")]
        public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

        [Description("[BDSP] Tiempo extra en milisegundos para esperar a que cargue el menú Y al inicio de cada bucle de intercambio.")]
        public int ExtraTimeOpenYMenu { get; set; } = 500;

        [Description("Tiempo de espera después de cada pulsación de tecla al navegar por los menús de Switch o ingresar el Código de Enlace.")]
        public int KeypressTime { get; set; } = 200;

        [Description("Número de intentos para reconectar a una conexión de socket después de que se pierde la conexión. Configura esto en -1 para intentarlo indefinidamente.")]
        public int ReconnectAttempts { get; set; } = 30;
    }

    // Categoría de configuración de apertura del juego
    public class OpeningGameSettingsCategory
    {
        public override string ToString() => "Apertura del Juego";

        [Description("Tiempo extra en milisegundos para esperar antes de presionar A en la pantalla de título.")]
        public int ExtraTimeLoadGame { get; set; } = 5000;

        [Description("Tiempo extra en milisegundos para esperar a que el mundo exterior se cargue después de la pantalla de título.")]
        public int ExtraTimeLoadOverworld { get; set; } = 3000;

        [Description("Activa esto si necesitas seleccionar un perfil al iniciar el juego.")]
        public bool ProfileSelectionRequired { get; set; } = true;

        [Description("Tiempo extra en milisegundos para esperar a que carguen los perfiles al iniciar el juego.")]
        public int ExtraTimeLoadProfile { get; set; }

        [Description("Activa esto para agregar un retraso al mensaje emergente \"Verificando si el juego puede jugarse\".")]
        public bool CheckGameDelay { get; set; } = false;

        [Description("Tiempo extra de espera para el mensaje emergente \"Verificando si el juego puede jugarse\".")]
        public int ExtraTimeCheckGame { get; set; } = 200;
    }

    // Categoría de configuración específica de incursiones
    public class RaidSettingsCategory
    {
        public override string ToString() => "Tiempos Específicos de Incursiones";

        [Description("[RaidBot] Tiempo extra en milisegundos para esperar después de aceptar a un amigo.")]
        public int ExtraTimeAddFriend { get; set; }

        [Description("[RaidBot] Tiempo extra en milisegundos para esperar después de eliminar a un amigo.")]
        public int ExtraTimeDeleteFriend { get; set; }

        [Description("[RaidBot] Tiempo extra en milisegundos para esperar antes de cerrar el juego para reiniciar la incursión.")]
        public int ExtraTimeEndRaid { get; set; }

        [Description("[RaidBot] Tiempo extra en milisegundos para esperar a que cargue la incursión después de hacer clic en el nido.")]
        public int ExtraTimeLoadRaid { get; set; }

        [Description("[RaidBot] Tiempo extra en milisegundos para esperar después de hacer clic en \"Invitar a otros\" antes de seleccionar un Pokémon.")]
        public int ExtraTimeOpenRaid { get; set; }
    }

    // Categoría de configuración de cierre del juego
    public class ClosingGameSettingsCategory
    {
        public override string ToString() => "Cierre del Juego";

        [Description("Tiempo extra en milisegundos para esperar después de hacer clic para cerrar el juego.")]
        public int ExtraTimeCloseGame { get; set; }

        [Description("Tiempo extra en milisegundos para esperar después de presionar HOME para minimizar el juego.")]
        public int ExtraTimeReturnHome { get; set; }
    }
}
