using Discord;
using Discord.Net;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;
using System;

namespace SysBot.Pokemon.Discord;

[Summary("Pone en cola nuevas operaciones de dump")]
public class DumpModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("dump")]
    [Alias("d")]
    [Summary("Dump los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync(int code)
    {
        if (await CheckUserInQueueAsync())
            return;

        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();
        await QueueHelper<T>.AddToQueueAsync(
            Context,
            code,
            Context.User.Username,
            sig,
            new T(),
            PokeRoutineType.Dump,
            PokeTradeType.Dump,
            Context.User,
            isBatchTrade: false,
            batchTradeNumber: 1,
            totalBatchTrades: 1,
            isMysteryEgg: false,
            lgcode: lgcode);

        _ = DeleteMessageAsync(Context.Message, 2000);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("Dump los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync([Summary("Trade Code")][Remainder] string code)
    {
        if (await CheckUserInQueueAsync())
            return;

        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(Context.User.Id) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("Dump los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync()
    {
        if (await CheckUserInQueueAsync())
            return;

        var code = Info.GetRandomTradeCode(Context.User.Id);
        await DumpAsync(code);
    }

    [Command("dumpList")]
    [Alias("dl", "dq")]
    [Summary("Imprime los usuarios en la cola de Dump.")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Dump);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "Tradeos pendientes";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("📝 Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
    }

    private async Task<bool> CheckUserInQueueAsync()
    {
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            var currentTime = DateTime.UtcNow;
            var formattedTime = currentTime.ToString("hh:mm tt");

            var queueEmbed = new EmbedBuilder
            {
                Color = Color.Red,
                ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif");

            // Añadir un field al Embed para indicar el error
            queueEmbed.AddField("__**Error**__:", $"<a:no:1206485104424128593> {Context.User.Mention} No pude agregarte a la cola", true);
            queueEmbed.AddField("__**Razón**__:", "No puedes agregar más operaciones hasta que la actual se procese.", true);
            queueEmbed.AddField("__**Solución**__:", "Espera un poco hasta que la operación existente se termine e intentalo de nuevo.");

            queueEmbed.Footer = new EmbedFooterBuilder
            {
                Text = $"{Context.User.Username} • {formattedTime}",
                IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
            };

            await ReplyAsync(embed: queueEmbed.Build()).ConfigureAwait(false);
            return true;
        }
        return false;
    }

    private static async Task DeleteMessageAsync(IMessage message, int delay)
    {
        await Task.Delay(delay);
        try
        {
            await message.DeleteAsync();
        }
        catch (HttpException)
        {
            // Ignore exceptions if the message was already deleted or we don't have permission
        }
    }
}
