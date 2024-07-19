using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public class RemoteControlBotSV(PokeBotState Config) : PokeRoutineExecutor9SV(Config)
{
    public override async Task HardStop()
    {
        await SetStick(SwitchStick.LEFT, 0, 0, 0_500, CancellationToken.None).ConfigureAwait(false); // reset
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            Log("Identificando los datos del entrenador de la consola host.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Iniciando el bucle principal, luego esperando comandos.");
            Config.IterateNextRoutine();
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                ReportStatus();
            }
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Finalizando el bucle {nameof(RemoteControlBotSV)}.");
        await HardStop().ConfigureAwait(false);
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

    private class DummyReset : IBotStateSettings
    {
        public bool ScreenOff => true;
    }
}
