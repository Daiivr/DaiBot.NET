using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using SysBot.Base.Util;
using SysBot.Pokemon.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.BasePokeDataOffsetsBS;

namespace SysBot.Pokemon;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PokeTradeBotBS : PokeRoutineExecutor8BS, ICountBot, ITradeBot, IDisposable
{
    private readonly PokeTradeHub<PB8> Hub;
    private readonly TradeAbuseSettings AbuseSettings;
    private readonly IDumper DumpSetting;
    private readonly TradeSettings TradeSettings;

    // Cached offsets that stay the same per session.
    private ulong BoxStartOffset;

    // Track the last Pokémon we were offered since it persists between trades.
    private byte[] lastOffered = new byte[8];

    private ulong LinkTradePokemonOffset;

    private ulong SoftBanOffset;

    private ulong UnionGamingOffset;

    private ulong UnionTalkingOffset;

    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? ConnectionSuccess;

    public ICountSettings Counts => TradeSettings;

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    private bool _disposed = false;

    public PokeTradeBotBS(PokeTradeHub<PB8> hub, PokeBotState config) : base(config)
    {
        Hub = hub;
        AbuseSettings = Hub.Config.TradeAbuse;
        DumpSetting = Hub.Config.Folder;
        TradeSettings = Hub.Config.Trade;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                // Unsubscribe event handlers
                ConnectionError = null;
                ConnectionSuccess = null;
            }

            // Dispose unmanaged resources if any

