using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsLGPE;

namespace SysBot.Pokemon;

public class PokeTradeBotLGPE(PokeTradeHub<PB7> Hub, PokeBotState Config) : PokeRoutineExecutor7LGPE(Config), ICountBot, ITradeBot
{
    private readonly TradeSettings TradeSettings = Hub.Config.Trade;

    public readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

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
    private readonly IDumper DumpSetting = Hub.Config.Folder;

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("Identificando los datos del entrenador de la consola host.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);

            OnConnectionSuccess();
            Log($"Iniciando el bucle principal {nameof(PokeTradeBotLGPE)}.");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"Finalizando el bucle {nameof(PokeTradeBotLGPE)}.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        UpdateBarrier(false);
        await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(Hub.Config, t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("Reiniciando el bucle principal.");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("Error detectado, reiniciando el juego!!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }

    private async Task InnerLoop(SAV7b sav, CancellationToken token)
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
                Log(e.Message);
                break;
            }
        }
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

    private async Task DoTrades(SAV7b sav, CancellationToken token)
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
            string tradetype = $" ({detail.Type})";
            Log($"Empezando el próximo intercambio de bots de {type}{tradetype} Obteniendo datos...");
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private async Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            Hub.Config.Stream.IdleAssets(this);
            Log("Nada que comprobar, esperando nuevos usuarios...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            await Click(B, 1_000, token).ConfigureAwait(false);
        else
            await Task.Delay(1_000, token).ConfigureAwait(false);
    }

    protected virtual (PokeTradeDetail<PB7>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        string botName = Connection.Name;
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority, botName))
            return (detail, priority);
        if (Hub.Queues.TryDequeueLedy(out detail))
            return (detail, PokeTradePriorities.TierFree);
        return (null, PokeTradePriorities.TierFree);
    }

    private async Task PerformTrade(SAV7b sav, PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
            if (result == PokeTradeResult.Success)
                return;
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            HandleAbortedTrade(detail, type, priority, result);
            throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
        }

        HandleAbortedTrade(detail, type, priority, result);
    }

    private void HandleAbortedTrade(PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "<a:warning:1206483664939126795> Oops! Algo ocurrió. Intentemoslo una ves mas.");
        }
        else
        {
            detail.SendNotification(this, $"<a:warning:1206483664939126795> Oops! Algo ocurrió. Cancelando el trade: **{result.GetDescription()}**.");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV7b sav, PokeTradeDetail<PB7> poke, CancellationToken token)
    {
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);
        var toSend = poke.TradeData;
        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            var trainerID = poke.Trainer.ID;
            var tradeCodeStorage1 = new TradeCodeStorage();
            var tradeDetails = tradeCodeStorage1.GetTradeDetails(trainerID);
            if (tradeDetails != null && tradeDetails.TID != 0 && tradeDetails.SID != 0)
            {
                Log($"Aplicar Auto OT al Pokémon usando Trainer OT: {tradeDetails.OT}, TID: {tradeDetails.TID}, SID: {tradeDetails.SID}");
                var updatedToSend = await ApplyAutoOT(toSend, trainerID);
                if (updatedToSend != null)
                {
                    toSend = updatedToSend;
                    poke.TradeData = updatedToSend;
                }
            }
        }
        if (toSend.Species != 0)
            await WriteBoxPokemon(toSend, 0, 0, token);
        if (!await IsOnOverworldStandard(token))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }
        await Click(X, 2000, token).ConfigureAwait(false);
        Log("Abriendo menú...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("Seleccionando Comunicar....");
        await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == waitingtotradescreen)
        {
            await Click(A, 1000, token);
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
            {
                while (!await IsOnOverworldStandard(token))
                {
                    await Click(B, 1000, token);
                }
                await Click(X, 2000, token).ConfigureAwait(false);
                Log("Abriendo menú...");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("Seleccionando Comunicar....");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
        }
        await Task.Delay(2000, token).ConfigureAwait(false);
        Log("Seleccionando conexión lejana......");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(poke, token);
        poke.TradeSearching(this);
        Log($"Buscando usuario {poke.Trainer.TrainerName}");
        await Task.Delay(3000, token).ConfigureAwait(false);
        var btimeout = new Stopwatch();
        btimeout.Restart();

        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100, token);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"No se encontró {poke.Trainer.TrainerName}");

                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"Encontrado {poke.Trainer.TrainerName}");
        await Task.Delay(10000, token).ConfigureAwait(false);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);

        var tradeCodeStorage = new TradeCodeStorage();

        if (tradepartnersav.OT != sav.OT)
        {
            uint displaySID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav.Blocks.Status.Data[0..4]) / 1_000_000;
            uint displayTID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav.Blocks.Status.Data[0..4]) % 1_000_000;
            string tid7 = displayTID.ToString("D6");
            string sid7 = displaySID.ToString("D4");
            Log($"Entrenador comercial de enlace encontrado: {tradepartnersav.OT}, TID7: {tid7}, SID7: {sid7}, Juego: {tradepartnersav.Version}");

            // Save the OT, TID7, and SID7 information in the TradeCodeStorage for tradepartnersav
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, tradepartnersav.OT, int.Parse(tid7), int.Parse(sid7));

            // Send notification with trainer details
            poke.SendNotification(this, $"Entrenador comercial de enlace encontrado: {tradepartnersav2.OT}, TID7: {tid7}, SID7: {sid7}");
        }

        if (tradepartnersav2.OT != sav.OT)
        {
            uint displaySID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav2.Blocks.Status.Data[0..4]) / 1_000_000;
            uint displayTID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav2.Blocks.Status.Data[0..4]) % 1_000_000;
            string tid7 = displayTID.ToString("D6");
            string sid7 = displaySID.ToString("D4");
            Log($"Entrenador comercial de enlace encontrado: {tradepartnersav2.OT}, TID7: {tid7}, SID7: {sid7}");

            // Save the OT, TID7, and SID7 information in the TradeCodeStorage for tradepartnersav2
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, tradepartnersav2.OT, int.Parse(tid7), int.Parse(sid7));

            // Send notification with trainer details
            poke.SendNotification(this, $"Entrenador comercial de enlace encontrado: {tradepartnersav2.OT}, TID7: {tid7}, SID7: {sid7}");
        }

        if (poke.Type == PokeTradeType.Dump)
        {
            var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
            await ExitTrade(false, token).ConfigureAwait(false);
            return result;
        }
        if (poke.Type == PokeTradeType.Clone)
        {
            var result = await ProcessCloneTradeAsync(poke, sav, token);
            await ExitTrade(false, token);
            return result;
        }
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
        {
            await Click(A, 1000, token);
        }
        poke.SendNotification(this, "Tienes **15 segundos** para seleccionar tu Pokémon comercial");
        Log("Esperando en la pantalla de comercio...");

        await Task.Delay(5_000, token).ConfigureAwait(false);
        var tradeResult = await ConfirmAndStartTrading(poke, 0, token);
        if (tradeResult != PokeTradeResult.Success)
        {
            if (tradeResult == PokeTradeResult.TrainerLeft)
                Log("Intercambio cancelado porque el entrenador abandonó el intercambio.");
            await ExitTrade(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.ExceptionInternal;
        }

        //trade was successful
        var received = await ReadPokemon(GetSlotOffset(0, 0), token);

        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("El usuario no completó el intercambio.");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("El usuario completó el intercambio.");
        poke.TradeFinished(this, received);

        // Still need to wait out the trade animation.
        await Task.Delay(10_000, token).ConfigureAwait(false);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PB7> detail, int slot, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
        Log("Confirmando e iniciando comercio...");
        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < 10; i++)
        {
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                return PokeTradeResult.TrainerLeft;
            await Click(A, 1_500, token).ConfigureAwait(false);
        }

        var tradeCounter = 0;
        Log("Comprobando los Pokémon recibidos en la ranura 1.");
        while (true)
        {
            var newEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                Log("Cambio detectado en la ranura 1");
                await Task.Delay(15_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }

            tradeCounter++;

            if (tradeCounter >= Hub.Config.Trade.TradeConfiguration.TradeAnimationMaxDelaySeconds)
            {
                // If we don't detect a B1S1 change, the trade didn't go through in that time.
                Log("No detectó un cambio en la ranura 1.");
                await Click(B, 1_000, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            if (await IsOnOverworldStandard(token))
                return PokeTradeResult.TrainerLeft;
            await Task.Delay(1000);
        }
    }

    private async Task<PokeTradeResult> ProcessCloneTradeAsync(PokeTradeDetail<PB7> detail, SAV7b sav, CancellationToken token)
    {
        detail.SendNotification(this, "__Resalta los pokemon__ de tu caja que quieras clonar, ¡hasta 6 a la vez! __Tienes 5 segundos__ entre resaltado y resaltado para pasar al siguiente pokemon (**¡Los 5 primeros empiezan ahora!**). Si quieres menos de 6, quédate en el mismo pokemon hasta que empiece el intercambio..");
        await Task.Delay(10_000);
        var offereddatac = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
        var offeredpbmc = new PB7(offereddatac);
        List<PB7> clonelist = new();
        clonelist.Add(offeredpbmc);
        detail.SendNotification(this, $"<a:yes:1206485105674166292> Agregaste {(Species)offeredpbmc.Species} a la lista de clonacion");

        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(5_000);
            var newoffereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
            var newofferedpbm = new PB7(newoffereddata);
            if (clonelist.Any(z => SearchUtil.HashByDetails(z) == SearchUtil.HashByDetails(newofferedpbm)))
            {
                continue;
            }
            else
            {
                clonelist.Add(newofferedpbm);
                offeredpbmc = newofferedpbm;
                detail.SendNotification(this, $"<a:yes:1206485105674166292> Agregaste {(Species)offeredpbmc.Species} a la lista de clonacion");
            }
        }

        var clonestring = new StringBuilder();
        foreach (var k in clonelist)
            clonestring.AppendLine($"{(Species)k.Species}");
        detail.SendNotification(this, clonestring.ToString());

        detail.SendNotification(this, "Saliendo del trade para inyectar clones, por favor reconéctese usando el mismo código de enlace.");
        await ExitTrade(false, token);
        foreach (var g in clonelist)
        {
            await WriteBoxPokemon(g, 0, clonelist.IndexOf(g), token);
            await Task.Delay(1000);
        }
        await Click(X, 2000, token).ConfigureAwait(false);
        Log("Abriendo menú...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("Seleccionando Comunicar...");
        await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == waitingtotradescreen)
        {
            await Click(A, 1000, token);
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
            {
                while (!await IsOnOverworldStandard(token))
                {
                    await Click(B, 1000, token);
                }
                await Click(X, 2000, token).ConfigureAwait(false);
                Log("Abriendo menú...");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("Seleccionando Comunicar...");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
        }
        await Task.Delay(2000);
        Log("Seleccionando Conexión Lejana...");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(detail, token);
        detail.TradeSearching(this);
        Log($"Buscando usuario {detail.Trainer.TrainerName}");
        var btimeout = new Stopwatch();
        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                detail.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"No se encontró a {detail.Trainer.TrainerName}");

                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"Encontrado {detail.Trainer.TrainerName}");
        await Task.Delay(10000);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);
        if (tradepartnersav.OT != sav.OT)
        {
            Log($"Entrenador comercial de enlace encontrado: {tradepartnersav.OT}, TID: {tradepartnersav.DisplayTID}, SID: {tradepartnersav.DisplaySID},Juego: {tradepartnersav.Version}");
            detail.SendNotification(this, $"Entrenador encontrado: **{tradepartnersav.OT}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradepartnersav.DisplayTID}__\n **SID**: __{tradepartnersav.DisplaySID}__\n **Juego**: __{tradepartnersav.Version}__\n▲");
        }
        if (tradepartnersav2.OT != sav.OT)
        {
            Log($"Entrenador comercial de enlace encontrado: {tradepartnersav2.OT}, TID: {tradepartnersav2.DisplayTID}, SID: {tradepartnersav2.DisplaySID}");
            detail.SendNotification(this, $"Entrenador encontrado: **{tradepartnersav2.OT}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradepartnersav2.DisplayTID}__\n **SID**: __{tradepartnersav2.DisplaySID}__\n **Juego**: __{tradepartnersav.Version}__\n▲");
        }
        foreach (var t in clonelist)
        {
            for (int q = 0; q < clonelist.IndexOf(t); q++)
            {
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token);
                await SetStick(SwitchStick.RIGHT, 0, 0, 1000, token).ConfigureAwait(false);
            }
            while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
            {
                await Click(A, 1000, token);
            }
            detail.SendNotification(this, $"Enviando {(Species)t.Species}. Tienes 15 segundos para seleccionar tu pokemon de intercambio");
            Log("Esperando en la pantalla comercial...");

            await Task.Delay(10_000).ConfigureAwait(false);
            detail.SendNotification(this, "<a:warning:1206483664939126795> Te quedan 5 segundos para llegar a la pantalla de operaciones y no interrumpir la operación.");
            await Task.Delay(5_000);
            var tradeResult = await ConfirmAndStartTrading(detail, clonelist.IndexOf(t), token);
            if (tradeResult != PokeTradeResult.Success)
            {
                if (tradeResult == PokeTradeResult.TrainerLeft)
                    Log("Intercambio cancelado porque el entrenador abandonó el intercambio.");
                await ExitTrade(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }
            await Task.Delay(30_000);
        }
        await ExitTrade(false, token);
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PB7> detail, CancellationToken token)
    {
        detail.SendNotification(this, "Resalta el Pokémon en tu caja, tienes 30 segundos");
        var offereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
        var offeredpbm = new PB7(offereddata);

        detail.SendNotification(this, offeredpbm, "▼ Aquí está el pokemon que me mostraste ▼");

        var quicktime = new Stopwatch();
        quicktime.Restart();
        while (quicktime.ElapsedMilliseconds <= 30_000)
        {
            var newoffereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
            var newofferedpbm = new PB7(newoffereddata);
            if (SearchUtil.HashByDetails(offeredpbm) != SearchUtil.HashByDetails(newofferedpbm))
            {
                detail.SendNotification(this, newofferedpbm, "▼ Aquí está el pokemon que me mostraste ▼");

                offeredpbm = newofferedpbm;
            }
        }
        detail.SendNotification(this, "¡El tiempo ha terminado!");
        return PokeTradeResult.Success;
    }

    private async Task EnterLinkCodeLG(PokeTradeDetail<PB7> poke, CancellationToken token)
    {
        if (poke.LGPETradeCode == null || !poke.LGPETradeCode.Any())
            poke.LGPETradeCode = new List<Pictocodes> { Pictocodes.Pikachu, Pictocodes.Pikachu, Pictocodes.Pikachu };
        Hub.Config.Stream.StartEnterCode(this);
        foreach (Pictocodes pc in poke.LGPETradeCode)
        {
            if ((int)pc > 4)
            {
                await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
            if ((int)pc <= 4)
            {
                for (int i = (int)pc; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
            else
            {
                for (int i = (int)pc - 5; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
            await Click(A, 200, token).ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            if ((int)pc <= 4)
            {
                for (int i = (int)pc; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
            else
            {
                for (int i = (int)pc - 5; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }

            if ((int)pc > 4)
            {
                await SetStick(SwitchStick.RIGHT, 0, 30000, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
        }
    }

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

    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("Comportamiento inesperado, recuperando posición.");
        int ctr = 120_000;
        while (!await IsOnOverworldStandard(token))
        {
            if (ctr < 0)
            {
                await RestartGameLGPE(Hub.Config, token).ConfigureAwait(false);
                return;
            }

            // Press B to bring up the exit screen
            Log("Saliendo del comercio...");
            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworldStandard(token))
                return;

            // Press A to confirm the exit
            Log("Presionando A para salir...");
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Wait 10 seconds
            await Task.Delay(10_000, token);

            // Press B three more times to navigate back to the overworld
            Log("Presionando B para retroceder completamente.");

            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);

            // Check if we are on the overworld
            Log("Comprobando si estamos en el pverworld.");
            if (await IsOnOverworldStandard(token))
            {
                Log("Con éxito en el overworld.");
                return;
            }

            ctr -= 6_000; // Subtract 6 seconds for the additional B presses and delay
        }
    }

    private Task<PB7?> ApplyAutoOT(PB7 toSend, ulong trainerID)
    {
        var tradeCodeStorage = new TradeCodeStorage();
        var tradeDetails = tradeCodeStorage.GetTradeDetails(trainerID);
        if (tradeDetails != null)
        {
            var cln = toSend.Clone();
#pragma warning disable CS8601 // Possible null reference assignment.
            cln.OriginalTrainerName = tradeDetails.OT;
#pragma warning restore CS8601 // Possible null reference assignment.
            cln.SetDisplayTID((uint)tradeDetails.TID);
            cln.SetDisplaySID((uint)tradeDetails.SID);
            cln.Language = (int)LanguageID.English; // Set the appropriate language ID
            ClearOTTrash(cln, tradeDetails);
            if (!toSend.IsNicknamed)
                cln.ClearNickname();
            if (toSend.IsShiny)
                cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

            if (!toSend.ChecksumValid)
                cln.RefreshChecksum();
            var tradelgpe = new LegalityAnalysis(cln);
            if (tradelgpe.Valid)
            {
                Log("El Pokémon es válido, aplicando Auto OT.");
                return Task.FromResult<PB7?>(cln);
            }
            else
            {
                Log("El Pokémon no válido, no aplica Auto OT.");
                Log(tradelgpe.Report());
                return Task.FromResult<PB7?>(null);
            }
        }
        else
        {
            Log("No se encontraron detalles comerciales para el OT del entrenador en cuestión.");
            return Task.FromResult<PB7?>(null);
        }
    }

    private static void ClearOTTrash(PB7 pokemon, TradeCodeStorage.TradeCodeDetails? tradeDetails)
    {
        if (tradeDetails?.OT == null)
        {
            LogUtil.LogInfo("AutoOT", "Los detalles comerciales o OT son nulos. Omitir ClearOTTrash.");
            return;
        }

        Span<byte> trash = pokemon.OriginalTrainerTrash;
        trash.Clear();
        string name = tradeDetails.OT;
        int maxLength = trash.Length / 2;
        int actualLength = Math.Min(name.Length, maxLength);
        for (int i = 0; i < actualLength; i++)
        {
            char value = name[i];
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
