using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;

namespace SysBot.Pokemon;

public class PokeTradeLogNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
{
    public Action<PokeRoutineExecutor<T>>? OnFinish { get; set; }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        LogUtil.LogInfo(message, routine.Connection.Label);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
        LogUtil.LogInfo(msg, routine.Connection.Label);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        LogUtil.LogInfo($"Notificando a {info.Trainer.TrainerName} acerca de su {GameInfo.GetStrings(1).Species[result.Species]}", routine.Connection.Label);
        LogUtil.LogInfo(message, routine.Connection.Label);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        LogUtil.LogInfo($"Cancelando el intercambio con {info.Trainer.TrainerName}, porque {msg}.", routine.Connection.Label);
        OnFinish?.Invoke(routine);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        LogUtil.LogInfo($"Finalizado el comercio {info.Trainer.TrainerName} {GameInfo.GetStrings(1).Species[info.TradeData.Species]} para {GameInfo.GetStrings(1).Species[result.Species]}", routine.Connection.Label);
        OnFinish?.Invoke(routine);
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        LogUtil.LogInfo($"Iniciando el bucle comercial para {info.Trainer.TrainerName}, enviando {GameInfo.GetStrings(1).Species[info.TradeData.Species]}", routine.Connection.Label);
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        LogUtil.LogInfo($"Búsqueda de operaciones con {info.Trainer.TrainerName}, enviando {GameInfo.GetStrings(1).Species[info.TradeData.Species]}", routine.Connection.Label);
    }
}
