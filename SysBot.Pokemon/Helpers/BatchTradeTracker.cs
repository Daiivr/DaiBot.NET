using System;
using System.Collections.Concurrent;
using System.Linq;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class BatchTradeTracker<T> where T : PKM, new()
    {
        private readonly ConcurrentDictionary<ulong, (string BotName, int LastBatchNumber, int TotalBatchTrades)> _activeTrainersByBot = new();
        private readonly ConcurrentDictionary<ulong, DateTime> _lastTradeTime = new();
        private readonly TimeSpan _tradeTimeout = TimeSpan.FromMinutes(5);

        public bool CanProcessBatchTrade(PokeTradeDetail<T> trade)
        {
            if (trade.TotalBatchTrades <= 1)
                return true; // Not a batch trade

            CleanupStaleEntries();

            // Check if this trainer is already being served by a bot
            if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
                // Verify the sequence
                bool isNextInSequence = trade.BatchTradeNumber == existing.LastBatchNumber + 1;
                bool isSameTotalTrades = trade.TotalBatchTrades == existing.TotalBatchTrades;

                return isNextInSequence && isSameTotalTrades;
            }

            // Only allow starting from the first trade in a batch
            bool canStart = trade.BatchTradeNumber == 1;
            return canStart;
        }

        public bool TryClaimBatchTrade(PokeTradeDetail<T> trade, string botName)
        {
            if (trade.TotalBatchTrades <= 1)
                return true; // Not a batch trade

            CleanupStaleEntries();

            // Check if this trainer is already being served by a bot
            if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
                // If this is from the same batch sequence
                if (trade.BatchTradeNumber == existing.LastBatchNumber + 1 &&
                    trade.TotalBatchTrades == existing.TotalBatchTrades)
                {
                    if (existing.BotName != botName)
                        return false;

                    // Update the last batch number
                    return _activeTrainersByBot.TryUpdate(
                        trade.Trainer.ID,
                        (botName, trade.BatchTradeNumber, trade.TotalBatchTrades),
                        existing);
                }
                return false;
            }

            // Only allow claiming from the first trade
            if (trade.BatchTradeNumber != 1)
                return false;

            // Try to claim this trainer for this bot
            if (_activeTrainersByBot.TryAdd(trade.Trainer.ID, (botName, trade.BatchTradeNumber, trade.TotalBatchTrades)))
            {
                _lastTradeTime[trade.Trainer.ID] = DateTime.Now;
                return true;
            }

            return false;
        }

        public void CompleteBatchTrade(PokeTradeDetail<T> trade)
        {
            if (trade.TotalBatchTrades <= 1)
                return;

            _lastTradeTime[trade.Trainer.ID] = DateTime.Now;

            if (trade.BatchTradeNumber == trade.TotalBatchTrades)
            {
                // Last trade in batch - remove all tracking for this trainer
                _activeTrainersByBot.TryRemove(trade.Trainer.ID, out _);
                _lastTradeTime.TryRemove(trade.Trainer.ID, out _);
            }
            else if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
                // Update the last batch number after completing a trade
                _activeTrainersByBot.TryUpdate(
                    trade.Trainer.ID,
                    (existing.BotName, trade.BatchTradeNumber, trade.TotalBatchTrades),
                    existing);
            }
        }

        private void CleanupStaleEntries()
        {
            var now = DateTime.Now;
            var staleTrainers = _lastTradeTime
                .Where(x => now - x.Value > _tradeTimeout)
                .Select(x => x.Key)
                .ToList();

            foreach (var trainerId in staleTrainers)
            {
                _lastTradeTime.TryRemove(trainerId, out _);
                _activeTrainersByBot.TryRemove(trainerId, out _);
            }
        }
    }
}