            _disposed = true;
        }
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PokeTradeBotBS()
    {
        Dispose(false);
    }

    public override async Task HardStop()
    {
        UpdateBarrier(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
        Dispose();
    }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("Identificando los datos del entrenador de la consola host.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);

            await RestartGameIfCantLeaveUnionRoom(token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            OnConnectionSuccess();
            Log($"Iniciando el bucle principal {nameof(PokeTradeBotBS)}.");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"Finalizando el bucle {nameof(PokeTradeBotBS)}.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(Hub.Config, t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("Restarting the main loop.");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    protected virtual async Task<(PB8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partnerID, CancellationToken token)
    {
        if (token.IsCancellationRequested) return (toSend, PokeTradeResult.RoutineCancel);

        return poke.Type switch
        {
            PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
            PokeTradeType.FixOT => await HandleFixOT(sav, poke, offered, partnerID, token).ConfigureAwait(false),
            _ => (toSend, PokeTradeResult.Success),
        };
    }

    protected virtual (PokeTradeDetail<PB8>? detail, uint priority) GetTradeData(PokeRoutineType type)
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

    protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    private static void ClearOTTrash(PB8 pokemon, string trainerName)
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

    private static ulong GetFakeNID(string trainerName, uint trainerID)
    {
        var nameHash = trainerName.GetHashCode();
        return ((ulong)trainerID << 32) | (uint)nameHash;
    }

    private async Task<PB8> ApplyAutoOT(PB8 toSend, SAV8BS sav, string tradePartner, uint trainerTID7, uint trainerSID7, CancellationToken token)
    {
        if (token.IsCancellationRequested) return toSend;

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("Rastreador HOME detectado. No se puede aplicar Auto OT.");
            return toSend;
        }
        // Current handler cannot be past gen OT
        if (toSend.Generation != toSend.Format)
        {
            Log("No se pueden aplicar los detalles del entrenador: el dueño actual no puede ser de diferente generación OT.");
            return toSend;
        }
        var cln = toSend.Clone();
        cln.TrainerTID7 = trainerTID7;
        cln.TrainerSID7 = trainerSID7;
        cln.OriginalTrainerName = tradePartner;
        ClearOTTrash(cln, tradePartner);
        if (!toSend.IsNicknamed)
            cln.ClearNickname();
        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);
        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();
        var tradeBS = new LegalityAnalysis(cln);
        if (tradeBS.Valid)
        {
            Log("El Pokémon es válido con la información del entrenador comercial aplicada. Intercambiando detalles.");
            await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            return cln;
        }
        else
        {
            Log("El Pokémon no es válido después de usar la información del entrenador comercial.");
            await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            return toSend;
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < Hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime; i++)
        {
            if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

            if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                return PokeTradeResult.SuspiciousActivity;

            // We're no longer talking, so they probably quit on us.
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerTooSlow;
            await Click(A, 1_000, token).ConfigureAwait(false);

            // EC is detectable at the start of the animation.
            var newEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                await Task.Delay(25_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }

        // If we don't detect a B1S1 change, the trade didn't go through in that time.
        return PokeTradeResult.TrainerTooSlow;
    }

    private async Task DoNothing(CancellationToken token)
    {
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
        {
            if (waitCounter == 0)
                Log("Ninguna tarea asignada. Esperando nueva asignación de tarea.");
            waitCounter++;
            if (waitCounter % 10 == 0 && Hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
    }

    private void CleanupAllBatchTradesFromQueue(PokeTradeDetail<PB8> detail)
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

        Log($"✅ Se han limpiado los intercambios por lotes para TrainerID: {detail.Trainer.ID}, UniqueTradeID: {detail.UniqueTradeID}");
    }

    private async Task DoTrades(SAV8BS sav, CancellationToken token)
    {
        var type = Config.CurrentRoutineType;
        int waitCounter = 0;
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
            if (detail.Type != PokeTradeType.Random || !Hub.Config.Distribution.RemainInUnionRoomBDSP)
                await RestartGameIfCantLeaveUnionRoom(token).ConfigureAwait(false);

            string tradetype = $" ({detail.Type})";
            Log($"Empezando el próximo intercambio de bots de {type}{tradetype} Obteniendo datos...");
            await Task.Delay(500, token).ConfigureAwait(false);
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            try
            {
                await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
            }
            catch (SocketException socket)
            {
                Log(socket.Message);
                await HandleAbortedBatchTrade(detail, type, priority, PokeTradeResult.ExceptionConnection, token).ConfigureAwait(false);
                throw;
            }
            catch (Exception e)
            {
                Log(e.Message);
                await HandleAbortedBatchTrade(detail, type, priority, PokeTradeResult.ExceptionInternal, token).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> EnsureOutsideOfUnionRoom(CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;

        if (!await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            return true;

        if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
            return false;
        if (!await ExitUnionRoomToOverworld(token).ConfigureAwait(false))
            return false;
        return true;
    }

    private async Task<bool> EnterUnionRoomWithCode(PokeTradeType tradeType, int tradeCode, CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;

        // Already in Union Room.
        if (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            return true;

        // Open y-comm and select global room
        await Click(Y, 1_000 + Hub.Config.Timings.MiscellaneousSettings.ExtraTimeOpenYMenu, token).ConfigureAwait(false);
        await Click(DRIGHT, 0_400, token).ConfigureAwait(false);

        // French has one less menu
        if (GameLang is not LanguageID.French)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        }

        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);

        // Japanese has one extra menu
        if (GameLang is LanguageID.Japanese)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        }

        await Click(A, 1_000, token).ConfigureAwait(false); // Would you like to enter? Screen

        Log("Seleccionando la sala de códigos de enlace.");

        // Link code selection index
        await Click(DDOWN, 0_200, token).ConfigureAwait(false);
        await Click(DDOWN, 0_200, token).ConfigureAwait(false);

        Log("Conectándome a internet");
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 2_000, 0, token).ConfigureAwait(false);

        // Extra menus.
        if (GameLang is LanguageID.German or LanguageID.Italian or LanguageID.Korean)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 0_750, 0, token).ConfigureAwait(false);
        }

        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);

        // Would you like to save your adventure so far?
        await Click(A, 0_500, token).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);

        Log("Guardando...");

        // Agree and save the game.
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 6_500, 0, token).ConfigureAwait(false);

        if (tradeType != PokeTradeType.Random)
            Hub.Config.Stream.StartEnterCode(this);
        Log($"Introduciendo el código de intercambio de enlace: {tradeCode:0000 0000}");
        await EnterLinkCode(tradeCode, Hub.Config, token).ConfigureAwait(false);

        // Wait for Barrier to trigger all bots simultaneously.
        WaitAtBarrierIfApplicable(token);
        if (token.IsCancellationRequested) return false;

        await Click(PLUS, 0_600, token).ConfigureAwait(false);
        Hub.Config.Stream.EndEnterCode(this);
        Log("Entrando en la Sala Unión.");

        // Wait until we're past the communication message.
        int tries = 100;
        while (!await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            if (token.IsCancellationRequested) return false;

            await Click(A, 0_300, token).ConfigureAwait(false);

            if (--tries < 1)
                return false;
        }

        await Task.Delay(1_300 + Hub.Config.Timings.MiscellaneousSettings.ExtraTimeJoinUnionRoom, token).ConfigureAwait(false);

        return true; // We've made it into the room and are ready to request.
    }

    private async Task<bool> ExitBoxToUnionRoom(CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;

        if (await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
        {
            Log("Saliendo de la caja...");
            int tries = 30;
            while (await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested) return false;

                await Click(B, 0_500, token).ConfigureAwait(false);
                if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                    break;
                await Click(DUP, 0_200, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);

                // Keeps regular quitting a little faster, only need this for trade evolutions + moves.
                if (tries < 10)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await Click(B, 0_500, token).ConfigureAwait(false);
                tries--;
                if (tries < 0)
                    return false;
            }
        }
        await Task.Delay(2_000, token).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ExitUnionRoomToOverworld(CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;

        if (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            Log("Saliendo de la Sala Unión...");
            for (int i = 0; i < 3; ++i)
                await Click(B, 0_200, token).ConfigureAwait(false);

            await Click(Y, 1_000, token).ConfigureAwait(false);
            await Click(DDOWN, 0_200, token).ConfigureAwait(false);
            for (int i = 0; i < 3; ++i)
                await Click(A, 0_400, token).ConfigureAwait(false);

            int tries = 10;
            while (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested) return false;

                await Task.Delay(0_400, token).ConfigureAwait(false);
                tries--;
                if (tries < 0)
                    return false;
            }
            await Task.Delay(3_000 + Hub.Config.Timings.MiscellaneousSettings.ExtraTimeLeaveUnionRoom, token).ConfigureAwait(false);
        }
        return true;
    }

    private async Task<TradePartnerBS> GetTradePartnerInfo(CancellationToken token)
    {
#pragma warning disable CS8603 // Possible null reference return.
        if (token.IsCancellationRequested) return null;
#pragma warning restore CS8603 // Possible null reference return.

        var id = await SwitchConnection.PointerPeek(4, Offsets.LinkTradePartnerIDPointer, token).ConfigureAwait(false);
        var name = await SwitchConnection.PointerPeek(TradePartnerBS.MaxByteLengthStringObject, Offsets.LinkTradePartnerNamePointer, token).ConfigureAwait(false);
        return new TradePartnerBS(id, name);
    }

    private void HandleAbortedTrade(PokeTradeDetail<PB8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "<a:warning:1206483664939126795> Oops! Algo ocurrió. Intentemoslo una vez mas.");
        }
        else
        {
            detail.SendNotification(this, $"<a:warning:1206483664939126795> Oops! Algo ocurrió. Cancelando el trade: **{result.GetDescription()}**.");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task<(PB8 toSend, PokeTradeResult check)> HandleFixOT(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PartnerDataHolder partner, CancellationToken token)
    {
        if (token.IsCancellationRequested) return (offered, PokeTradeResult.RoutineCancel);

        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, $"¡Aqui esta lo que me mostraste - {GameInfo.GetStrings(1).Species[offered.Species]}");

        var adOT = TradeExtensions<PB8>.HasAdName(offered, out _);
        var laInit = new LegalityAnalysis(offered);
        if (!adOT && laInit.Valid)
        {
            poke.SendNotification(this, "<a:no:1206485104424128593> No se detectó ningún anuncio en Apodo ni OT, y el Pokémon es legal. Saliendo del comercio.");
            return (offered, PokeTradeResult.TrainerRequestBad);
        }

        var clone = (PB8)offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        string shiny = string.Empty;
        if (!TradeExtensions<PB8>.ShinyLockCheck(offered.Species, TradeExtensions<PB8>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
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
            Log($"La solicitud de FixOT ha detectado un Pokémon ilegal de {name}: {(Species)offered.Species}");
            var report = laInit.Report();
            Log(laInit.Report());
            poke.SendNotification(this, $"<a:no:1206485104424128593> **Los Pokémon mostrados no son legales. Intentando regenerar...**\n\n```{report}```");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
        }

        if (clone.FatefulEncounter)
        {
            clone.SetDefaultNickname(laInit);
            var info = new SimpleTrainerInfo { Gender = clone.OriginalTrainerGender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 8 };
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OriginalTrainerName == clone.OriginalTrainerName).ToList();
            if (mg.Count > 0)
                clone = TradeExtensions<PB8>.CherishHandler(mg.First(), info);
            else clone = (PB8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }
        else
        {
            clone = (PB8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }

        clone = (PB8)TradeExtensions<PB8>.TrashBytes(clone, new LegalityAnalysis(clone));
        clone.ResetPartyStats();
        var la = new LegalityAnalysis(clone);
        if (!la.Valid)
        {
            poke.SendNotification(this, "<a:warning:1206483664939126795> Este Pokémon no es __**legal**__ según los controles de legalidad de __PKHeX__. No pude arreglar esto. Cancelando trade...");
            return (clone, PokeTradeResult.IllegalTrade);
        }

        TradeExtensions<PB8>.HasAdName(offered, out string detectedAd);
        poke.SendNotification(this, $"{(!laInit.Valid ? "**Legalizado" : "**Arreglado Nickname/OT para")} {(Species)clone.Species}** (Anuncio encontrado: {detectedAd})! ¡Ahora confirma el intercambio!");
        Log($"{(!laInit.Valid ? "Legalizado" : "Arreglado Nickname/OT para")} {(Species)clone.Species}!");

        await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);
        poke.SendNotification(this, "¡Ahora confirma el intercambio!");
        await Click(A, 0_800, token).ConfigureAwait(false);
        await Click(A, 6_000, token).ConfigureAwait(false);

        var pk2 = await ReadPokemon(LinkTradePokemonOffset, token).ConfigureAwait(false);
        var comp = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);
        bool changed = pk2 == null || !comp.SequenceEqual(lastOffered) || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
        if (changed)
        {
            Log($"{name} ha cambiado el Pokémon ({(Species)clone.Species}){(pk2 != null ? $" a {(Species)pk2.Species}" : "")}");
            poke.SendNotification(this, "**¡Libera los Pokémon mostrados originalmente, por favor!**");

            bool verify = await ReadUntilChanged(LinkTradePokemonOffset, comp, 10_000, 0_200, false, true, token).ConfigureAwait(false);
            if (verify)
                verify = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 5_000, 0_200, true, true, token).ConfigureAwait(false);
            changed = !verify && (pk2 == null || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName);
        }

        // Update the last Pokémon they showed us.
        lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);

        if (changed)
        {
            poke.SendNotification(this, "<a:warning:1206483664939126795> El Pokémon fue cambiado y no vuelto a cambiar. Saliendo del comercio.");
            Log("El entrenador no se quiso despedir de su ad-mon.");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_500, token).ConfigureAwait(false);
        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<(PB8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partner, CancellationToken token)
    {
        if (token.IsCancellationRequested) return (toSend, PokeTradeResult.RoutineCancel);

        // Allow the trade partner to do a Ledy swap.
        var config = Hub.Config.Distribution;
        var trade = Hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
        if (trade != null)
        {
            if (trade.Type == LedyResponseType.AbuseDetected)
            {
                var msg = $"<a:warning:1206483664939126795> Encontrado **{partner.TrainerName}** por abusar de los intercambios de Ledy.";
                EchoUtil.Echo(msg);

                return (toSend, PokeTradeResult.SuspiciousActivity);
            }

            toSend = trade.Receive;
            poke.TradeData = toSend;

            poke.SendNotification(this, "<a:loading:1210133423050719283> Inyectando el Pokémon solicitado.");
            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        for (int i = 0; i < 5; i++)
        {
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        return (toSend, PokeTradeResult.Success);
    }

    // These don't change per session, and we access them frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        Log("Offsets de la sesión en caché...");
        BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        UnionGamingOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkIsGamingPointer, token).ConfigureAwait(false);
        UnionTalkingOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkIsTalkingPointer, token).ConfigureAwait(false);
        SoftBanOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkPenaltyPointer, token).ConfigureAwait(false);
    }

    private async Task InnerLoop(SAV8BS sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.Idle => DoNothing(token),
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
                var attempts = Hub.Config.Timings.MiscellaneousSettings.ReconnectAttempts;
                var delay = Hub.Config.Timings.MiscellaneousSettings.ExtraReconnectDelay;
                var protocol = Config.Connection.Protocol;
                if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                    return;
            }
        }
    }

    private void OnConnectionError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    private void OnConnectionSuccess()
    {
        ConnectionSuccess?.Invoke(this, EventArgs.Empty);
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8BS sav, PokeTradeDetail<PB8> poke, CancellationToken token)
    {
        if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

        // Update Barrier Settings
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        var distroRemainInRoom = poke.Type == PokeTradeType.Random && Hub.Config.Distribution.RemainInUnionRoomBDSP;

        // If we weren't supposed to remain and started out in the Union Room, ensure we're out of the box.
        if (!distroRemainInRoom && await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
                return PokeTradeResult.RecoverReturnOverworld;
        }

        if (await CheckIfSoftBanned(SoftBanOffset, token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        var toSend = poke.TradeData;
        if (toSend.Species != 0)
        {
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
        }

        // Enter Union Room. Shouldn't do anything if we're already there.
        if (!await EnterUnionRoomWithCode(poke.Type, poke.Code, token).ConfigureAwait(false))
        {
            // We don't know how far we made it in, so restart the game to be safe.
            await RestartGameBDSP(token).ConfigureAwait(false);
            return PokeTradeResult.RecoverEnterUnionRoom;
        }
        await RequestUnionRoomTrade(token).ConfigureAwait(false);
        poke.TradeSearching(this);
        var waitPartner = Hub.Config.Trade.TradeConfiguration.TradeWaitTime;

        // Keep pressing A until we detect someone talking to us.
        while (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false) && waitPartner > 0)
        {
            if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

            for (int i = 0; i < 2; ++i)
                await Click(A, 0_450, token).ConfigureAwait(false);

            if (--waitPartner <= 0)
            {
                // Ensure we exit the union room when no trainer is found.
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log("¡Encontré a un usuario hablando con nosotros!");

        // Keep pressing A until TargetTranerParam (sic) is loaded (when we hit the box).
        while (!await IsPartnerParamLoaded(token).ConfigureAwait(false) && waitPartner > 0)
        {
            if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

            for (int i = 0; i < 2; ++i)
                await Click(A, 0_450, token).ConfigureAwait(false);

            // Can be false if they talked and quit.
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                break;
            if (--waitPartner <= 0)
            {
                // Ensure we exit the union room if the partner is too slow.
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
        }
        Log("Entrando en la caja...");

        // Still going through dialog and box opening.
        await Task.Delay(3_000, token).ConfigureAwait(false);

        // Can happen if they quit out of talking to us.
        if (!await IsPartnerParamLoaded(token).ConfigureAwait(false))
        {
            // Ensure we exit the union room if the partner is too slow.
            await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
        var trainerNID = GetFakeNID(tradePartner.TrainerName, tradePartner.TrainerID);
        RecordUtil<PokeTradeBotBS>.Record($"Iniciando\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");

        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

        string ot = tradePartner.TrainerName;
        int tid = int.Parse(tradePartner.TID7);
        int sid = int.Parse(tradePartner.SID7);

        if (existingTradeDetails != null)
        {
            bool shouldUpdateOT = existingTradeDetails.OT != tradePartner.TrainerName;
            bool shouldUpdateTID = existingTradeDetails.TID != tid;
            bool shouldUpdateSID = existingTradeDetails.SID != sid;

            ot = shouldUpdateOT ? tradePartner.TrainerName : existingTradeDetails.OT ?? tradePartner.TrainerName;
            tid = shouldUpdateTID ? tid : existingTradeDetails.TID;
            sid = shouldUpdateSID ? sid : existingTradeDetails.SID;
        }

        if (ot != null)
        {
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid, sid);
        }

        var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
            return PokeTradeResult.SuspiciousActivity;

        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, sav, tradePartner.TrainerName, (uint)tid, (uint)sid, token);
        }

        await Task.Delay(2_000, token).ConfigureAwait(false);

        // Confirm Box 1 Slot 1
        if (poke.Type == PokeTradeType.Specific)
        {
            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
        }

        poke.SendNotification(this, $"Entrenador encontrado: **{tradePartner.TrainerName}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradePartner.TID7}__\n **SID**: __{tradePartner.SID7}__\n▲\n\n Esperando por un __Pokémon__...");

        // Requires at least one trade for this pointer to make sense, so cache it here.
        LinkTradePokemonOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerPokemonPointer, token).ConfigureAwait(false);

        if (poke.Type == PokeTradeType.Dump)
            return await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);

        // Wait for user input... Needs to be different from the previously offered Pokémon.
        var tradeOffered = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 25_000, 1_000, false, true, token).ConfigureAwait(false);
        if (!tradeOffered)
            return PokeTradeResult.TrainerTooSlow;

        // If we detected a change, they offered something.
        var offered = await ReadPokemon(LinkTradePokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (offered.Species == 0 || !offered.ChecksumValid)
            return PokeTradeResult.TrainerTooSlow;

        PokeTradeResult update;
        var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
        (toSend, update) = await GetEntityToSend(sav, poke, offered, toSend, trainer, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
            return update;

        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
            return tradeResult;

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        // Trade was Successful!
        var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("El usuario no completó el intercambio.");
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("El usuario completó el intercambio.");
        poke.TradeFinished(this, received);

        // Only log if we completed the trade.
        UpdateCountsAndExport(poke, received, toSend);

        // Still need to wait out the trade animation.
        await Task.Delay(12_000, token).ConfigureAwait(false);

        Log("Intentando salir de la Sala Unión.");
        // Now get out of the Union Room.
        if (!await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false))
            return PokeTradeResult.RecoverReturnOverworld;

        // Sometimes they offered another mon, so store that immediately upon leaving Union Room.
        lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);

        return PokeTradeResult.Success;
    }

    private bool GetNextBatchTrade(PokeTradeDetail<PB8> currentTrade, out PokeTradeDetail<PB8>? nextDetail)
    {
        nextDetail = null;
        var batchQueue = Hub.Queues.GetQueue(PokeRoutineType.Batch);
        Log($"{currentTrade.Trainer.TrainerName}-{currentTrade.Trainer.ID}: Buscando la siguiente trade después de {currentTrade.BatchTradeNumber}/{currentTrade.TotalBatchTrades}");

        // Get all trades for this user - include UniqueTradeID filter to avoid mixing different batches
        var userTrades = batchQueue.Queue.GetSnapshot()
            .Select(x => x.Value)
            .Where(x => x.Trainer.ID == currentTrade.Trainer.ID &&
                       x.UniqueTradeID == currentTrade.UniqueTradeID)
            .OrderBy(x => x.BatchTradeNumber)
            .ToList();

        // Log what we found
        foreach (var trade in userTrades)
        {
            Log($"{currentTrade.Trainer.TrainerName}-{currentTrade.Trainer.ID}: Trade encontrada en la cola: #{trade.BatchTradeNumber}/{trade.TotalBatchTrades} para el entrenador {trade.Trainer.TrainerName}");
        }

        // Get the next sequential trade
        nextDetail = userTrades.FirstOrDefault(x => x.BatchTradeNumber == currentTrade.BatchTradeNumber + 1);
        if (nextDetail != null)
        {
            Log($"{currentTrade.Trainer.TrainerName}-{currentTrade.Trainer.ID}: Siguiente trade seleccionada: {nextDetail.BatchTradeNumber}/{nextDetail.TotalBatchTrades}");
            return true;
        }

        Log($"{currentTrade.Trainer.TrainerName}-{currentTrade.Trainer.ID}: No se encontraron más trades para este usuario");
        return false;
    }

    private async Task<PokeTradeResult> PerformBatchTrade(SAV8BS sav, PokeTradeDetail<PB8> poke, CancellationToken token)
    {
        int completedTrades = 0;
        var startingDetail = poke;
        var originalTrainerID = startingDetail.Trainer.ID;
        bool firstTrade = true;

        // Verify that this is actually the first trade in the batch
        // If not, find the first trade and use that as our starting point
        if (startingDetail.BatchTradeNumber != 1 && startingDetail.TotalBatchTrades > 1)
        {
            Log($"Trade iniciada con un elemento que no es el primero del lote ({startingDetail.BatchTradeNumber}/{startingDetail.TotalBatchTrades}). Buscando el primer elemento...");

            var batchQueue = Hub.Queues.GetQueue(PokeRoutineType.Batch);
            var firstTradeInBatch = batchQueue.Queue.GetSnapshot()
                .Select(x => x.Value)
                .Where(x => x.Trainer.ID == startingDetail.Trainer.ID &&
                           x.UniqueTradeID == startingDetail.UniqueTradeID &&
                           x.BatchTradeNumber == 1)
                .FirstOrDefault();

            if (firstTradeInBatch != null)
            {
                Log($"Primer trade del lote encontrada. Iniciando con la trade 1/{startingDetail.TotalBatchTrades} en su lugar.");
                poke = firstTradeInBatch;
                startingDetail = firstTradeInBatch;
            }
            else
            {
                Log($"ADVERTENCIA: No se pudo encontrar la primera trade del lote. Se continuará con la trade {startingDetail.BatchTradeNumber}/{startingDetail.TotalBatchTrades}");
            }
        }

        // Helper method to send collected Pokémon back to the user before early return
        void SendCollectedPokemonAndCleanup()
        {
            var allReceived = _batchTracker.GetReceivedPokemon(originalTrainerID);
            if (allReceived.Count > 0)
            {
                poke.SendNotification(this, $"Enviándote los {allReceived.Count} Pokémon que me intercambiaste antes de la interrupción.");

                // Log the Pokémon we're returning for debugging
                Log($"Devolviendo {allReceived.Count} Pokémon al entrenador {originalTrainerID}.");
                foreach (var pokemon in allReceived)
                {
                    Log($"  - Devolviendo: {pokemon.Species} (Checksum: {pokemon.Checksum:X8})");
                    poke.TradeFinished(this, pokemon);
                }
            }
            else
            {
                Log($"No se encontraron Pokémon para devolver al entrenador {originalTrainerID}.");
            }

            // Cleanup
            _batchTracker.ClearReceivedPokemon(originalTrainerID);
        }

        // Initial routine setup and room entry
        if (token.IsCancellationRequested)
        {
            SendCollectedPokemonAndCleanup();
            return PokeTradeResult.RoutineCancel;
        }

        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        if (await CheckIfSoftBanned(SoftBanOffset, token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        // Enter Union Room initially
        if (!await EnterUnionRoomWithCode(poke.Type, poke.Code, token).ConfigureAwait(false))
        {
            SendCollectedPokemonAndCleanup();
            await RestartGameBDSP(token).ConfigureAwait(false);
            return PokeTradeResult.RecoverEnterUnionRoom;
        }
        
        while (completedTrades < startingDetail.TotalBatchTrades)
        {
            var toSend = poke.TradeData;
            if (toSend.Species != 0)
                await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
            if (firstTrade)
            {
                await RequestUnionRoomTrade(token).ConfigureAwait(false);
                firstTrade = false;
            }
            poke.TradeSearching(this);
            var waitPartner = Hub.Config.Trade.TradeConfiguration.TradeWaitTime;
            while (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false) && waitPartner > 0)
            {
                for (int i = 0; i < 2; ++i)
                    await Click(A, 0_450, token).ConfigureAwait(false);
                if (--waitPartner <= 0)
                {
                    if (completedTrades > 0)
                        poke.SendNotification(this, $"⚠️ No se encontró al entrenador después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");

                    SendCollectedPokemonAndCleanup();
                    await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                    return PokeTradeResult.NoTrainerFound;
                }
            }
            Log("¡Encontré a un usuario hablando con nosotros!");
            while (!await IsPartnerParamLoaded(token).ConfigureAwait(false) && waitPartner > 0)
            {
                if (token.IsCancellationRequested)
                {
                    SendCollectedPokemonAndCleanup();
                    return PokeTradeResult.RoutineCancel;
                }

                for (int i = 0; i < 2; ++i)
                    await Click(A, 0_450, token).ConfigureAwait(false);
                if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                    break;
                if (--waitPartner <= 0)
                {
                    if (completedTrades > 0)
                        poke.SendNotification(this, $"⚠️ El compañero de trade fue demasiado lento después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                    SendCollectedPokemonAndCleanup();
                    await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                    return PokeTradeResult.TrainerTooSlow;
                }
            }
            Log("Entrando en la caja...");
            await Task.Delay(3_000, token).ConfigureAwait(false);
            if (!await IsPartnerParamLoaded(token).ConfigureAwait(false))
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ El compañero de trade fue demasiado lento después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
            var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var trainerNID = GetFakeNID(tradePartner.TrainerName, tradePartner.TrainerID);

            // Handle trainer data
            var tradeCodeStorage = new TradeCodeStorage();
            var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);
            string ot = tradePartner.TrainerName;
            int tid = int.Parse(tradePartner.TID7);
            int sid = int.Parse(tradePartner.SID7);
            if (existingTradeDetails != null)
            {
                bool shouldUpdateOT = existingTradeDetails.OT != tradePartner.TrainerName;
                bool shouldUpdateTID = existingTradeDetails.TID != tid;
                bool shouldUpdateSID = existingTradeDetails.SID != sid;
                ot = shouldUpdateOT ? tradePartner.TrainerName : existingTradeDetails.OT ?? tradePartner.TrainerName;
                tid = shouldUpdateTID ? tid : existingTradeDetails.TID;
                sid = shouldUpdateSID ? sid : existingTradeDetails.SID;
            }
            if (ot != null)
            {
                tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid, sid);
            }
            var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
            if (partnerCheck != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ Actividad sospechosa detectada después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.SuspiciousActivity;
            }

            Log($"Encontré un entrenador para intercambiar por Link Trade:: {tradePartner.TrainerName}-{tradePartner.TID7} (ID: {trainerNID})");
            if (completedTrades == 0 || startingDetail.TotalBatchTrades == 1)
                poke.SendNotification(this, $"Entrenador encontrado: **{tradePartner.TrainerName}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradePartner.TID7}__\n **SID**: __{tradePartner.SID7}__\n▲\n\n Esperando por un __Pokémon__...");

            if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
            {
                toSend = await ApplyAutoOT(toSend, sav, tradePartner.TrainerName, (uint)tid, (uint)sid, token);
                if (toSend.Species != 0)
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
            }
            await Task.Delay(2_000, token).ConfigureAwait(false);
            // Begin trade
            LinkTradePokemonOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerPokemonPointer, token).ConfigureAwait(false);
            var offered = await ReadUntilPresent(LinkTradePokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (offered == null || offered.Species == 0 || !offered.ChecksumValid)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ Pokémon no válido ofrecido después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
            var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
            PokeTradeResult update;
            (toSend, update) = await GetEntityToSend(sav, poke, offered, toSend, trainer, token).ConfigureAwait(false);
            if (update != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ La verificación de actualización falló después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return update;
            }
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                if (completedTrades > 0)
                    poke.SendNotification(this, $"⚠️ La confirmación del trade falló después del trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return tradeResult;
            }
            // Trade cleanup and validation
            var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                poke.SendNotification(this, $"⚠️ El compañero no completó el trade {completedTrades + 1}/{startingDetail.TotalBatchTrades}. Cancelando los trades restantes.");
                SendCollectedPokemonAndCleanup();
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            UpdateCountsAndExport(poke, received, toSend);
            LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);
            completedTrades++;

            // Store received Pokemon using original trainer ID
            _batchTracker.AddReceivedPokemon(originalTrainerID, received);
            Log($"Pokémon recibido {received.Species} (Checksum: {received.Checksum:X8}) añadido al rastreador de lote para el entrenador {originalTrainerID} (Trade {completedTrades}/{startingDetail.TotalBatchTrades})");

            if (completedTrades == startingDetail.TotalBatchTrades)
            {
                // Get all collected Pokemon before cleaning anything up
                var allReceived = _batchTracker.GetReceivedPokemon(originalTrainerID);
                Log($"Trades por lote completados. Se encontraron {allReceived.Count} Pokémon almacenados para el entrenador {originalTrainerID}");

                // First send notification that trades are complete
                poke.SendNotification(this, "✅ ¡Todos los intercambios por lotes completados! ¡Gracias por tradear!");

                // Then finish each trade with the corresponding received Pokemon
                foreach (var pokemon in allReceived)
                {
                    Log($"  - Devolviendo: {pokemon.Species} (Checksum: {pokemon.Checksum:X8})");
                    poke.TradeFinished(this, pokemon);
                }

                // Mark the batch as fully completed and clean up
                Hub.Queues.CompleteTrade(this, startingDetail);
                CleanupAllBatchTradesFromQueue(startingDetail);
                _batchTracker.ClearReceivedPokemon(originalTrainerID);

                // Exit the trade state to prevent further searching
                await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                poke.IsProcessing = false; // Ensure the trade is marked as not processing
                break;
            }
            if (GetNextBatchTrade(poke, out var nextDetail))
            {
                if (nextDetail == null)
                {
                    poke.SendNotification(this, "⚠️ Error en la secuencia de lotes. Terminando operaciones.");
                    SendCollectedPokemonAndCleanup();
                    await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                    return PokeTradeResult.Success;
                }

                poke.SendNotification(this, $"✅ Intercambio {completedTrades} completado! Preparando el siguiente Pokémon: ({nextDetail.BatchTradeNumber}/{nextDetail.TotalBatchTrades}). Por favor, espera en la pantalla de intercambio!");
                poke = nextDetail;
                await Task.Delay(10_000, token).ConfigureAwait(false); // Add delay for trade animation/pokedex register
                await Click(A, 1_000, token).ConfigureAwait(false);
                if (poke.TradeData.Species != 0)
                {
                    if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
                    {
                        var nextToSend = await ApplyAutoOT(poke.TradeData, sav, tradePartner.TrainerName, (uint)tid, (uint)sid, token);
                        await SetBoxPokemonAbsolute(BoxStartOffset, nextToSend, token, sav).ConfigureAwait(false);
                    }
                    else
                    {
                        await SetBoxPokemonAbsolute(BoxStartOffset, poke.TradeData, token, sav).ConfigureAwait(false);
                    }
                }
                continue;
            }
            poke.SendNotification(this, "⚠️ No puedo encontrar el próximo intercambio en secuencia. El intercambio en lote se cancelará.");
            SendCollectedPokemonAndCleanup();
            await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
            return PokeTradeResult.Success;
        }

        // Ensure we exit properly even if the loop breaks unexpectedly
        await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
        poke.IsProcessing = false; // Explicitly mark as not processing
        return PokeTradeResult.Success;
    }
    private async Task PerformTrade(SAV8BS sav, PokeTradeDetail<PB8> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            if (detail.Type == PokeTradeType.Batch)
                result = await PerformBatchTrade(sav, detail, token).ConfigureAwait(false);
            else
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);

            if (result != PokeTradeResult.Success)
            {
                if (detail.Type == PokeTradeType.Batch)
                    await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
                else
                    HandleAbortedTrade(detail, type, priority, result);
            }
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            if (detail.Type == PokeTradeType.Batch)
                await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
            else
                HandleAbortedTrade(detail, type, priority, result);
            throw;
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
            if (detail.Type == PokeTradeType.Batch)
                await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
            else
                HandleAbortedTrade(detail, type, priority, result);
        }
    }
    private async Task HandleAbortedBatchTrade(PokeTradeDetail<PB8> detail, PokeRoutineType type, uint priority, PokeTradeResult result, CancellationToken token)
    {
        detail.IsProcessing = false;
        if (detail.TotalBatchTrades > 1)
        {
            if (result != PokeTradeResult.Success)
            {
                if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
                {
                    detail.IsRetry = true;
                    Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                    detail.SendNotification(this, $"<a:warning:1206483664939126795> Oops! Algo sucedió durante el comercio por lotes {detail.BatchTradeNumber}/{detail.TotalBatchTrades}. Te volveré a poner en cola para otro intento.");
                }
                else
                {
                    detail.SendNotification(this, $"<a:warning:1206483664939126795> El trade {detail.BatchTradeNumber}/{detail.TotalBatchTrades} falló. Cancelando las operaciones por lotes restantes: {result}");
                    CleanupAllBatchTradesFromQueue(detail);
                    detail.TradeCanceled(this, result);
                    await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false);
                }
            }
            else
            {
                CleanupAllBatchTradesFromQueue(detail);
            }
        }
        else
        {
            HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

        int ctr = 0;
        var time = TimeSpan.FromSeconds(Hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;

        var bctr = 0;
        while (ctr < Hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            if (token.IsCancellationRequested) return PokeTradeResult.RoutineCancel;

            // We're no longer talking, so they probably quit on us.
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            // Wait for user input... Needs to be different from the previously offered Pokémon.
            var tradeOffered = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 3_000, 1_000, false, true, token).ConfigureAwait(false);
            if (!tradeOffered)
                continue;

            // If we detected a change, they offered something.
            var pk = await ReadPokemon(LinkTradePokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            var newECchk = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);
            if (pk.Species == 0 || !pk.ChecksumValid || lastOffered.SequenceEqual(newECchk))
                continue;
            lastOffered = newECchk;

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
            var msg = Hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"File {ctr}";

            // Extra information about trainer data for people requesting with their own trainer data.
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "Male" : "Female";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**Datos del entrenador**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}```";

            // Extra information for shiny eggs, because of people dumping to skip hatching.
            var eggstring = pk.IsEgg ? "Egg " : string.Empty;
            msg += pk.IsShiny ? $"\n**Este Pokémon {eggstring}es shiny!**" : string.Empty;
            detail.SendNotification(this, pk, msg);
        }

        Log($"Finalizó el ciclo de dump después de procesar {ctr} Pokémon.");
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"Dumped {ctr} Pokémon.");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank pk8
        return PokeTradeResult.Success;
    }

    private async Task RequestUnionRoomTrade(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        // Move to middle of room
        await PressAndHold(DUP, 2_000, 0_250, token).ConfigureAwait(false);
        // Y-button trades always put us in a place where we can open the call menu without having to move.
        Log("Intentando abrir el menú Y.");
        await Click(Y, 1_000, token).ConfigureAwait(false);
        await Click(A, 0_400, token).ConfigureAwait(false);
        await Click(DDOWN, 0_400, token).ConfigureAwait(false);
        await Click(DDOWN, 0_400, token).ConfigureAwait(false);
        await Click(A, 0_100, token).ConfigureAwait(false);
    }

    private async Task RestartGameBDSP(CancellationToken token)
    {
        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task RestartGameIfCantLeaveUnionRoom(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        if (!await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false))
            await RestartGameBDSP(token).ConfigureAwait(false);
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
            Hub.BotSync.Barrier.AddParticipant();
            Log($"Se unió a la barrera. Conteo: {Hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            Hub.BotSync.Barrier.RemoveParticipant();
            Log($"Dejó la barrera. Conteo: {Hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PB8> poke, PB8 received, PB8 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.CountStatsSettings.AddCompletedDistribution();
        else if (poke.Type == PokeTradeType.FixOT)
            counts.CountStatsSettings.AddCompletedFixOTs();
        else
            counts.CountStatsSettings.AddCompletedTrade();

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
        {
            var subfolder = poke.Type.ToString().ToLower();
            var service = poke.Notifier.GetType().ToString().ToLower();
            var tradedFolder = service.Contains("twitch") ? Path.Combine("traded", "twitch") : service.Contains("discord") ? Path.Combine("traded", "discord") : "traded";
            DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.FixOT)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private void WaitAtBarrierIfApplicable(CancellationToken token)
    {
        if (!ShouldWaitAtBarrier)
            return;
        var opt = Hub.Config.Distribution.SynchronizeBots;
        if (opt == BotSyncOption.NoSync)
            return;

        var timeoutAfter = Hub.Config.Distribution.SynchronizeTimeout;
        if (FailedBarrier == 1) // failed last iteration
            timeoutAfter *= 2; // try to re-sync in the event things are too slow.

        var result = Hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

        if (result)
        {
            FailedBarrier = 0;
            return;
        }

        FailedBarrier++;
        Log($"Se agotó el tiempo de espera de sincronización de barrera después de {timeoutAfter} segundos. Continuando.");
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            Hub.Config.Stream.IdleAssets(this);
            Log("Nada que comprobar, esperando nuevos usuarios...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            return Click(B, 1_000, token);
        return Task.Delay(1_000, token);
    }
}
