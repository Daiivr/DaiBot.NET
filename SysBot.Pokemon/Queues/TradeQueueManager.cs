using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Manages multiple trade queues and handles trade distribution logic.
/// </summary>
public class TradeQueueManager<T> where T : PKM, new()
{
    public readonly PokeTradeQueue<T>[] AllQueues;
    public readonly List<Action<PokeRoutineExecutorBase, PokeTradeDetail<T>>> Forwarders = [];
    public readonly TradeQueueInfo<T> Info;

    private readonly BatchTradeTracker<T> _batchTracker = new();
    private readonly PokeTradeHub<T> Hub;

    // Individual queues for different trade types
    private readonly PokeTradeQueue<T> Batch = new(PokeTradeType.Batch);
    private readonly PokeTradeQueue<T> Clone = new(PokeTradeType.Clone);
    private readonly PokeTradeQueue<T> Dump = new(PokeTradeType.Dump);
    private readonly PokeTradeQueue<T> FixOT = new(PokeTradeType.FixOT);
    private readonly PokeTradeQueue<T> Seed = new(PokeTradeType.Seed);
    private readonly PokeTradeQueue<T> Trade = new(PokeTradeType.Specific);

    public TradeQueueManager(PokeTradeHub<T> hub)
    {
        Hub = hub;
        Info = new(hub);
        AllQueues = [Seed, Dump, Clone, FixOT, Trade, Batch];

        foreach (var q in AllQueues)
            q.Queue.Settings = hub.Config.Favoritism;
    }

    public void ClearAll()
    {
        foreach (var q in AllQueues)
            q.Clear();
    }

    public void Enqueue(PokeRoutineType type, PokeTradeDetail<T> detail, uint priority) =>
            GetQueue(type).Enqueue(detail, priority);

    public PokeTradeQueue<T> GetQueue(PokeRoutineType type) => type switch
    {
        PokeRoutineType.SeedCheck => Seed,
        PokeRoutineType.Clone => Clone,
        PokeRoutineType.Dump => Dump,
        PokeRoutineType.FixOT => FixOT,
        PokeRoutineType.Batch => Batch,
        _ => Trade,
    };

    public void StartTrade(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail)
    {
        foreach (var f in Forwarders)
            f.Invoke(b, detail);
    }

    public void CompleteTrade(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) =>
            _batchTracker.CompleteBatchTrade(detail);

    public bool TryDequeue(PokeRoutineType type, out PokeTradeDetail<T> detail, out uint priority, string botName)
    {
        detail = default!;
        priority = default;
        var queue = GetQueue(type);
        if (!queue.TryPeek(out detail, out priority))
            return false;

        if (detail.TotalBatchTrades > 1 && !_batchTracker.CanProcessBatchTrade(detail))
            return false;

        if (!queue.TryDequeue(out detail, out priority))
            return false;

        if (detail.TotalBatchTrades > 1 && !_batchTracker.TryClaimBatchTrade(detail, botName))
        {
            queue.Enqueue(detail, priority);
            return false;
        }

        return true;
    }

    public bool TryDequeueLedy(out PokeTradeDetail<T> detail, bool force = false)
    {
        detail = default!;
        var cfg = Hub.Config.Distribution;

        if ((!cfg.DistributeWhileIdle && !force) || Hub.Ledy.Pool.Count == 0)
            return false;
        var random = Hub.Ledy.Pool.GetRandomPoke();
        var code = cfg.RandomCode ? Hub.Config.Trade.GetRandomTradeCode() : cfg.TradeCode;
        var lgcode = GetDefaultLGCode();

        var trainer = new PokeTradeTrainerInfo("Random Distribution");
        detail = new(random, trainer, PokeTradeHub<T>.LogNotifier, PokeTradeType.Random, code, false, lgcode);
        return true;
    }

    private static List<Pictocodes> GetDefaultLGCode() =>
        [Pictocodes.Pikachu, Pictocodes.Pikachu, Pictocodes.Pikachu];
}
