using System;
using System.Collections.Concurrent;
using System.Linq;
using PKHeX.Core;
namespace SysBot.Pokemon
{
    /// <summary>
    /// Tracks and manages batch trade sequences to ensure sequential processing by a single bot.
    /// </summary>
    public class BatchTradeTracker<T> where T : PKM, new()
    {
        private readonly ConcurrentDictionary<ulong, (string BotName, int LastBatchNumber, int TotalBatchTrades)> _activeTrainersByBot = new();
        private readonly ConcurrentDictionary<ulong, DateTime> _lastTradeTime = new();
        private readonly TimeSpan _tradeTimeout = TimeSpan.FromMinutes(5);
        public bool CanProcessBatchTrade(PokeTradeDetail<T> trade)
        {
            if (trade.TotalBatchTrades <= 1)
                return true;
            CleanupStaleEntries();
            if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
                return trade.BatchTradeNumber == existing.LastBatchNumber + 1 &&
                       trade.TotalBatchTrades == existing.TotalBatchTrades;
            }
            return trade.BatchTradeNumber == 1;
        }
        public bool TryClaimBatchTrade(PokeTradeDetail<T> trade, string botName)
        {
            if (trade.TotalBatchTrades <= 1)
                return true;
            CleanupStaleEntries();
            if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
                if (trade.BatchTradeNumber == existing.LastBatchNumber + 1 &&
                    trade.TotalBatchTrades == existing.TotalBatchTrades)
                {
                    if (existing.BotName != botName)
                        return false;
                    return _activeTrainersByBot.TryUpdate(
                        trade.Trainer.ID,
                        (botName, trade.BatchTradeNumber, trade.TotalBatchTrades),
                        existing);
                }
                return false;
            }
            if (trade.BatchTradeNumber != 1)
                return false;
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
                _activeTrainersByBot.TryRemove(trade.Trainer.ID, out _);
                _lastTradeTime.TryRemove(trade.Trainer.ID, out _);
            }
            else if (_activeTrainersByBot.TryGetValue(trade.Trainer.ID, out var existing))
            {
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
