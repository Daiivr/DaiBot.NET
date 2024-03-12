using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsLGPE;

namespace SysBot.Pokemon;

public class PokeTradeBotLGPE(PokeTradeHub<PB7> Hub, PokeBotState Config) : PokeRoutineExecutor7LGPE(Config), ICountBot
{
    private readonly TradeSettings TradeSettings = Hub.Config.Trade;
    public readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

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

            Log("Identifying trainer data of the host console.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);


            Log($"Starting main {nameof(PokeTradeBotLGPE)} loop.");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Ending {nameof(PokeTradeBotLGPE)} loop.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        UpdateBarrier(false);
        await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
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
                Log("No task assigned. Waiting for new task assignment.");
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
            Log($"Starting next {type}{tradetype} Bot Trade. Getting data...");
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
            Log("Nothing to check, waiting for new users...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            await Click(B, 1_000, token).ConfigureAwait(false);
        else
            await Task.Delay(1_000, token).ConfigureAwait(false);
    }
    protected virtual (PokeTradeDetail<PB7>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
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
        if (toSend.Species != 0)
            await WriteBoxPokemon(toSend, 0, 0, token);
        if (!await IsOnOverworldStandard(token))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }
        await Click(X, 2000, token).ConfigureAwait(false);
        Log("Opening Menu...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("Selecting Communicate......");
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
                Log("Opening Menu......");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("Selecting Communicate......");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }


        }
        await Task.Delay(2000);
        Log("Selecting Faraway Connection......");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(poke, token);
        poke.TradeSearching(this);
        Log($"Searching for user {poke.Trainer.TrainerName}");
        await Task.Delay(3000);
        var btimeout = new Stopwatch();
        btimeout.Restart();

        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"{poke.Trainer.TrainerName} not found");


                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"{poke.Trainer.TrainerName} Found");
        await Task.Delay(10000);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);

        if (tradepartnersav.OT != sav.OT)
        {
            Log($"Found Link Trade Partner: {tradepartnersav.OT}, TID: {tradepartnersav.TID16}, SID: {tradepartnersav.SID16}, Game: {tradepartnersav.Version}");
            var modifiedPokemon = await SetPkmWithTradePartnerDetails(toSend, tradepartnersav, token);
            if (modifiedPokemon != null)
            {
                poke.TradeData = modifiedPokemon; // Update the Pokémon to be traded with the modified version
                poke.SendNotification(this, $"Entrenador encontrado: **{tradepartnersav.OT}**,\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradepartnersav.TID16}__\n **SID**: __{tradepartnersav.SID16}__\n **Juego**: __{tradepartnersav.Version}__\n▲");
            }
        }

        if (tradepartnersav2.OT != sav.OT)
        {
            Log($"Found Link Trade Partner: {tradepartnersav2.OT}, TID: {tradepartnersav2.TID16}, SID: {tradepartnersav2.SID16}");
            var modifiedPokemon = await SetPkmWithTradePartnerDetails(toSend, tradepartnersav2, token); 
            if (modifiedPokemon != null)
            {
                poke.TradeData = modifiedPokemon; // Update the Pokémon to be traded with the modified version
                poke.SendNotification(this, $"Entrenador encontrado: **{tradepartnersav2.OT}**,\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradepartnersav2.TID16}__\n **SID**: __{tradepartnersav2.SID16}__\n **Juego**: __{tradepartnersav.Version}__\n▲");
            }
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
        Log("Waiting on Trade Screen...");

        await Task.Delay(15_000).ConfigureAwait(false);
        var tradeResult = await ConfirmAndStartTrading(poke, 0, token);
        if (tradeResult != PokeTradeResult.Success)
        {
            if (tradeResult == PokeTradeResult.TrainerLeft)
                Log("Trade canceled because trainer left the trade.");
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
            Log("User did not complete the trade.");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("User completed the trade.");
        poke.TradeFinished(this, received);


        // Still need to wait out the trade animation.

        for (var i = 0; i < 30; i++)
            await Click(B, 0_500, token).ConfigureAwait(false);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }
    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PB7> detail, int slot, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
        Log("Confirming and initiating trade...");
        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < 10; i++)
        {

            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                return PokeTradeResult.TrainerLeft;
            await Click(A, 1_500, token).ConfigureAwait(false);
        }

        var tradeCounter = 0;
        Log("Checking for received pokemon in slot 1");
        while (true)
        {

            var newEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                Log("Change detected in slot 1");
                await Task.Delay(15_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }

            tradeCounter++;

            if (tradeCounter >= Hub.Config.Trade.TradeConfiguration.TradeAnimationMaxDelaySeconds)
            {
                // If we don't detect a B1S1 change, the trade didn't go through in that time.
                Log("Did not detect a change in slot 1.");
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
                continue;
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
        Log("Opening Menu...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("Selecting Communicate...");
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
                Log("Opening Menu...");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("Selecting Communicate...");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }


        }
        await Task.Delay(2000);
        Log("Selecting Faraway Connection...");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(detail, token);
        detail.TradeSearching(this);
        Log($"Searching for user {detail.Trainer.TrainerName}");
        var btimeout = new Stopwatch();
        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                detail.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"{detail.Trainer.TrainerName} not found");


                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"{detail.Trainer.TrainerName} Found");
        await Task.Delay(10000);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);
        if (tradepartnersav.OT != sav.OT)
        {
            Log($"Found Link Trade Parter: {tradepartnersav.OT}, TID: {tradepartnersav.DisplayTID}, SID: {tradepartnersav.DisplaySID},Game: {tradepartnersav.Version}");
            detail.SendNotification(this, $"Entrenador encontrado: **{tradepartnersav.OT}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{tradepartnersav.DisplayTID}__\n **SID**: __{tradepartnersav.DisplaySID}__\n **Juego**: __{tradepartnersav.Version}__\n▲");
        }
        if (tradepartnersav2.OT != sav.OT)
        {
            Log($"Found Link Trade Parter: {tradepartnersav2.OT}, TID: {tradepartnersav2.DisplayTID}, SID: {tradepartnersav2.DisplaySID}");
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
            Log("Waiting on trade screen...");

            await Task.Delay(10_000).ConfigureAwait(false);
            detail.SendNotification(this, "<a:warning:1206483664939126795> Te quedan 5 segundos para llegar a la pantalla de operaciones y no interrumpir la operación.");
            await Task.Delay(5_000);
            var tradeResult = await ConfirmAndStartTrading(detail, clonelist.IndexOf(t), token);
            if (tradeResult != PokeTradeResult.Success)
            {
                if (tradeResult == PokeTradeResult.TrainerLeft)
                    Log("Trade canceled because trainer left the trade.");
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
            Log($"Joined the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            Hub.BotSync.Barrier.RemoveParticipant();
            Log($"Left the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
        }
    }
    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("Unexpected behavior, recovering position.");
        int ctr = 120_000;
        while (!await IsOnOverworldStandard(token))
        {
            if (ctr < 0)
            {
                await RestartGameLGPE(Hub.Config, token).ConfigureAwait(false);
                return;
            }

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworldStandard(token))
                return;


            await Click(BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen ? A : B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworldStandard(token))
                return;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworldStandard(token))
                return;

            ctr -= 3_000;
        }
    }

    private async Task<PB7> SetPkmWithTradePartnerDetails(PB7 toSend, SAV7b tradePartnerSav, CancellationToken token)
    {
        var cln = (PB7)toSend.Clone();

        cln.OriginalTrainerName = tradePartnerSav.OT;
        cln.TID16 = tradePartnerSav.TID16;
        cln.SID16 = tradePartnerSav.SID16;
        cln.Language = tradePartnerSav.Language;
        cln.ClearNickname();
        cln.RefreshChecksum();

        var tradelgpe = new LegalityAnalysis(cln);
        if (tradelgpe.Valid)
        {
            Log("Pokemon is valid, applying AutoOT.");
            return cln; // Return the modified clone for use in the trade
        }
        else
        {
            Log("Pokemon not valid, not applying AutoOT.");
            return null;
        }
    }

    }

