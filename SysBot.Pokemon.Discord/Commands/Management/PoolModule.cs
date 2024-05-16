using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Distribution Pool Module")]
public class PoolModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("poolReload")]
    [Summary("Vuelve a cargar el grupo de bots desde la carpeta de configuración.")]
    [RequireSudo]
    public async Task ReloadPoolAsync()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;

        var pool = hub.Ledy.Pool.Reload(hub.Config.Folder.DistributeFolder);
        if (!pool)
            await ReplyAsync("<a:warning:1206483664939126795> Fallo al recargar desde carpeta.").ConfigureAwait(false);
        else
            await ReplyAsync($"<a:yes:1206485105674166292> Recargado desde carpeta. Recuento de grupos: {hub.Ledy.Pool.Count}").ConfigureAwait(false);
    }

    [Command("pool")]
    [Summary("Muestra los detalles de los archivos Pokémon en el grupo aleatorio.")]
    public async Task DisplayPoolCountAsync()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var pool = hub.Ledy.Pool;
        var count = pool.Count;
        if (count is > 0 and < 20)
        {
            var lines = pool.Files.Select((z, i) => $"{i + 1:00}: {z.Key} = {(Species)z.Value.Request.RequestInfo.Species}");
            var msg = string.Join("\n", lines);

            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = $"Count: {count}";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Pool Details", embed: embed.Build()).ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync($"Pool Count: {count}").ConfigureAwait(false);
        }
    }
}
