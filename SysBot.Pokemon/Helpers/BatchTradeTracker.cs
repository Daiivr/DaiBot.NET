using PKHeX.Core;
using System.Collections.Concurrent;
using System;
using System.Linq;

namespace SysBot.Pokemon.Helpers
{
    public class BatchTradeTracker<T> where T : PKM, new()
    {
        // Just track which bot is handling which batch
        private readonly ConcurrentDictionary<(ulong TrainerId, int UniqueTradeID), string> _activeBatches = new();
        private readonly TimeSpan _tradeTimeout = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<(ulong TrainerId, int UniqueTradeID), DateTime> _lastTradeTime = new();

        public bool CanProcessBatchTrade(PokeTradeDetail<T> trade)
        {
            if (trade.TotalBatchTrades <= 1)
                return true;

            CleanupStaleEntries();
            var key = (trade.Trainer.ID, trade.UniqueTradeID);

            // If _nobodyishome is handling this batch yet, allow it
            if (!_activeBatches.ContainsKey(key))
                return true;

            return true; // Allow all trades from this batch
        }

        public bool TryClaimBatchTrade(PokeTradeDetail<T> trade, string botName)
        {
            if (trade.TotalBatchTrades <= 1)
                return true;

            var key = (trade.Trainer.ID, trade.UniqueTradeID);

            // If we already have this batch, make sure it's the same bot
            if (_activeBatches.TryGetValue(key, out var existingBot))
            {
                _lastTradeTime[key] = DateTime.Now;
                return botName == existingBot;
            }

            // claim this batch
            if (_activeBatches.TryAdd(key, botName))
            {
                _lastTradeTime[key] = DateTime.Now;
                return true;
            }

            return false;
        }

        public void CompleteBatchTrade(PokeTradeDetail<T> trade)
        {
            if (trade.TotalBatchTrades <= 1)
                return;

            var key = (trade.Trainer.ID, trade.UniqueTradeID);
            _lastTradeTime[key] = DateTime.Now;

            // Only remove tracking when it's the last trade
            if (trade.BatchTradeNumber == trade.TotalBatchTrades)
            {
                _activeBatches.TryRemove(key, out _);
                _lastTradeTime.TryRemove(key, out _);
            }
        }

        private void CleanupStaleEntries()
        {
            var now = DateTime.Now;
            var staleKeys = _lastTradeTime
                .Where(x => now - x.Value > _tradeTimeout)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                _activeBatches.TryRemove(key, out _);
                _lastTradeTime.TryRemove(key, out _);
            }
        }
    }
}
