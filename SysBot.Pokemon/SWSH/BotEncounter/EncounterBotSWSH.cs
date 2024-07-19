using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon;

public abstract class EncounterBotSWSH : PokeRoutineExecutor8SWSH, IEncounterBot
{
    public readonly IReadOnlyList<string> UnwantedMarks;

    protected readonly byte[] BattleMenuReady = [0, 0, 0, 255];

    protected readonly PokeTradeHub<PK8> Hub;

    protected int encounterCount;

    // Cached offsets that stay the same per session.
    protected ulong OverworldOffset;

    private readonly int[] DesiredMaxIVs;

    private readonly int[] DesiredMinIVs;

    private readonly IDumper DumpSetting;

    private readonly EncounterSettings Settings;

    private bool IsWaiting;

    protected EncounterBotSWSH(PokeBotState Config, PokeTradeHub<PK8> hub) : base(Config)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSWSH;
        DumpSetting = Hub.Config.Folder;
        StopConditionSettings.InitializeTargetIVs(Hub.Config, out DesiredMinIVs, out DesiredMaxIVs);
        StopConditionSettings.ReadUnwantedMarks(Hub.Config.StopConditions, out UnwantedMarks);
    }

    public ICountSettings Counts => Settings;

    public void Acknowledge() => IsWaiting = false;

    public override async Task HardStop()
    {
        await ResetStick(CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    public override async Task MainLoop(CancellationToken token)
    {
        var settings = Hub.Config.EncounterSWSH;
        Log("Identificando los datos del entrenador de la consola host.");
        var sav = await IdentifyTrainer(token).ConfigureAwait(false);
        await InitializeHardware(settings, token).ConfigureAwait(false);

        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);

        try
        {
            Log($"Iniciando el bucle principal {GetType().Name}.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);
            await EncounterLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Finalizando el bucle {GetType().Name}.");
        await HardStop().ConfigureAwait(false);
    }

    protected abstract Task EncounterLoop(SAV8SWSH sav, CancellationToken token);

    protected async Task FleeToOverworld(CancellationToken token)
    {
        // This routine will always escape a battle.
        await Click(DUP, 0_200, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);

        while (await IsInBattle(token).ConfigureAwait(false))
        {
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }
    }

    // return true if breaking loop
    protected async Task<bool> HandleEncounter(PK8 pk, CancellationToken token)
    {
        encounterCount++;
        var print = StopConditionSettings.GetPrintName(pk);
        Log($"Encuentro: {encounterCount}{Environment.NewLine}{print}{Environment.NewLine}");

        var folder = IncrementAndGetDumpFolder(pk);
        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, folder, pk);

        if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, UnwantedMarks))
            return false;

        if (Hub.Config.StopConditions.CaptureVideoClip)
        {
            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
            await PressAndHold(CAPTURE, 2_000, 0, token).ConfigureAwait(false);
        }

        var mode = Settings.ContinueAfterMatch;
        var msg = $"Resultado encontrado!\n{print}\n" + GetModeMessage(mode);

        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
        EchoUtil.Echo(msg);

        if (mode == ContinueAfterMatch.StopExit)
            return true;
        if (mode == ContinueAfterMatch.Continue)
            return false;

        IsWaiting = true;
        while (IsWaiting)
            await Task.Delay(1_000, token).ConfigureAwait(false);
        return false;
    }

    protected Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        return SetStick(LEFT, 0, 0, 0_500, token); // reset
    }

    private static string GetModeMessage(ContinueAfterMatch mode) => mode switch
    {
        ContinueAfterMatch.Continue => "Continuando...",
        ContinueAfterMatch.PauseWaitAcknowledge => "Esperando instrucciones para continuar.",
        ContinueAfterMatch.StopExit => "Detener la ejecución de rutina; reinicia el bot para buscar nuevamente.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "El tipo de resultado de la coincidencia no era válido."),
    };

    private string IncrementAndGetDumpFolder(PK8 pk)
    {
        var legendary = SpeciesCategory.IsLegendary(pk.Species) || SpeciesCategory.IsMythical(pk.Species) || SpeciesCategory.IsSubLegendary(pk.Species);
        if (legendary)
        {
            Settings.AddCompletedLegends();
            return "legends";
        }

        if (pk.IsEgg)
        {
            Settings.AddCompletedEggs();
            return "egg";
        }

        if (pk.Species is >= (int)Species.Dracozolt and <= (int)Species.Arctovish)
        {
            Settings.AddCompletedFossils();
            return "fossil";
        }

        Settings.AddCompletedEncounters();
        return "encounters";
    }
}
