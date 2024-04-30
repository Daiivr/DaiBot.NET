using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Borra y alterna las funciones de la cola.")]
public class QueueModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("queueStatus")]
    [Alias("qs", "ts")]
    [Summary("Comprueba la posición del usuario en la cola.")]
    public async Task GetTradePositionAsync()
    {
        var userID = Context.User.Id;
        var tradeEntry = Info.GetDetail(userID);

        string msg;
        if (tradeEntry != null)
        {
            var uniqueTradeID = tradeEntry.UniqueTradeID;
            msg = Context.User.Mention + " - " + Info.GetPositionString(userID, uniqueTradeID);
        }
        else
        {
            msg = Context.User.Mention + " - Actualmente no estás en la cola.";
        }

        await ReplyAndDeleteAsync(msg, 5, Context.Message).ConfigureAwait(false);
    }

    [Command("queueClear")]
    [Alias("qc", "tc")]
    [Summary("Borra al usuario de las colas comerciales. No eliminará a un usuario si está siendo procesado.")]
    public async Task ClearTradeAsync()
    {
        string msg = ClearTrade(Context.User.Id);
        await ReplyAndDeleteAsync(msg, 5, Context.Message).ConfigureAwait(false);
    }


    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas comerciales. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("ID de usuario de discord")] ulong id)
    {
        string msg = ClearTrade(id);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas comerciales. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("Nombre de usuario de la persona a borrar")] string _)
    {
        foreach (var user in Context.Message.MentionedUsers)
        {
            string msg = ClearTrade(user.Id);
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas comerciales. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync()
    {
        var users = Context.Message.MentionedUsers;
        if (users.Count == 0)
        {
            await ReplyAsync("<a:warning:1206483664939126795> Ningún usuario fue mencionado").ConfigureAwait(false);
            return;
        }
        foreach (var u in users)
            await ClearTradeUserAsync(u.Id).ConfigureAwait(false);
    }

    [Command("queueClearAll")]
    [Alias("qca", "tca")]
    [Summary("Borra a todos los usuarios de las colas comerciales.")]
    [RequireSudo]
    public async Task ClearAllTradesAsync()
    {
        Info.ClearAllQueues();
        await ReplyAsync("<a:yes:1206485105674166292> Borrados todo en la cola de espera.").ConfigureAwait(false);
    }

    [Command("queueToggle")]
    [Alias("qt", "tt")]
    [Summary("Activa/desactiva la posibilidad de unirse a la cola comercial.")]
    [RequireSudo]
    public Task ToggleQueueTradeAsync()
    {
        var state = Info.ToggleQueue();
        var msg = state
            ? "<a:yes:1206485105674166292> **Configuración de cola modificada**: Los usuarios ahora __pueden unirse__ a la **cola**."
            : "<a:warning:1206483664939126795> **Configuración de cola modificada**: Los usuarios __**NO PUEDEN**__ unirse a la `cola` hasta que se vuelva a `habilitar`.";

        return Context.Channel.EchoAndReply(msg);
    }

    [Command("queueMode")]
    [Alias("qm")]
    [Summary("Cambia la forma en que se controlan las colas (manual/umbral/intervalo).")]
    [RequireSudo]
    public async Task ChangeQueueModeAsync([Summary("Queue mode")] QueueOpening mode)
    {
        SysCord<T>.Runner.Hub.Config.Queues.QueueToggleMode = mode;
        await ReplyAsync($"<a:yes:1206485105674166292> Modo de cola cambiado a {mode}.").ConfigureAwait(false);
    }

    [Command("queueList")]
    [Alias("ql")]
    [Summary("Envía al MD la lista de usuarios en la cola.")]
    [RequireSudo]
    public async Task ListUserQueue()
    {
        var lines = SysCord<T>.Runner.Hub.Queues.Info.GetUserList("(ID {0}) - Code: {1} - {2} - {3}");
        var msg = string.Join("\n", lines);
        if (msg.Length < 3)
            await ReplyAsync("La lista de espera está vacía.").ConfigureAwait(false);
        else
            await Context.User.SendMessageAsync(msg).ConfigureAwait(false);
    }

    [Command("addTradeCode")]
    [Alias("atc")]
    [Summary("Almacena un código comercial para el usuario.")]
    public async Task AddTradeCodeAsync([Summary("The trade code to store.")] int tradeCode)
    {
        var user = Context.User; // Obtiene el objeto IUser que representa al usuario.
        var userID = user.Id;

        // Validate the trade code range before adding
        if (tradeCode < 0 || tradeCode > 99999999)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {user.Mention}, lo siento, el código de comercio debe estar entre **00000000** y **99999999**.").ConfigureAwait(false);
        }
        else
        {
            string msg = QueueModule<T>.AddTradeCode(userID, tradeCode, user.Mention);

            // Sends the message to the user's DM.
            await user.SendMessageAsync(msg).ConfigureAwait(false);
        }

        // Attempt to delete the command message if possible.
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    private static string AddTradeCode(ulong userID, int tradeCode, string userMention)
    {
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.SetTradeCode(userID, tradeCode);

        if (success)
            return $"<a:yes:1206485105674166292> {userMention}, tu código de comercio **{tradeCode}** ha sido almacenado correctamente.";
        else
        {
            // Obtén el código de comercio existente.
            int existingTradeCode = tradeCodeStorage.GetTradeCode(userID);
            return $"<a:warning:1206483664939126795> {userMention}, ya tienes un codigo de tradeo establecido y es **{existingTradeCode}**,\nSi deseas cambiarlo usa `{botPrefix}utc` seguido del nuevo código.";
        }
    }

    [Command("updateTradeCode")]
    [Alias("utc")]
    [Summary("Actualiza el código comercial almacenado para el usuario.")]
    public async Task UpdateTradeCodeAsync([Summary("The new trade code to update.")] int newTradeCode)
    {
        var user = Context.User; // Obtiene el objeto IUser que representa al usuario.
        var userID = user.Id;
        // Validate the trade code range before updating
        if (newTradeCode < 0 || newTradeCode > 99999999)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {user.Mention}, lo siento, el código de comercio debe estar entre **00000000** y **99999999**.").ConfigureAwait(false);
        }
        else
        {
            string msg = QueueModule<T>.UpdateTradeCode(userID, newTradeCode, user.Mention);

            // Sends the message to the user's DM.
            await user.SendMessageAsync(msg).ConfigureAwait(false);
        }

        // Attempt to delete the command message if possible.
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    private static string UpdateTradeCode(ulong userID, int newTradeCode, string userMention)
    {
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.UpdateTradeCode(userID, newTradeCode);

        if (success)
            return $"<a:yes:1206485105674166292> {userMention}, tu código de tradeo se ha actualizado correctamente a **{newTradeCode}**.";
        else
            return $"<a:warning:1206483664939126795> {userMention}, hubo un problema al actualizar tu código de comercio. Por favor, intenta de nuevo.";
    }

    [Command("deleteTradeCode")]
    [Alias("dtc")]
    [Summary("Elimina el código comercial almacenado para el usuario.")]
    public async Task DeleteTradeCodeAsync()
    {
        var usermention = Context.User.Mention;
        var userID = Context.User.Id;
        string msg = QueueModule<T>.DeleteTradeCode(userID, usermention);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    private static string DeleteTradeCode(ulong userID, string userMention)
    {
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.DeleteTradeCode(userID);

        if (success)
            return $"<a:yes:1206485105674166292> {userMention}, Su código de tradeo se ha eliminado correctamente.";
        else
            return $"<a:warning:1206483664939126795> {userMention} No se encontró ningún código de tradeo para su ID de usuario.";
    }

    private static string ClearTrade(ulong userID)
    {
        var result = Info.ClearTrade(userID);
        return GetClearTradeMessage(result);
    }

    private async Task ReplyAndDeleteAsync(string message, int delaySeconds, IMessage? messageToDelete = null)
    {
        try
        {
            var sentMessage = await ReplyAsync(message).ConfigureAwait(false);
            _ = DeleteMessagesAfterDelayAsync(sentMessage, messageToDelete, delaySeconds);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(QueueModule<T>));
        }
    }

    private async Task DeleteMessagesAfterDelayAsync(IMessage sentMessage, IMessage? messageToDelete, int delaySeconds)
    {
        try
        {
            await Task.Delay(delaySeconds * 1000);
            await sentMessage.DeleteAsync();
            if (messageToDelete != null)
                await messageToDelete.DeleteAsync();
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(QueueModule<T>));
        }
    }

    private static string GetClearTradeMessage(QueueResultRemove result)
    {
        return result switch
        {
            QueueResultRemove.Removed => $"<a:yes:1206485105674166292> Eliminé tus operaciones pendientes de la cola.",
            QueueResultRemove.CurrentlyProcessing => "<a:warning:1206483664939126795> Parece que actualmente tienes operaciones en proceso! No lass eliminé de la cola.",
            QueueResultRemove.CurrentlyProcessingRemoved => "<a:warning:1206483664939126795> Parece que tiene operaciones en proceso. Se han eliminado otras operaciones pendientes de la cola.",
            QueueResultRemove.NotInQueue => "<a:warning:1206483664939126795> Lo sentimos, actualmente no estás en la lista.",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }
}
