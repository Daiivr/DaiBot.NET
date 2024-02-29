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
            case PokeTradeResult.NoTrainerFound:
                return "__No se encontró ningún entrenador__";
            case PokeTradeResult.TrainerTooSlow:
                return "__El entrenador fue demasiado lento__";
            case PokeTradeResult.TrainerLeft:
                return "__El entrenador abandonó el intercambio__";
            case PokeTradeResult.TrainerOfferCanceledQuick:
                return "__El entrenador canceló la oferta demasiado rapido__";
            case PokeTradeResult.TrainerRequestBad:
                return "__Solicitud del entrenador inválida__";
            case PokeTradeResult.IllegalTrade:
                return "__Intercambio ilegal__.";
            case PokeTradeResult.SuspiciousActivity:
                return "__Actividad sospechosa detectada__";
            // Agrega casos para otros valores si es necesario
            default:
                return result.ToString(); // Devuelve el nombre por defecto para los valores no especificados
        }
    }
    public static bool ShouldAttemptRetry(this PokeTradeResult t) => t >= PokeTradeResult.RoutineCancel;
}
