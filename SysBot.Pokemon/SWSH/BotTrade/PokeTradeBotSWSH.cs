using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class PokeTradeBotSWSH(PokeTradeHub<PK8> hub, PokeBotState config) : PokeRoutineExecutor8SWSH(config), ICountBot, ITradeBot
{
    public static ISeedSearchHandler<PK8> SeedChecker { get; set; } = new NoSeedSearchHandler<PK8>();

    private readonly TradeSettings TradeSettings = hub.Config.Trade;

    private readonly PokeTradeHub<PK8> Hub = hub ?? throw new ArgumentNullException(nameof(hub));

    private readonly TradeAbuseSettings AbuseSettings = hub.Config.TradeAbuse;

    public event EventHandler<Exception>? ConnectionError;

    public event EventHandler? ConnectionSuccess;

    private void OnConnectionError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    private void OnConnectionSuccess()
    {
        ConnectionSuccess?.Invoke(this, EventArgs.Empty);
    }

    public ICountSettings Counts => TradeSettings;

    /// <summary>
    /// Folder to dump received trade data to.
    /// </summary>
    /// <remarks>If null, will skip dumping.</remarks>
    private readonly IDumper DumpSetting = hub.Config.Folder;

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    // Cached offsets that stay the same per session.
    private ulong OverworldOffset;

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(hub.Config.Trade, token).ConfigureAwait(false);

            Log("Identificando los datos del entrenador de la consola host.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            OnConnectionSuccess();
            Log($"Iniciando el bucle principal {nameof(PokeTradeBotSWSH)}.");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"Finalizando el bucle {nameof(PokeTradeBotSWSH)}.");
        await HardStop().ConfigureAwait(false);
    }

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(new PokeTradeHubConfig(), t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);

        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("Reiniciando el bucle principal.");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    private async Task InnerLoop(SAV8SWSH sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.Idle => DoNothing(token),
                PokeRoutineType.SurpriseTrade => DoSurpriseTrades(sav, token),
                _ => DoTrades(sav, token),
            };
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (SocketException e)
            {
                if (e.StackTrace != null)
                    Connection.LogError(e.StackTrace);
                var attempts = hub.Config.Timings.MiscellaneousSettings.ReconnectAttempts;
                var delay = hub.Config.Timings.MiscellaneousSettings.ExtraReconnectDelay;
                var protocol = Config.Connection.Protocol;
                if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                    return;
            }
        }
    }

    private async Task DoNothing(CancellationToken token)
    {
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
        {
            if (waitCounter == 0)
                Log("No hay ninguna tarea asignada. Esperando la asignación de una nueva tarea.");
            waitCounter++;
            if (waitCounter % 10 == 0 && hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
    }

    private async Task DoTrades(SAV8SWSH sav, CancellationToken token)
    {
        var type = Config.CurrentRoutineType;
        int waitCounter = 0;
        await SetCurrentBox(0, token).ConfigureAwait(false);
        while (!token.IsCancellationRequested && Config.NextRoutineType == type)
        {
            var (detail, priority) = GetTradeData(type);
            if (detail is null)
            {
                await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                continue;
            }
            waitCounter = 0;

            detail.IsProcessing = true;
            string tradetype = $" ({detail.Type})";
            Log($"A partir del próximo {type}{tradetype} Bot Trade. Obteniendo datos...");
            hub.Config.Stream.StartTrade(this, detail, hub);
            hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            hub.Config.Stream.IdleAssets(this);
            Log("Nada que comprobar, esperando nuevos usuarios...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && hub.Config.AntiIdle)
            return Click(B, 1_000, token);
        return Task.Delay(1_000, token);
    }

    protected virtual (PokeTradeDetail<PK8>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        string botName = Connection.Name;

        // First check the specific type's queue
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority, botName))
        {
            return (detail, priority);
        }
        // If we're doing FlexTrade, also check the Batch queue
        if (type == PokeRoutineType.FlexTrade)
        {
            if (Hub.Queues.TryDequeue(PokeRoutineType.Batch, out detail, out priority, botName))
            {
                return (detail, priority);
            }
        }
        if (Hub.Queues.TryDequeueLedy(out detail))
        {
            return (detail, PokeTradePriorities.TierFree);
        }
        return (null, PokeTradePriorities.TierFree);
    }

    private void CleanupAllBatchTradesFromQueue(PokeTradeDetail<PK8> detail)
    {
        var result = Hub.Queues.Info.ClearTrade(detail.Trainer.ID);
        var batchQueue = Hub.Queues.GetQueue(PokeRoutineType.Batch);
        // Clear any remaining trades for this batch from the queue
        var remainingTrades = batchQueue.Queue.GetSnapshot()
            .Where(x => x.Value.Trainer.ID == detail.Trainer.ID &&
                       x.Value.UniqueTradeID == detail.UniqueTradeID)
            .ToList();
        foreach (var trade in remainingTrades)
        {
            batchQueue.Queue.Remove(trade.Value);
        }
        Log($"Se limpiaron los intercambios por lotes para TrainerID: {detail.Trainer.ID}");
    }
    private bool GetNextBatchTrade(PokeTradeDetail<PK8> currentTrade, out PokeTradeDetail<PK8>? nextDetail)
    {
        nextDetail = null;
        var batchQueue = Hub.Queues.GetQueue(PokeRoutineType.Batch);
        Log($"Buscando el próximo intercambio después de {currentTrade.BatchTradeNumber}/{currentTrade.TotalBatchTrades}.");
        // Get all trades for this user
        var userTrades = batchQueue.Queue.GetSnapshot()
            .Select(x => x.Value)
            .Where(x => x.Trainer.ID == currentTrade.Trainer.ID)
            .OrderBy(x => x.BatchTradeNumber)
            .ToList();
        // Log what we found
        foreach (var trade in userTrades)
        {
            Log($"Encontré un intercambio en la cola: {trade.BatchTradeNumber}/{trade.TotalBatchTrades} para el entrenador {trade.Trainer.TrainerName}.");
        }
        // Get the next sequential trade
        nextDetail = userTrades.FirstOrDefault(x => x.BatchTradeNumber == currentTrade.BatchTradeNumber + 1);
        if (nextDetail != null)
        {
            Log($"Seleccionado el siguiente intercambio {nextDetail.BatchTradeNumber}/{nextDetail.TotalBatchTrades}.");
            return true;
        }
        Log("No se encontraron más trades para este usuario");
        return false;
    }
    private async Task<PokeTradeResult> PerformBatchTrade(SAV8SWSH sav, PokeTradeDetail<PK8> poke, CancellationToken token)
    {
        // Update Barrier Settings
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        hub.Config.Stream.EndEnterCode(this);
        int completedTrades = 0;
        var startingDetail = poke;
        bool firstTrade = true;
        while (completedTrades < startingDetail.TotalBatchTrades)
        {
            var toSend = poke.TradeData;
            if (toSend.Species != 0)
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            if (firstTrade)
            {
                // Only do initial connection and code entry for the first trade
                await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
                if (await CheckIfSoftBanned(token).ConfigureAwait(false))
                    await UnSoftBan(token).ConfigureAwait(false);
                if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                {
                    await ExitTrade(true, token).ConfigureAwait(false);
                    return PokeTradeResult.RecoverStart;
                }
                Log("Abriendo el menú de Y-Comm.");
                await Click(Y, 2_000, token).ConfigureAwait(false);
                Log("Seleccionar Enlace Comercial.");
                await Click(A, 1_500, token).ConfigureAwait(false);
                Log("Seleccionar código de enlace comercial.");
                await Click(DDOWN, 500, token).ConfigureAwait(false);
                for (int i = 0; i < 2; i++)
                    await Click(A, 1_500, token).ConfigureAwait(false);
                // All other languages require an extra A press at this menu.
                if (GameLang != LanguageID.English && GameLang != LanguageID.Spanish)
                    await Click(A, 1_500, token).ConfigureAwait(false);
                // Loading Screen
                if (poke.Type != PokeTradeType.Random)
                    hub.Config.Stream.StartEnterCode(this);
                await Task.Delay(hub.Config.Timings.MiscellaneousSettings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);
                var code = poke.Code;
                Log($"Ingresando el código de enlace comercial: {code:0000 0000}...");
                await EnterLinkCode(code, hub.Config, token).ConfigureAwait(false);
                // Wait for Barrier to trigger all bots simultaneously.
                WaitAtBarrierIfApplicable(token);
                await Click(PLUS, 1_000, token).ConfigureAwait(false);
                hub.Config.Stream.EndEnterCode(this);
                // Confirming and return to overworld.
                var delay_count = 0;
                while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                {
                    if (delay_count++ >= 5)
                    {
                        // Too many attempts, recover out of the trade.
                        await ExitTrade(true, token).ConfigureAwait(false);
                        return PokeTradeResult.RecoverPostLinkCode;
                    }
                    for (int i = 0; i < 5; i++)
                        await Click(A, 0_800, token).ConfigureAwait(false);
                }
                firstTrade = false;
            }
            poke.TradeSearching(this);
            var partnerFound = await WaitForTradePartnerOffer(token).ConfigureAwait(false);
            if (!partnerFound)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ No se encontró ningún entrenador después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los intercambios restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.NoTrainerFound;
            }
            // Extra delay needed to open box properly 
            await Task.Delay(5_500 + hub.Config.Timings.MiscellaneousSettings.ExtraTimeOpenBox, token).ConfigureAwait(false);
            var trainerName = await GetTradePartnerName(TradeMethod.LinkTrade, token).ConfigureAwait(false);
            var trainerTID = await GetTradePartnerTID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
            var trainerSID = await GetTradePartnerSID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
            var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);
            RecordUtil<PokeTradeBotSWSH>.Record($"Iniciando\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
            Log($"Encontré un entrenador para intercambiar: {trainerName}-{trainerTID} (ID: {trainerNID})");
            poke.SendNotification(this, $"Entrenador encontrado: **{trainerName}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{trainerTID}__\n **SID**: __{trainerSID}__\n▲\n\n Esperando por un __Pokémon__...");
            var tradeCodeStorage = new TradeCodeStorage();
            var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);
            bool shouldUpdateOT = existingTradeDetails?.OT != trainerName;
            bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(trainerTID);
            bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(trainerSID);
            if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
            {
                string? ot = shouldUpdateOT ? trainerName : existingTradeDetails?.OT;
                int? tid = shouldUpdateTID ? int.Parse(trainerTID) : existingTradeDetails?.TID;
                int? sid = shouldUpdateSID ? int.Parse(trainerSID) : existingTradeDetails?.SID;
                if (ot != null && tid.HasValue && sid.HasValue)
                {
                    tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid.Value, sid.Value);
                }
            }
            var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, trainerName, AbuseSettings, token);
            if (partnerCheck != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ Actividad sospechosa detectada después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los intercambios restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return partnerCheck;
            }
            if (!await IsInBox(token).ConfigureAwait(false))
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ No pude entrar en la casilla después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los intercambios restantes.");
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverOpenBox;
            }
            if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
            {
                toSend = await ApplyAutoOT(toSend, trainerName, sav, token);
            }
            var offered = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            var oldEC = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 4, token).ConfigureAwait(false);
            if (offered == null)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ No se ofrecieron Pokémons después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los intercambios restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
            var trainer = new PartnerDataHolder(trainerNID, trainerName, trainerTID);
            var update = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, token).ConfigureAwait(false);
            if (update.check != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ La verificación falló después de la operación: {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los intercambios restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return update.check;
            }
            toSend = update.toSend;
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ La operación falló después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando las operaciones restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return tradeResult;
            }
            var received = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ La operación no se completó después del intercambio {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando las operaciones restantes.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
            // Trade was successful
            UpdateCountsAndExport(poke, received, toSend);
            LogSuccessfulTrades(poke, trainerNID, trainerName);
            completedTrades++;
            _batchTracker.AddReceivedPokemon(poke.Trainer.ID, received);
            if (completedTrades == startingDetail.TotalBatchTrades)
            {
                // Get all collected Pokemon before cleaning anything up
                var allReceived = _batchTracker.GetReceivedPokemon(poke.Trainer.ID);
                // First send notification that trades are complete
                poke.SendNotification(this, "✅ ¡Se han completado todos los intercambios por lotes! ¡Gracias por realizar el intercambio!");
                // Then finish each trade with the corresponding received Pokemon
                if (Hub.Config.Discord.ReturnPKMs)
                {
                    foreach (var pokemon in allReceived)
                    {
                        poke.TradeFinished(this, pokemon);
                    }
                }
                // cleanup
                Hub.Queues.CompleteTrade(this, poke);
                CleanupAllBatchTradesFromQueue(poke);
                _batchTracker.ClearReceivedPokemon(poke.Trainer.ID);
                break;
            }
            if (GetNextBatchTrade(poke, out var nextDetail))
            {
                if (nextDetail == null)
                {
                    poke.SendNotification(this, "⚠️ Error en secuencia de lotes. Fin de operaciones.");
                    await ExitTrade(false, token).ConfigureAwait(false);
                    return PokeTradeResult.Success;
                }
                poke.SendNotification(this, $"✅ Intercambio {completedTrades} completado! Preparando el siguiente Pokémon: ({nextDetail.BatchTradeNumber}/{nextDetail.TotalBatchTrades}). Por favor, espera en la pantalla de intercambio!");
                poke = nextDetail;
                await Click(A, 1_000, token).ConfigureAwait(false);
                if (poke.TradeData.Species != 0)
                {
                    await SetBoxPokemon(poke.TradeData, 0, 0, token, sav).ConfigureAwait(false);
                }
                continue;
            }
            poke.SendNotification(this, "⚠️ No se puede encontrar la siguiente operación en la secuencia. Se finalizará la operación por lotes.");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.Success;
        }
        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private async Task PerformTrade(SAV8SWSH sav, PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            if (detail.Type == PokeTradeType.Batch)
                result = await PerformBatchTrade(sav, detail, token).ConfigureAwait(false);
            else
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);

            if (result == PokeTradeResult.Success)
                return;
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            HandleAbortedTrade(detail, type, priority, result);
            throw;
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
        }

        HandleAbortedTrade(detail, type, priority, result);
    }

    private void HandleAbortedTrade(PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "<a:warning:1206483664939126795> Oops! Algo ocurrió. Intentemoslo una vez mas.");
        }
        else
        {
            detail.SendNotification(this, $"<a:warning:1206483664939126795> Oops! Algo ocurrió. Cancelando el trade: **{result.GetDescription()}**.");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task DoSurpriseTrades(SAV8SWSH sav, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.SurpriseTrade)
        {
            var pkm = hub.Ledy.Pool.GetRandomSurprise();
            await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
            var _ = await PerformSurpriseTrade(sav, pkm, token).ConfigureAwait(false);
        }
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8SWSH sav, PokeTradeDetail<PK8> poke, CancellationToken token)
    {
        // Update Barrier Settings
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
        hub.Config.Stream.EndEnterCode(this);

        if (await CheckIfSoftBanned(token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        var toSend = poke.TradeData;
        if (toSend.Species != 0)
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        while (await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
        {
            Log("Sigo buscando, restableciendo la posición del bot.");
            await ResetTradePosition(token).ConfigureAwait(false);
        }

        Log("Abriendo el menú de Y-Comm.");
        await Click(Y, 2_000, token).ConfigureAwait(false);

        Log("Seleccionando Comercio de enlaces.");
        await Click(A, 1_500, token).ConfigureAwait(false);

        Log("Seleccionando el código Link Trade.");
        await Click(DDOWN, 500, token).ConfigureAwait(false);

        for (int i = 0; i < 2; i++)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // All other languages require an extra A press at this menu.
        if (GameLang != LanguageID.English && GameLang != LanguageID.Spanish)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // Loading Screen
        if (poke.Type != PokeTradeType.Random)
            hub.Config.Stream.StartEnterCode(this);
        await Task.Delay(hub.Config.Timings.MiscellaneousSettings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        var code = poke.Code;
        Log($"Ingresando el código de enlace comercial: {code:0000 0000}...");
        await EnterLinkCode(code, hub.Config, token).ConfigureAwait(false);

        // Wait for Barrier to trigger all bots simultaneously.
        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);

        hub.Config.Stream.EndEnterCode(this);

        // Confirming and return to overworld.
        var delay_count = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            if (delay_count++ >= 5)
            {
                // Too many attempts, recover out of the trade.
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverPostLinkCode;
            }

            for (int i = 0; i < 5; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);
        }

        poke.TradeSearching(this);
        await Task.Delay(0_500, token).ConfigureAwait(false);

        // Wait for a Trainer...
        var partnerFound = await WaitForTradePartnerOffer(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
        {
            return PokeTradeResult.RoutineCancel;
        }
        if (!partnerFound)
        {
            await ResetTradePosition(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        // Select Pokémon
        // pkm already injected to b1s1
        await Task.Delay(5_500 + hub.Config.Timings.MiscellaneousSettings.ExtraTimeOpenBox, token).ConfigureAwait(false); // necessary delay to get to the box properly

        var trainerName = await GetTradePartnerName(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerTID = await GetTradePartnerTID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerSID = await GetTradePartnerSID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);
        RecordUtil<PokeTradeBotSWSH>.Record($"Iniciando\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        Log($"Encontré un entrenador para intercambiar: {trainerName}-{trainerTID} (ID: {trainerNID})");

        var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, trainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            await ExitSeedCheckTrade(token).ConfigureAwait(false);
            return partnerCheck;
        }

        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

        bool shouldUpdateOT = existingTradeDetails?.OT != trainerName;
        bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(trainerTID);
        bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(trainerSID);

        if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
        {
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, shouldUpdateOT ? trainerName : existingTradeDetails.OT, shouldUpdateTID ? int.Parse(trainerTID) : existingTradeDetails.TID, shouldUpdateSID ? int.Parse(trainerSID) : existingTradeDetails.SID);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
        }

        if (!await IsInBox(token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverOpenBox;
        }

        if (hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, trainerName, sav, token);
        }

        // Confirm Box 1 Slot 1
        if (poke.Type == PokeTradeType.Specific)
        {
            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
        }

        poke.SendNotification(this, $"Entrenador encontrado: **{trainerName}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{trainerTID}__\n **SID**: __{trainerSID}__\n▲\n\n Esperando por un __Pokémon__...");

        if (poke.Type == PokeTradeType.Dump)
            return await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);

        // Wait for User Input...
        var offered = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        var oldEC = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 4, token).ConfigureAwait(false);
        if (offered is null)
        {
            await ExitSeedCheckTrade(token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        if (poke.Type == PokeTradeType.Seed)
        {
            // Immediately exit, we aren't trading anything.
            return await EndSeedCheckTradeAsync(poke, offered, token).ConfigureAwait(false);
        }

        var trainer = new PartnerDataHolder(trainerNID, trainerName, trainerTID);
        (toSend, PokeTradeResult update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return update;
        }

        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        // Trade was Successful!
        var received = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);

        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log($"Intercambio no completado. El usuario no intercambió su Pokémon.");
            RecordUtil<PokeTradeBotSWSH>.Record($"Cancelado\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.ID}\t{toSend.Species}\t{toSend.EncryptionConstant:X8}\t{offered.Species}\t{offered.EncryptionConstant:X8}");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log($"Operación completada. Se recibió {GameInfo.GetStrings(1).Species[received.Species]} del usuario y se envió {GameInfo.GetStrings(1).Species[toSend.Species]}.");
        poke.TradeFinished(this, received);

        RecordUtil<PokeTradeBotSWSH>.Record($"Finalizado\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.ID}\t{toSend.Species}\t{toSend.EncryptionConstant:X8}\t{received.Species}\t{received.EncryptionConstant:X8}");

        // Only log if we completed the trade.
        UpdateCountsAndExport(poke, received, toSend);

        // Log for Trade Abuse tracking.
        LogSuccessfulTrades(poke, trainerNID, trainerName);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    protected virtual Task<bool> WaitForTradePartnerOffer(CancellationToken token)
    {
        Log("Esperando al entrenador...");
        return WaitForPokemonChanged(LinkTradePartnerPokemonOffset, hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000, 0_200, token);
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PK8> poke, PK8 received, PK8 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.CountStatsSettings.AddCompletedDistribution();
        else if (poke.Type == PokeTradeType.FixOT)
            counts.CountStatsSettings.AddCompletedFixOTs();
        else if (poke.Type == PokeTradeType.Clone)
            counts.CountStatsSettings.AddCompletedClones();
        else
            counts.CountStatsSettings.AddCompletedTrade();

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
        {
            var subfolder = poke.Type.ToString().ToLower();
            var service = poke.Notifier.GetType().ToString().ToLower();
            var tradedFolder = service.Contains("twitch") ? Path.Combine("traded", "twitch") : service.Contains("discord") ? Path.Combine("traded", "discord") : "traded";
            DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone or PokeTradeType.FixOT)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime; i++)
        {
            // If we are in a Trade Evolution/Pokédex Entry and the Trade Partner quits, we land on the Overworld
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerLeft;
            if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                return PokeTradeResult.SuspiciousActivity;
            await Click(A, 1_000, token).ConfigureAwait(false);

            // EC is detectable at the start of the animation.
            var newEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                await Task.Delay(25_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }

        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            return PokeTradeResult.TrainerLeft;

        return PokeTradeResult.Success;
    }

    protected virtual async Task<(PK8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, PK8 toSend, PartnerDataHolder partnerID, CancellationToken token)
    {
        return poke.Type switch
        {
            PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
            PokeTradeType.Clone => await HandleClone(sav, poke, offered, oldEC, token).ConfigureAwait(false),
            PokeTradeType.FixOT => await HandleFixOT(sav, poke, offered, partnerID, token).ConfigureAwait(false),
            _ => (toSend, PokeTradeResult.Success),
        };
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleClone(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, CancellationToken token)
    {
        if (hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, "¡Esto es lo que me mostraste!");

        var la = new LegalityAnalysis(offered);
        if (!la.Valid)
        {
            Log($"La solicitud de clonación (de {poke.Trainer.TrainerName}) ha detectado un Pokémon no válido: {GameInfo.GetStrings(1).Species[offered.Species]}.");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

            var report = la.Report();
            Log(report);
            poke.SendNotification(this, "<a:no:1206485104424128593> Este Pokémon no es __**legal**__ según los controles de legalidad de __PKHeX__. Tengo prohibido clonar esto. Cancelando trade...");
            poke.SendNotification(this, report);

            return (offered, PokeTradeResult.IllegalTrade);
        }

        var clone = offered.Clone();
        if (hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        poke.SendNotification(this, $"**<a:yes:1206485105674166292> He __clonado__ tu **{GameInfo.GetStrings(1).Species[clone.Species]}!**\nAhora __preciosa__ **B** para cancelar y luego seleccione un Pokémon que no quieras para reliazar el tradeo.");
        Log($"Cloné un {(Species)clone.Species}. Esperando que el usuario cambie su Pokémon...");

        // Separate this out from WaitForPokemonChanged since we compare to old EC from original read.
        var partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);

        if (!partnerFound)
        {
            poke.SendNotification(this, "**Porfavor cambia el pokemon ahora o cancelare el tradeo!!!**");

            // They get one more chance.
            partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);
        }

        var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (!partnerFound || pk2 == null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
        {
            Log("El socio comercial no cambió su Pokémon.");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
    {
        // Allow the trade partner to do a Ledy swap.
        var config = hub.Config.Distribution;
        var trade = hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
        if (trade != null)
        {
            if (trade.Type == LedyResponseType.AbuseDetected)
            {
                var msg = $"Se ha detectado a {partner.TrainerName} por abusar de las operaciones de Ledy.";
                if (AbuseSettings.EchoNintendoOnlineIDLedy)
                    msg += $"\nID: {partner.TrainerOnlineID}";
                if (!string.IsNullOrWhiteSpace(AbuseSettings.LedyAbuseEchoMention))
                    msg = $"{AbuseSettings.LedyAbuseEchoMention} {msg}";
                EchoUtil.Echo(msg);

                return (toSend, PokeTradeResult.SuspiciousActivity);
            }

            toSend = trade.Receive;
            poke.TradeData = toSend;

            poke.SendNotification(this, "<a:loading:1210133423050719283> Inyectando el Pokémon solicitado.");
            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        for (int i = 0; i < 5; i++)
        {
            if (await IsUserBeingShifty(poke, token).ConfigureAwait(false))
                return (toSend, PokeTradeResult.SuspiciousActivity);
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        return (toSend, PokeTradeResult.Success);
    }

    // For pointer offsets that don't change per session are accessed frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("Compensaciones de sesión de almacenamiento en caché...");
        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
    }

    protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    private async Task RestartGameSWSH(CancellationToken token)
    {
        await ReOpenGame(hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        int ctr = 0;
        var time = TimeSpan.FromSeconds(hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;
        var pkprev = new PK8();
        var bctr = 0;
        while (ctr < hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            var pk = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (pk == null || pk.Species < 1 || !pk.ChecksumValid || SearchUtil.HashByDetails(pk) == SearchUtil.HashByDetails(pkprev))
                continue;

            // Save the new Pokémon for comparison next round.
            pkprev = pk;

            // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
            if (DumpSetting.Dump)
            {
                var subfolder = detail.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, pk); // received
            }

            var la = new LegalityAnalysis(pk);
            var verbose = $"```{la.Report(true)}```";
            Log($"El Pokémon mostrado es: {(la.Valid ? "Válido" : "Inválido")}.");

            ctr++;
            var msg = hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"File {ctr}";

            // Extra information about trainer data for people requesting with their own trainer data.
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "Male" : "Female";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**Datos del entrenador**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}```";

            // Extra information for shiny eggs, because of people dumping to skip hatching.
            var eggstring = pk.IsEgg ? "Egg " : string.Empty;
            msg += pk.IsShiny ? $"\n**¡Este Pokémon {eggstring} es shiny!**" : string.Empty;
            detail.SendNotification(this, pk, msg);
        }

        Log($"Finalizó el ciclo de volcado después de procesar {ctr} Pokémon.");
        await ExitSeedCheckTrade(token).ConfigureAwait(false);
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"Dumped {ctr} Pokémon.");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank pk8
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> PerformSurpriseTrade(SAV8SWSH sav, PK8 pkm, CancellationToken token)
    {
        // General Bot Strategy:
        // 1. Inject to b1s1
        // 2. Send out Trade
        // 3. Clear received PKM to skip the trade animation
        // 4. Repeat

        // Inject to b1s1
        if (await CheckIfSoftBanned(token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        Log("Comenzando el próximo comercio sorpresa. Obteniendo datos...");
        await SetBoxPokemon(pkm, 0, 0, token, sav).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        if (await CheckIfSearchingForSurprisePartner(token).ConfigureAwait(false))
        {
            Log("Sigo buscando, restableciendo la posición del bot.");
            await ResetTradePosition(token).ConfigureAwait(false);
        }

        Log("Abriendo el menú de Y-Comm.");
        await Click(Y, 1_500, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        Log("Seleccionando Comercio Sorpresa.");
        await Click(DDOWN, 0_500, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        await Task.Delay(0_750, token).ConfigureAwait(false);

        if (!await IsInBox(token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverPostLinkCode;
        }

        Log($"Seleccionando Pokémon: {pkm.FileName}");

        // Box 1 Slot 1; no movement required.
        await Click(A, 0_700, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        Log("Confirmando...");
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await Click(A, 0_800, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        // Let Surprise Trade be sent out before checking if we're back to the Overworld.
        await Task.Delay(3_000, token).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverReturnOverworld;
        }

        // Wait 30 Seconds for Trainer...
        Log("Esperando un socio comercial sorpresa...");

        // Wait for an offer...
        var oldEC = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 4, token).ConfigureAwait(false);
        var partnerFound = await ReadUntilChanged(SurpriseTradeSearchOffset, oldEC, hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000, 0_200, false, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        if (!partnerFound)
        {
            await ResetTradePosition(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        // Let the game flush the results and de-register from the online surprise trade queue.
        await Task.Delay(7_000, token).ConfigureAwait(false);

        var TrainerName = await GetTradePartnerName(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
        var TrainerTID = await GetTradePartnerTID7(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
        var SurprisePoke = await ReadSurpriseTradePokemon(token).ConfigureAwait(false);

        Log($"Socio comercial sorpresa encontrado: {TrainerName}-{TrainerTID}, Pokémon: {(Species)SurprisePoke.Species}");

        // Clear out the received trade data; we want to skip the trade animation.
        // The box slot locks have been removed prior to searching.

        await Connection.WriteBytesAsync(BitConverter.GetBytes(SurpriseTradeSearch_Empty), SurpriseTradeSearchOffset, token).ConfigureAwait(false);
        await Connection.WriteBytesAsync(PokeTradeBotUtil.EMPTY_SLOT, SurpriseTradePartnerPokemonOffset, token).ConfigureAwait(false);

        // Let the game recognize our modifications before finishing this loop.
        await Task.Delay(5_000, token).ConfigureAwait(false);

        // Clear the Surprise Trade slot locks! We'll skip the trade animation and reuse the slot on later loops.
        // Write 8 bytes of FF to set both Int32's to -1. Regular locks are [Box32][Slot32]

        await Connection.WriteBytesAsync(BitConverter.GetBytes(ulong.MaxValue), SurpriseTradeLockBox, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            Log("¡Negocio completo!");
        else
            await ExitTrade(true, token).ConfigureAwait(false);

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, "surprise", SurprisePoke);
        TradeSettings.CountStatsSettings.AddCompletedSurprise();

        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> EndSeedCheckTradeAsync(PokeTradeDetail<PK8> detail, PK8 pk, CancellationToken token)
    {
        await ExitSeedCheckTrade(token).ConfigureAwait(false);

        detail.TradeFinished(this, pk);

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, "seed", pk);

        // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
#pragma warning disable 4014
        Task.Run(() =>
        {
            try
            {
                ReplyWithSeedCheckResults(detail, pk);
            }
            catch (Exception ex)
            {
                detail.SendNotification(this, $"No se pueden calcular las semillas: {ex.Message}\r\n{ex.StackTrace}");
            }
        }, token);
#pragma warning restore 4014

        TradeSettings.CountStatsSettings.AddCompletedSeedCheck();

        return PokeTradeResult.Success;
    }

    private void ReplyWithSeedCheckResults(PokeTradeDetail<PK8> detail, PK8 result)
    {
        detail.SendNotification(this, "Calculando tu(s) semilla(s)...");

        if (result.IsShiny)
        {
            Log("¡El Pokémon ya es shiny!"); // Do not bother checking for next shiny frame
            detail.SendNotification(this, "¡Este Pokémon ya es shiny! No se realizó el cálculo de la semilla de incursión.");

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, "seed", result);

            detail.TradeFinished(this, result);
            return;
        }

        SeedChecker.CalculateAndNotify(result, detail, hub.Config.SeedCheckSWSH, this);
        Log("Cálculo de semillas completado.");
    }

    private void WaitAtBarrierIfApplicable(CancellationToken token)
    {
        if (!ShouldWaitAtBarrier)
            return;
        var opt = hub.Config.Distribution.SynchronizeBots;
        if (opt == BotSyncOption.NoSync)
            return;

        var timeoutAfter = hub.Config.Distribution.SynchronizeTimeout;
        if (FailedBarrier == 1) // failed last iteration
            timeoutAfter *= 2; // try to re-sync in the event things are too slow.

        var result = hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

        if (result)
        {
            FailedBarrier = 0;
            return;
        }

        FailedBarrier++;
        Log($"Se agotó el tiempo de espera de sincronización de barrera después de {timeoutAfter} segundos. Continuo.");
    }

    /// <summary>
    /// Checks if the barrier needs to get updated to consider this bot.
    /// If it should be considered, it adds it to the barrier if it is not already added.
    /// If it should not be considered, it removes it from the barrier if not already removed.
    /// </summary>
    private void UpdateBarrier(bool shouldWait)
    {
        if (ShouldWaitAtBarrier == shouldWait)
            return; // no change required

        ShouldWaitAtBarrier = shouldWait;
        if (shouldWait)
        {
            hub.BotSync.Barrier.AddParticipant();
            Log($"Se unió a la barrera. Conteo: {hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            hub.BotSync.Barrier.RemoveParticipant();
            Log($"Dejó la barrera. Conteo: {hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    private async Task<bool> WaitForPokemonChanged(uint offset, int waitms, int waitInterval, CancellationToken token)
    {
        // check EC and checksum; some pkm may have same EC if shown sequentially
        var oldEC = await Connection.ReadBytesAsync(offset, 8, token).ConfigureAwait(false);
        return await ReadUntilChanged(offset, oldEC, waitms, waitInterval, false, token).ConfigureAwait(false);
    }

    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("Comportamiento inesperado, recuperando posición.");

        int attempts = 0;
        int softBanAttempts = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            var screenID = await GetCurrentScreen(token).ConfigureAwait(false);
            if (screenID == CurrentScreen_Softban)
            {
                softBanAttempts++;
                if (softBanAttempts > 10)
                    await RestartGameSWSH(token).ConfigureAwait(false);
            }

            attempts++;
            if (attempts >= 15)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }
    }

    private async Task ExitSeedCheckTrade(CancellationToken token)
    {
        // Seed Check Bot doesn't show anything, so it can skip the first B press.
        int attempts = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            attempts++;
            if (attempts >= 15)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }

        await Task.Delay(3_000, token).ConfigureAwait(false);
    }

    private async Task ResetTradePosition(CancellationToken token)
    {
        Log("Restableciendo la posición del bot.");

        // Shouldn't ever be used while not on overworld.
        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await ExitTrade(true, token).ConfigureAwait(false);

        // Ensure we're searching before we try to reset a search.
        if (!await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
            return;

        await Click(Y, 2_000, token).ConfigureAwait(false);
        for (int i = 0; i < 5; i++)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // Extra A press for Japanese.
        if (GameLang == LanguageID.Japanese)
            await Click(A, 1_500, token).ConfigureAwait(false);
        await Click(B, 1_500, token).ConfigureAwait(false);
        await Click(B, 1_500, token).ConfigureAwait(false);
    }

    private async Task<bool> CheckIfSearchingForLinkTradePartner(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(LinkTradeSearchingOffset, 1, token).ConfigureAwait(false);
        return data[0] == 1; // changes to 0 when found
    }

    private async Task<bool> CheckIfSearchingForSurprisePartner(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0) == SurpriseTradeSearch_Searching;
    }

    private async Task<string> GetTradePartnerName(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerNameOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 26, token).ConfigureAwait(false);
        return StringConverter8.GetString(data);
    }

    private async Task<string> GetTradePartnerTID7(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerTIDSIDOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false);

        var tidsid = BitConverter.ToUInt32(data, 0);
        return $"{tidsid % 1_000_000:000000}";
    }

    // Thanks Secludely https://github.com/Secludedly/ZE-FusionBot/commit/f064d9eaf11ba2b2a0a79fa4c7ec5bf6dacf780c
    private async Task<string> GetTradePartnerSID7(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerTIDSIDOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false);

        var tidsid = BitConverter.ToUInt32(data, 0);
        return $"{tidsid / 1_000_000:0000}";
    }

    public async Task<ulong> GetTradePartnerNID(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(LinkTradePartnerNIDOffset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt64(data, 0);
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleFixOT(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PartnerDataHolder partner, CancellationToken token)
    {
        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, "¡Esto es lo que me mostraste!");

        var adOT = TradeExtensions<PK8>.HasAdName(offered, out _);
        var laInit = new LegalityAnalysis(offered);
        if (!adOT && laInit.Valid)
        {
            poke.SendNotification(this, "<a:warning:1206483664939126795> No se detectó ningún anuncio en Apodo ni OT, y el Pokémon es legal. Saliendo del comercio.");
            return (offered, PokeTradeResult.TrainerRequestBad);
        }

        var clone = offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        string shiny = string.Empty;
        if (!TradeExtensions<PK8>.ShinyLockCheck(offered.Species, TradeExtensions<PK8>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
            shiny = $"\nShiny: {(offered.ShinyXor == 0 ? "Square" : offered.IsShiny ? "Star" : "No")}";
        else shiny = "\nShiny: No";

        var name = partner.TrainerName;
        var ball = $"\n{(Ball)offered.Ball}";
        var extraInfo = $"OT: {name}{ball}{shiny}";
        var set = ShowdownParsing.GetShowdownText(offered).Split('\n').ToList();
        var shinyRes = set.Find(x => x.Contains("Shiny"));
        if (shinyRes != null)
            set.Remove(shinyRes);
        set.InsertRange(1, extraInfo.Split('\n'));

        if (!laInit.Valid)
        {
            Log($"La solicitud de reparación de OT ha detectado un Pokémon ilegal de {name}: {(Species)offered.Species}");
            var report = laInit.Report();
            Log(laInit.Report());
            poke.SendNotification(this, $"<a:warning:1206483664939126795> **Los Pokémon mostrados no son legales. Intentando regenerar...**\n\n```{report}```");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
        }

        if (clone.FatefulEncounter)
        {
            clone.SetDefaultNickname(laInit);
            var info = new SimpleTrainerInfo { Gender = clone.OriginalTrainerGender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 8 };
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OriginalTrainerName == clone.OriginalTrainerName).ToList();
            if (mg.Count > 0)
                clone = TradeExtensions<PK8>.CherishHandler(mg.First(), info);
            else clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }
        else
        {
            clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }

        clone = (PK8)TradeExtensions<PK8>.TrashBytes(clone, new LegalityAnalysis(clone));
        clone.ResetPartyStats();
        var la = new LegalityAnalysis(clone);
        if (!la.Valid)
        {
            poke.SendNotification(this, "<a:no:1206485104424128593> Este Pokémon no es __**legal**__ según los controles de legalidad de __PKHeX__. No pude arreglar esto. Cancelando trade...");
            return (clone, PokeTradeResult.IllegalTrade);
        }

        poke.SendNotification(this, $"{(!laInit.Valid ? "**Legalizado" : "**Arreglado Nickname/OT para")} {(Species)clone.Species}**!");
        Log($"{(!laInit.Valid ? "Legalizado" : "Arreglado Nickname/OT para")} {(Species)clone.Species}!");

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);
        poke.SendNotification(this, "¡Ahora confirma el intercambio!");

        await Task.Delay(6_000, token).ConfigureAwait(false);
        var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 1_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
        bool changed = pk2 == null || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
        if (changed)
        {
            Log($"{name} cambió el Pokémon mostrado ({(Species)clone.Species}){(pk2 != null ? $" a {(Species)pk2.Species}" : "")}");
            poke.SendNotification(this, "**¡Libera los Pokémon mostrados originalmente, por favor!**");
            var timer = 10_000;
            while (changed)
            {
                pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 2_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
                changed = pk2 == null || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                if (timer <= 0)
                    break;
            }
        }

        if (changed)
        {
            poke.SendNotification(this, "Pokémon fue intercambiado y no vuelto a cambiar. Saliendo del comercio.");
            Log("El socio comercial no quiso despedir su ad-mon.");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_500, token).ConfigureAwait(false);
        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<PK8> ApplyAutoOT(PK8 toSend, string trainerName, SAV8SWSH sav, CancellationToken token)
    {
        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("Rastreador de casa detectado. No se puede aplicar Auto OT.");
            return toSend;
        }

        // Current handler cannot be past gen OT
        if (toSend.Generation != toSend.Format)
        {
            Log("No se pueden aplicar los detalles del socio: el dueño actual no puede ser de diferente generación OT.");
            return toSend;
        }
        var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
        var tidsid = BitConverter.ToUInt32(data, 0);
        var cln = toSend.Clone();
        cln.OriginalTrainerGender = data[6];
        cln.TrainerTID7 = tidsid % 1_000_000;
        cln.TrainerSID7 = tidsid / 1_000_000;
        cln.Language = data[5];
        cln.OriginalTrainerName = trainerName;
        ClearOTTrash(cln, trainerName);
        if (!toSend.IsNicknamed)
            cln.ClearNickname();

        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();

        var tradeswsh = new LegalityAnalysis(cln);
        if (tradeswsh.Valid)
        {
            Log("Pokémon es válido con la información del socio comercial aplicada. Intercambiando detalles.");
            await SetBoxPokemon(cln, 0, 0, token, sav).ConfigureAwait(false);
            return cln;
        }
        else
        {
            Log("Pokémon no es válido después de usar la información del socio comercial.");
            return toSend;
        }
    }

    private static void ClearOTTrash(PK8 pokemon, string trainerName)
    {
        Span<byte> trash = pokemon.OriginalTrainerTrash;
        trash.Clear();
        int maxLength = trash.Length / 2;
        int actualLength = Math.Min(trainerName.Length, maxLength);
        for (int i = 0; i < actualLength; i++)
        {
            char value = trainerName[i];
            trash[i * 2] = (byte)value;
            trash[(i * 2) + 1] = (byte)(value >> 8);
        }
        if (actualLength < maxLength)
        {
            trash[actualLength * 2] = 0x00;
            trash[(actualLength * 2) + 1] = 0x00;
        }
    }
}
