using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Manages multiple trade queues and handles trade distribution logic.
    /// </summary>
    public class TradeQueueManager<T> where T : PKM, new()
    {
        public readonly PokeTradeQueue<T>[] AllQueues;
        public readonly List<Action<PokeRoutineExecutorBase, PokeTradeDetail<T>>> Forwarders = [];
        public readonly TradeQueueInfo<T> Info;
        private readonly BatchTradeTracker<T> _batchTracker = new();

        private readonly PokeTradeQueue<T> Batch = new(PokeTradeType.Batch);
        private readonly PokeTradeQueue<T> Clone = new(PokeTradeType.Clone);
        private readonly PokeTradeQueue<T> Dump = new(PokeTradeType.Dump);
        private readonly PokeTradeQueue<T> FixOT = new(PokeTradeType.FixOT);
        private readonly PokeTradeHub<T> Hub;
        private readonly PokeTradeQueue<T> Seed = new(PokeTradeType.Seed);
        private readonly PokeTradeQueue<T> Trade = new(PokeTradeType.Specific);

        public TradeQueueManager(PokeTradeHub<T> hub)
        {
            Hub = hub;
            Info = new TradeQueueInfo<T>(hub);
            AllQueues = [Seed, Dump, Clone, FixOT, Trade, Batch];

            foreach (var q in AllQueues)
                q.Queue.Settings = hub.Config.Favoritism;
        }

        public void ClearAll()
        {
            foreach (var q in AllQueues)
                q.Clear();
        }

        public void Enqueue(PokeRoutineType type, PokeTradeDetail<T> detail, uint priority)
        {
            var queue = GetQueue(type);
            queue.Enqueue(detail, priority);
        }

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
            if (detail.TotalBatchTrades > 1)
            {
                LogUtil.LogInfo($"[Batch] {b.Connection.Name}: Starting batch trade {detail.UniqueTradeID} ({detail.BatchTradeNumber}/{detail.TotalBatchTrades})", nameof(TradeQueueManager<T>));
            }
            foreach (var f in Forwarders)
                f.Invoke(b, detail);
        }

        public void CompleteTrade(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail)
        {
            if (detail.TotalBatchTrades > 1)
            {
                LogUtil.LogInfo($"[Batch] {b.Connection.Name}: Completed batch trade {detail.UniqueTradeID} ({detail.BatchTradeNumber}/{detail.TotalBatchTrades})", nameof(TradeQueueManager<T>));
            }
            _batchTracker.CompleteBatchTrade(detail);
        }

        public bool TryDequeue(PokeRoutineType type, out PokeTradeDetail<T> detail, out uint priority, string botName)
        {
            detail = default!;
            priority = default;
            // First peek at the next trade
            var queue = GetQueue(type);
            if (!queue.TryPeek(out detail, out priority))
            {
                return false;
            }

            // For batch trades, check if this bot can process it
            if (detail.TotalBatchTrades > 1 && !_batchTracker.CanProcessBatchTrade(detail))
            {
                return false;
            }

            // Try to actually dequeue and claim the trade
            if (!queue.TryDequeue(out detail, out priority))
            {
                return false;
            }

            // For batch trades, try to claim it for this bot
            if (detail.TotalBatchTrades > 1 && !_batchTracker.TryClaimBatchTrade(detail, botName))
            {
                queue.Enqueue(detail, priority);
                return false;
            }

            if (detail.TotalBatchTrades > 1)
            {
                LogUtil.LogInfo($"[Queue] {botName}: Successfully claimed batch trade {detail.BatchTradeNumber}/{detail.TotalBatchTrades}", nameof(TradeQueueManager<T>));
            }

            return true;
        }

        private bool GetFlexDequeueWeighted(QueueSettings cfg, out PokeTradeDetail<T> detail, out uint priority, string botName)
        {
            PokeTradeQueue<T>? preferredQueue = null;
            long bestWeight = 0; // prefer higher weights
            uint bestPriority = uint.MaxValue; // prefer smaller
            PokeTradeDetail<T>? bestDetail = null;

            foreach (var q in AllQueues)
            {
                var peek = q.TryPeek(out var qDetail, out var qPriority);
                if (!peek)
                    continue;

                // Skip this queue if we can't process this batch trade
                if (qDetail.TotalBatchTrades > 1 && !_batchTracker.CanProcessBatchTrade(qDetail))
                    continue;

                // priority queue is a min-queue, so prefer smaller priorities
                if (qPriority > bestPriority)
                    continue;

                var count = q.Count;
                var time = qDetail.Time;
                var weight = cfg.GetWeight(count, time, q.Type);

                if (qPriority >= bestPriority && weight <= bestWeight)
                    continue;

                bestWeight = weight;
                bestPriority = qPriority;
                preferredQueue = q;
                bestDetail = qDetail;
            }

            if (preferredQueue == null || bestDetail == null)
            {
                detail = default!;
                priority = default;
                return false;
            }

            // Try to dequeue and claim the trade
            bool success = preferredQueue.TryDequeue(out detail, out priority);
            if (success && detail.TotalBatchTrades > 1)
            {
                if (!_batchTracker.TryClaimBatchTrade(detail, botName))
                {
                    // If we couldn't claim it, put it back in the queue
                    preferredQueue.Enqueue(detail, priority);
                    return false;
                }
            }

            return success;
        }

        public bool TryDequeueLedy(out PokeTradeDetail<T> detail, bool force = false)
        {
            detail = default!;
            var cfg = Hub.Config.Distribution;
            if (!cfg.DistributeWhileIdle && !force)
                return false;

            if (Hub.Ledy.Pool.Count == 0)
                return false;

            var random = Hub.Ledy.Pool.GetRandomPoke();
            var code = cfg.RandomCode ? Hub.Config.Trade.GetRandomTradeCode() : cfg.TradeCode;

            var lgcode = TradeSettings.GetRandomLGTradeCode(true);
            if (lgcode == null || lgcode.Count == 0)
            {
                lgcode = new List<Pictocodes> { Pictocodes.Pikachu, Pictocodes.Pikachu, Pictocodes.Pikachu };
            }
            var trainer = new PokeTradeTrainerInfo("Random Distribution");
            detail = new PokeTradeDetail<T>(random, trainer, PokeTradeHub<T>.LogNotifier, PokeTradeType.Random, code, false, lgcode);
            return true;
        }
    }
}
