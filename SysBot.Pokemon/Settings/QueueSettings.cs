using System;
using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public class QueueSettings
{
    private const string FeatureToggle = nameof(FeatureToggle);
    private const string UserBias = nameof(UserBias);
    private const string TimeBias = nameof(TimeBias);
    private const string QueueToggle = nameof(QueueToggle);

    public override string ToString() => "Configuración para unirse a la cola";

    // General

    [Category(FeatureToggle), Description("Alterna si los usuarios pueden unirse a la cola."), DisplayName("Permitir a los usuarios ponerse en cola?")]
    public bool CanQueue { get; set; } = true;

    [Category(FeatureToggle), Description("Evita agregar usuarios si ya hay tantos usuarios en la cola."), DisplayName("Numero maximo de usuarios en cola")]
    public int MaxQueueCount { get; set; } = 999;

    [Category(FeatureToggle), Description("Permite a los usuarios salir de la cola mientras se intercambian."), DisplayName("Permitir a usuarios salir de la cola incluso si esta siendo procesado?")]
    public bool CanDequeueIfProcessing { get; set; }

    [Category(FeatureToggle), Description("Determina cómo el modo Flex procesará las colas."), DisplayName("Modo Flex")]
    public FlexYieldMode FlexMode { get; set; } = FlexYieldMode.Weighted;

    [Category(FeatureToggle), Description("Determina cuándo se activa y desactiva la cola."), DisplayName("Modo de alternancia de colas")]
    public QueueOpening QueueToggleMode { get; set; } = QueueOpening.Threshold;

    // Queue Toggle

    [Category(QueueToggle), Description("Modo de umbral: recuento de usuarios que harán que se abra la cola")]
    public int ThresholdUnlock { get; set; }

    [Category(QueueToggle), Description("Modo de umbral: recuento de usuarios que provocarán el cierre de la cola.")]
    public int ThresholdLock { get; set; } = 30;

    [Category(QueueToggle), Description("Modo programado: segundos de estar abierto antes de que se bloquee la cola.")]
    public int IntervalOpenFor { get; set; } = 5 * 60;

    [Category(QueueToggle), Description("Modo programado: segundos de cierre antes de que se desbloquee la cola.")]
    public int IntervalCloseFor { get; set; } = 15 * 60;

    // Flex Users

    [Category(UserBias), Description("Sesga el peso de la cola de comercio en función de cuántos usuarios hay en la cola.")]
    public int YieldMultCountTrade { get; set; } = 100;

    [Category(UserBias), Description("Sesga el peso de la cola de verificación de semillas en función de cuántos usuarios hay en la cola.")]
    public int YieldMultCountSeedCheck { get; set; } = 100;

    [Category(UserBias), Description("Sesga el peso de la cola de clonación en función de cuántos usuarios hay en la cola.")]
    public int YieldMultCountClone { get; set; } = 100;

    [Category(UserBias), Description("Sesga el peso de la cola Fix OT en función de cuántos usuarios hay en la cola.")]
    public int YieldMultCountFixOT { get; set; } = 100;

    [Category(UserBias), Description("Sesga el peso de la cola de volcado en función de cuántos usuarios hay en la cola.")]
    public int YieldMultCountDump { get; set; } = 100;

    // Flex Time

    [Category(TimeBias), Description("Determina si el peso debe sumarse o multiplicarse por el peso total.")]
    public FlexBiasMode YieldMultWait { get; set; } = FlexBiasMode.Multiply;

    [Category(TimeBias), Description("Comprueba el tiempo transcurrido desde que el usuario se unió a la cola de Comercio y aumenta el peso de la cola en consecuencia.")]
    public int YieldMultWaitTrade { get; set; } = 1;

    [Category(TimeBias), Description("Comprueba el tiempo transcurrido desde que el usuario se unió a la cola de verificación de semillas y aumenta el peso de la cola en consecuencia.")]
    public int YieldMultWaitSeedCheck { get; set; } = 1;

    [Category(TimeBias), Description("Comprueba el tiempo transcurrido desde que el usuario se unió a la cola de clonación y aumenta el peso de la cola en consecuencia.")]
    public int YieldMultWaitClone { get; set; } = 1;

    [Category(TimeBias), Description("Comprueba el tiempo transcurrido desde que el usuario se unió a la cola de volcado y aumenta el peso de la cola en consecuencia.")]
    public int YieldMultWaitDump { get; set; } = 1;

    [Category(TimeBias), Description("Comprueba el tiempo transcurrido desde que el usuario se unió a la cola Fix OT y aumenta el peso de la cola en consecuencia.")]
    public int YieldMultWaitFixOT { get; set; } = 1;

    [Category(TimeBias), Description("Multiplica la cantidad de usuarios en cola para dar una estimación de cuánto tiempo llevará hasta que se procese al usuario.")]
    public float EstimatedDelayFactor { get; set; } = 1.1f;

    private int GetCountBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultCountSeedCheck,
        PokeTradeType.Clone => YieldMultCountClone,
        PokeTradeType.Dump => YieldMultCountDump,
        PokeTradeType.FixOT => YieldMultCountFixOT,
        _ => YieldMultCountTrade,
    };

    private int GetTimeBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultWaitSeedCheck,
        PokeTradeType.Clone => YieldMultWaitClone,
        PokeTradeType.Dump => YieldMultWaitDump,
        PokeTradeType.FixOT => YieldMultWaitFixOT,
        _ => YieldMultWaitTrade,
    };

    /// <summary>
    /// Gets the weight of a <see cref="PokeTradeType"/> based on the count of users in the queue and time users have waited.
    /// </summary>
    /// <param name="count">Count of users for <see cref="type"/></param>
    /// <param name="time">Next-to-be-processed user's time joining the queue</param>
    /// <param name="type">Queue type</param>
    /// <returns>Effective weight for the trade type.</returns>
    public long GetWeight(int count, DateTime time, PokeTradeType type)
    {
        var now = DateTime.Now;
        var seconds = (now - time).Seconds;

        var cb = GetCountBias(type) * count;
        var tb = GetTimeBias(type) * seconds;

        return YieldMultWait switch
        {
            FlexBiasMode.Multiply => cb * tb,
            _ => cb + tb,
        };
    }

    /// <summary>
    /// Estimates the amount of time (minutes) until the user will be processed.
    /// </summary>
    /// <param name="position">Position in the queue</param>
    /// <param name="botct">Amount of bots processing requests</param>
    /// <returns>Estimated time in Minutes</returns>
    public float EstimateDelay(int position, int botct) => (EstimatedDelayFactor * position) / botct;
}

public enum FlexBiasMode
{
    Add,
    Multiply,
}

public enum FlexYieldMode
{
    LessCheatyFirst,
    Weighted,
}

public enum QueueOpening
{
    Manual,
    Threshold,
    Interval,
}
