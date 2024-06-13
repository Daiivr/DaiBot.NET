namespace SysBot.Pokemon;

public enum PokeTradeResult
{
    Success,

    // Trade Partner Failures
    NoTrainerFound,

    TrainerTooSlow,

    TrainerLeft,

    TrainerOfferCanceledQuick,

    TrainerRequestBad,

    IllegalTrade,

    SuspiciousActivity,

    // Recovery -- General Bot Failures
    // Anything below here should be retried once if possible.
    RoutineCancel,

    ExceptionConnection,

    ExceptionInternal,

    RecoverStart,

    RecoverPostLinkCode,

    RecoverOpenBox,

    RecoverReturnOverworld,

    RecoverEnterUnionRoom,
}

public static class PokeTradeResultExtensions
{
    public static string GetDescription(this PokeTradeResult result)
    {
        switch (result)
        {
            case PokeTradeResult.Success:
                return "__Intercambio exitoso__";
            case PokeTradeResult.NoTrainerFound:
                return "__No se encontró ningún entrenador__";
            case PokeTradeResult.TrainerTooSlow:
                return "__El entrenador fue demasiado lento__";
            case PokeTradeResult.TrainerLeft:
                return "__El entrenador abandonó el intercambio__";
            case PokeTradeResult.TrainerOfferCanceledQuick:
                return "__El entrenador canceló la oferta demasiado rápido__";
            case PokeTradeResult.TrainerRequestBad:
                return "__Solicitud del entrenador inválida__";
            case PokeTradeResult.IllegalTrade:
                return "__Intercambio ilegal__";
            case PokeTradeResult.SuspiciousActivity:
                return "__Actividad sospechosa detectada__";
            case PokeTradeResult.RoutineCancel:
                return "__Cancelación de rutina por el bot__";
            case PokeTradeResult.ExceptionConnection:
                return "__Excepción de conexión__";
            case PokeTradeResult.ExceptionInternal:
                return "__Excepción interna del sistema__";
            case PokeTradeResult.RecoverStart:
                return "__Recuperación iniciada__";
            case PokeTradeResult.RecoverPostLinkCode:
                return "__Recuperación después de código de enlace__";
            case PokeTradeResult.RecoverOpenBox:
                return "__Recuperación al abrir caja__";
            case PokeTradeResult.RecoverReturnOverworld:
                return "__Recuperación al volver al mundo__";
            case PokeTradeResult.RecoverEnterUnionRoom:
                return "__Recuperación al entrar en la sala de unión__";
            default:
                return result.ToString(); // Devuelve el nombre por defecto para los valores no especificados
        }
    }

    public static bool ShouldAttemptRetry(this PokeTradeResult t) => t >= PokeTradeResult.RoutineCancel;
}
