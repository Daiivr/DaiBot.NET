using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class EncounterBotFossilSWSH : EncounterBotSWSH
{
    private static readonly PK8 Blank = new();

    private readonly IDumper DumpSetting;

    private readonly FossilSettings Settings;

    public EncounterBotFossilSWSH(PokeBotState Config, PokeTradeHub<PK8> hub) : base(Config, hub)
    {
        Settings = Hub.Config.EncounterSWSH.Fossil;
        DumpSetting = Hub.Config.Folder;
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(new PokeTradeHubConfig(), t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
    }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);

        Log("Comprobando el recuento de items...");
        var pouchData = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
        var counts = FossilCount.GetFossilCounts(pouchData);
        int reviveCount = counts.PossibleRevives(Settings.Species);
        if (reviveCount == 0)
        {
            Log("Piezas fósiles insuficientes. Primero obtenga al menos una de cada pieza fósil requerida.");
            return;
        }
        Log($"Hay suficientes piezas fósiles disponibles para revivir {reviveCount} {Settings.Species}.");

        while (!token.IsCancellationRequested)
        {
            if (encounterCount != 0 && encounterCount % reviveCount == 0)
            {
                Log($"Se quedaron sin fósiles para revivir {Settings.Species}.");
                if (Settings.InjectWhenEmpty)
                {
                    Log("Restaurando los datos originales de la bolsa.");
                    await Connection.WriteBytesAsync(pouchData, ItemTreasureAddress, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                else
                {
                    Log("Los restos fósiles se han agotado. Reiniciando el juego.");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                    await SetupBoxState(DumpSetting, token).ConfigureAwait(false);
                }
            }

            await ReviveFossil(counts, token).ConfigureAwait(false);
            Log("Fósil revivió. Comprobando detalles...");

            var pk = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (pk.Species == 0 || !pk.ChecksumValid)
            {
                Log("No se encontró ningún fósil en la casilla 1, ranura 1. Asegúrate de que el grupo esté lleno. Reiniciando bucle.");
                continue;
            }

            if (await HandleEncounter(pk, token).ConfigureAwait(false))
                return;

            Log("Borrando la ranura de destino.");
            await SetBoxPokemon(Blank, 0, 0, token).ConfigureAwait(false);
        }
    }

    private async Task ReviveFossil(FossilCount count, CancellationToken token)
    {
        Log("Iniciando rutina de recuperación de fósiles...");
        if (GameLang == LanguageID.Spanish)
            await Click(A, 0_900, token).ConfigureAwait(false);

        await Click(A, 1_100, token).ConfigureAwait(false);

        // French is slightly slower.
        if (GameLang == LanguageID.French)
            await Task.Delay(0_200, token).ConfigureAwait(false);

        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting first fossil.
        if (count.UseSecondOption1(Settings.Species))
            await Click(DDOWN, 0_300, token).ConfigureAwait(false);
        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting second fossil.
        if (count.UseSecondOption2(Settings.Species))
            await Click(DDOWN, 300, token).ConfigureAwait(false);

        // A spam through accepting the fossil and agreeing to revive.
        for (int i = 0; i < 16; i++)
            await Click(A, 0_200, token).ConfigureAwait(false);

        // Safe to mash B from here until we get out of all menus.
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await Click(B, 0_200, token).ConfigureAwait(false);
    }
}
