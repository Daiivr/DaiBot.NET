using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Pone en cola nuevos intercambios de clonacion")]
public class CloneModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync(int code)
    {
        // Check if the user is already in the queue
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
            return;
        }

        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, false, lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("<a:loading:1210133423050719283> Procesando su solicitud de clonación...").ConfigureAwait(false);

        // Use a fire-and-forget approach for the delay and deletion
        _ = Task.Delay(2000).ContinueWith(async _ =>
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);

            if (confirmationMessage != null)
                await confirmationMessage.DeleteAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync([Summary("Trade Code")][Remainder] string code)
    {
        // Check if the user is already in the queue
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
            return;
        }

        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(userID) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, false, lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("<a:loading:1210133423050719283> Procesando su solicitud de clonación...").ConfigureAwait(false);

        // Use a fire-and-forget approach for the delay and deletion
        _ = Task.Delay(2000).ContinueWith(async _ =>
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);

            if (confirmationMessage != null)
                await confirmationMessage.DeleteAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync()
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return CloneAsync(code);
    }

    [Command("cloneList")]
    [Alias("cl", "cq")]
    [Summary("Imprime los usuarios en la cola de clonación.")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Clone);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "Pending Trades";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("📝 Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
    }
}
