using PKHeX.Core;
using SysBot.Base;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class DistributionSettings : ISynchronizationSetting
{
    private const string Distribute = nameof(Distribute);

    private const string Synchronize = nameof(Synchronize);

    [Category(Distribute), Description("Cuando está habilitado, los bots inactivos de Link Trade distribuirán aleatoriamente archivos PKM desde la carpeta de distribución."), DisplayName("Distribuir mientas el bot esta inactivo?")]
    public bool DistributeWhileIdle { get; set; } = true;

    [Category(Distribute), Description("Si se establece en true, las operaciones de intercambio de nicks de Random Ledy se cancelarán en lugar de intercambiar una entidad aleatoria del grupo.")]
    public bool LedyQuitIfNoMatch { get; set; }

    [Category(Distribute), Description("Cuando se establece en algo distinto a Ninguno, los intercambios aleatorios requerirán esta especie además de la coincidencia del apodo.")]
    public Species LedySpecies { get; set; } = Species.None;

    [Category(Distribute), Description("El código de enlace comercial de distribución utiliza el rango mínimo y máximo en lugar del código comercial fijo."), DisplayName("Usar un codigo aleatorio para la distribución?")]
    public bool RandomCode { get; set; }

    [Category(Distribute), Description("Para BDSP, el robot de distribución irá a una sala específica y permanecerá allí hasta que se detenga."), DisplayName("Permanecer en la sala Unión (BDSP)")]
    public bool RemainInUnionRoomBDSP { get; set; } = true;

    // Distribute
    [Category(Distribute), Description("Cuando está activada, la DistributionFolder se producirá aleatoriamente en lugar de en la misma secuencia."), DisplayName("Aleatorizar los archivos de distribución?.")]
    public bool Shuffled { get; set; }

    [Category(Synchronize), Description("Link Trade: Usando múltiples bots de distribución -- todos los bots confirmarán su código de comercio al mismo tiempo. Cuando es Local, los bots continuarán cuando todos estén en la barrera. En Remoto, algo más debe indicar a los bots que continúen."), DisplayName("Sincronizar Bots")]
    public BotSyncOption SynchronizeBots { get; set; } = BotSyncOption.LocalSync;

    // Synchronize
    [Category(Synchronize), Description("Link Trade: Usando múltiples bots de distribución -- una vez que todos los bots estén listos para confirmar el código de comercio, el Hub esperará X milisegundos antes de liberar a todos los bots."), DisplayName("Sincronizar el retraso de la barrera")]
    public int SynchronizeDelayBarrier { get; set; }

    [Category(Synchronize), Description("Link Trade: Usando múltiples bots de distribución -- cuánto tiempo (segundos) esperará un bot para sincronizarse antes de continuar de todos modos."), DisplayName("Tiempo de espera de sincronización")]
    public double SynchronizeTimeout { get; set; } = 90;

    [Category(Distribute), Description("Código de enlace comercial a usar en la distribución."), DisplayName("Codigo de distribución")]
    public int TradeCode { get; set; } = 7196;

    public override string ToString() => "Configuración comercial de distribución";
}
