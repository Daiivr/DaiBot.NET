using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class SudoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("blacklist")]
    [Summary("Incluye en la lista negra a un usuario de Discord mencionado.")]
    [RequireSudo]
    // ReSharper disable once UnusedParameter.Global
    public async Task BlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("blacklistComment")]
    [Summary("Agrega un comentario para una ID de usuario de Discord incluida en la lista negra.")]
    [RequireSudo]
    // ReSharper disable once UnusedParameter.Global
    public async Task BlackListUsers(ulong id, [Remainder] string comment)
    {
        var obj = SysCordSettings.Settings.UserBlacklist.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No se puede encontrar un usuario con ese ID: ({id}).").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"<a:yes:1206485105674166292> Listo. Cambiado el comentario existente **({oldComment})** a **({comment})**.").ConfigureAwait(false);
    }

    [Command("unblacklist")]
    [Summary("Elimina un usuario de Discord mencionado de la lista negra.")]
    [RequireSudo]
    // ReSharper disable once UnusedParameter.Global
    public async Task UnBlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => objects.Any(o => o.ID == z.ID));
        await ReplyAsync("Done.").ConfigureAwait(false);
    }

    [Command("blacklistId")]
    [Summary("Incluye en la lista negra los ID de usuario de Discord. (Útil si el usuario no está en el servidor).")]
    [RequireSudo]
    public async Task BlackListIDs([Summary("Comma Separated Discord IDs")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("unBlacklistId")]
    [Summary("Elimina las ID de usuario de Discord de la lista negra. (Útil si el usuario no está en el servidor).")]
    [RequireSudo]
    public async Task UnBlackListIDs([Summary("Comma Separated Discord IDs")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("blacklistSummary")]
    [Alias("printBlacklist", "blacklistPrint")]
    [Summary("Muestra la lista de usuarios de Discord incluidos en la lista negra.")]
    [RequireSudo]
    public async Task PrintBlacklist()
    {
        var lines = SysCordSettings.Settings.UserBlacklist.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("banID")]
    [Summary("Prohíbe las identificaciones de usuarios en línea.")]
    [RequireSudo]
    public async Task BanOnlineIDs([Summary("Comma Separated Online IDs")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);

        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.AddIfNew(objects);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("bannedIDComment")]
    [Summary("Agrega un comentario para una identificación de usuario en línea prohibida.")]
    [RequireSudo]
    public async Task BanOnlineIDs(ulong id, [Remainder] string comment)
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var obj = hub.Config.TradeAbuse.BannedIDs.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No se puede encontrar un usuario con ese ID en línea: ({id}).").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"<a:yes:1206485105674166292> Listo. Cambiado el comentario existente **({oldComment})** a **({comment})**.").ConfigureAwait(false);
    }

    [Command("unbanID")]
    [Summary("Desbanea las identificaciones de usuarios en línea.")]
    [RequireSudo]
    public async Task UnBanOnlineIDs([Summary("Comma Separated Online IDs")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("bannedIDSummary")]
    [Alias("printBannedID", "bannedIDPrint")]
    [Summary("Muestra la lista de identificaciones en línea prohibidas.")]
    [RequireSudo]
    public async Task PrintBannedOnlineIDs()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var lines = hub.Config.TradeAbuse.BannedIDs.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("forgetUser")]
    [Alias("forget")]
    [Summary("Perdona a los usuarios que se encontraron anteriormente.")]
    [RequireSudo]
    public async Task ForgetPreviousUser([Summary("Comma Separated Online IDs")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        foreach (var ID in IDs)
        {
            PokeRoutineExecutorBase.PreviousUsers.RemoveAllNID(ID);
            PokeRoutineExecutorBase.PreviousUsersDistribution.RemoveAllNID(ID);
        }
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("previousUserSummary")]
    [Alias("prevUsers")]
    [Summary("Muestra una lista de usuarios encontrados anteriormente.")]
    [RequireSudo]
    public async Task PrintPreviousUsers()
    {
        bool found = false;
        var lines = PokeRoutineExecutorBase.PreviousUsers.Summarize().ToList();
        if (lines.Count != 0)
        {
            found = true;
            var msg = "Usuarios anteriores:\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }

        lines = PokeRoutineExecutorBase.PreviousUsersDistribution.Summarize().ToList();
        if (lines.Count != 0)
        {
            found = true;
            var msg = "Usuarios de distribución anteriores:\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }
        if (!found)
            await ReplyAsync("<a:warning:1206483664939126795> No se han encontrado usuarios anteriores.").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(IUser channel) => new()
    {
        ID = channel.Id,
        Name = channel.Username,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };

    private RemoteControlAccess GetReference(ulong id) => new()
    {
        ID = id,
        Name = "Manual",
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };

    protected static IEnumerable<ulong> GetIDs(string content)
    {
        return content.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries)
            .Select(z => ulong.TryParse(z, out var x) ? x : 0).Where(z => z != 0);
    }
}
