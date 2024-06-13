using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Borra y alterna las funciones de la cola.")]
public class QueueModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("queueMode")]
    [Alias("qm")]
    [Summary("Cambia la forma en que se controlan las colas (manual/umbral/intervalo).")]
    [RequireSudo]
    public async Task ChangeQueueModeAsync([Summary("Queue mode")] QueueOpening mode)
    {
        SysCord<T>.Runner.Hub.Config.Queues.QueueToggleMode = mode;
        await ReplyAsync($"<a:yes:1206485105674166292> Modo de cola cambiado a {mode}.").ConfigureAwait(false);
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
    public async Task ClearTradeUserAsync([Summary("Discord user ID")] ulong id)
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
    [Summary("Clears the user from the trade queues. Will not remove a user if they are being processed.")]
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

    [Command("addTradeCode")]
    [Alias("atc")]
    [Summary("Almacena un código comercial para el usuario.")]
    public async Task AddTradeCodeAsync([Summary("El código de comercio para almacenar.")] int tradeCode)
    {
        var user = Context.User as IUser;
        var userID = user.Id;

        if (tradeCode < 0 || tradeCode > 99999999)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {user.Mention}, lo siento, el código de comercio debe estar entre **00000000** y **99999999**.").ConfigureAwait(false);
        }
        else
        {
            var formattedCode = FormatTradeCode(tradeCode); // Formatea el código con espacio.
            await AddTradeCode(userID, tradeCode, user, formattedCode); // Asegúrate de actualizar este método para usar el código formateado.
        }

        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    // Este método debe ser actualizado para aceptar y manejar 'formattedCode'
    private static async Task AddTradeCode(ulong userID, int tradeCode, IUser user, string formattedCode)
    {
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.SetTradeCode(userID, tradeCode);

        var embedBuilder = new EmbedBuilder();

        if (success)
        {
            embedBuilder.WithColor(Color.Green)
                        .WithTitle("Código de Comercio Almacenado")
                        .WithDescription($"<a:yes:1206485105674166292> {user.Mention}, tu código de comercio ha sido almacenado correctamente.\n\n__**Código:**__\n# {formattedCode}")
                        .WithThumbnailUrl("https://i.imgur.com/Zs9hmNq.gif");
        }
        else
        {
            int existingTradeCode = tradeCodeStorage.GetTradeCode(userID);
            string formattedExistingCode = FormatTradeCode(existingTradeCode);
            embedBuilder.WithColor(Color.Red)
                        .WithTitle("Código de Comercio Existente")
                        .WithDescription($"<a:Error:1223766391958671454> {user.Mention}, ya tienes un código de comercio establecido.")
                        .AddField("__**Código Existente**__", $"Tu código actual es:\n __**{formattedExistingCode}**__", true)
                        .AddField("\u200B", "\u200B", true)
                        .AddField("__**Solución**__", $"Si deseas cambiarlo, usa `{botPrefix}utc` seguido del nuevo código.", true)
                        .WithThumbnailUrl("https://i.imgur.com/haOeRR9.gif");
        }

        await user.SendMessageAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
    }

    [Command("updateTradeCode")]
    [Alias("utc")]
    [Summary("Actualiza el código comercial almacenado para el usuario.")]
    public async Task UpdateTradeCodeAsync([Summary("The new trade code to update.")] int newTradeCode)
    {
        var user = Context.User;
        var userID = user.Id;
        // Validate the trade code range before updating
        if (newTradeCode < 0 || newTradeCode > 99999999)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {user.Mention}, lo siento, el código de comercio debe estar entre **00000000** y **99999999**.").ConfigureAwait(false);
        }
        else
        {
            var formattedCode = FormatTradeCode(newTradeCode); // Formatea el nuevo código con espacio.
            await UpdateTradeCode(userID, newTradeCode, user, formattedCode); // Pasa el código formateado a la función de actualización.
        }

        // Attempt to delete the command message if possible.
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    private static async Task UpdateTradeCode(ulong userID, int newTradeCode, IUser user, string formattedCode)
    {
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.UpdateTradeCode(userID, newTradeCode);

        var embedBuilder = new EmbedBuilder();

        if (success)
        {
            embedBuilder.WithColor(Color.Green)
                        .WithTitle("Código de Comercio Actualizado")
                        .WithDescription($"<a:yes:1206485105674166292> {user.Mention}, tu código de comercio se ha actualizado correctamente.\n\n__**Nuevo Código:**__\n# **{formattedCode}**")
                        .WithThumbnailUrl("https://i.imgur.com/Zs9hmNq.gif");
        }
        else
        {
            embedBuilder.WithColor(Color.Red)
                        .WithTitle("Error al Actualizar Código de Comercio")
                        .WithDescription($"<a:Error:1223766391958671454> {user.Mention}, hubo un problema al actualizar tu código de comercio.")
                        .AddField("__**Razón**__", $"Al parecer, aún no has establecido un **código** de tradeo permanente.", true)
                        .AddField("\u200B", "\u200B", true)
                        .AddField("__**Solución**__", $"Si deseas establecer un **código**, usa `{botPrefix}atc` seguido del código.", true)
                        .WithThumbnailUrl("https://i.imgur.com/haOeRR9.gif");
        }

        await user.SendMessageAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
    }

    [Command("deleteTradeCode")]
    [Alias("dtc")]
    [Summary("Elimina el código comercial almacenado para el usuario.")]
    public async Task DeleteTradeCodeAsync()
    {
        var user = Context.User; // Obtiene el objeto IUser que representa al usuario.
        var userID = user.Id;
        await DeleteTradeCode(userID, user); // Invoca directamente el método que maneja la eliminación del código.

        // Intenta eliminar el mensaje del comando si es posible.
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    private static async Task DeleteTradeCode(ulong userID, IUser user)
    {
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.DeleteTradeCode(userID);

        var embedBuilder = new EmbedBuilder();

        if (success)
        {
            embedBuilder.WithColor(Color.Green)
                        .WithTitle("Código de Comercio Eliminado")
                        .WithDescription($"<a:yes:1206485105674166292> {user.Mention}, tu código de comercio se ha eliminado correctamente.")
                        .WithThumbnailUrl("https://i.imgur.com/Zs9hmNq.gif");
        }
        else
        {
            embedBuilder.WithColor(Color.Red)
                        .WithTitle("Error al Eliminar Código de Comercio")
                        .WithDescription($"<a:Error:1223766391958671454> {user.Mention}, no se pudo eliminar tu código de comercio.")
                        .AddField("__**Razón**__", $"Es posible que no tengas un **código** de comercio establecido.", true)
                        .AddField("\u200B", "\u200B", true)
                        .AddField("__**Solución**__", $"Para establecer un **código**, usa `{botPrefix}atc` seguido del código que deseas.", true)
                        .WithThumbnailUrl("https://i.imgur.com/haOeRR9.gif");
        }

        await user.SendMessageAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
    }

    public static string FormatTradeCode(int code)
    {
        string codeStr = code.ToString("D8"); // Asegura que el código siempre tenga 8 dígitos.
        return codeStr.Substring(0, 4) + " " + codeStr.Substring(4, 4); // Inserta un espacio después de los primeros 4 dígitos.
    }

    private static string ClearTrade(ulong userID)
    {
        var result = Info.ClearTrade(userID);
        return GetClearTradeMessage(result);
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
}
