using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class LogModule : ModuleBase<SocketCommandContext>
{
    private static readonly Dictionary<ulong, ChannelLogger> Channels = [];

    public static void RestoreLogging(DiscordSocketClient discord, DiscordSettings settings)
    {
        foreach (var ch in settings.LoggingChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddLogChannel(c, ch.ID);
        }

        LogUtil.LogInfo("Added logging to Discord channel(s) on Bot startup.", "Discord");
    }

    [Command("logHere")]
    [Summary("Hace que el bot inicie sesión en el canal.")]
    [RequireSudo]
    public async Task AddLogAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("<a:warning:1206483664939126795> Ya estoy registrando aquí.").ConfigureAwait(false);
            return;
        }

        AddLogChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.LoggingChannels.AddIfNew([GetReference(Context.Channel)]);
        await ReplyAsync("<a:yes:1206485105674166292> ¡Añadida salida de registro a este canal!").ConfigureAwait(false);
    }

    [Command("logClearAll")]
    [Summary("Borra todas las configuraciones de registro.")]
    [RequireSudo]
    public async Task ClearLogsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"<a:yes:1206485105674166292> Registro borrado de: {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
            LogUtil.Forwarders.Remove(entry);
        }

        LogUtil.Forwarders.RemoveAll(y => Channels.Select(z => z.Value).Contains(y));
        Channels.Clear();
        SysCordSettings.Settings.LoggingChannels.Clear();
        await ReplyAsync("<a:yes:1206485105674166292> ¡Registro borrado de todos los canales!").ConfigureAwait(false);
    }

    [Command("logClear")]
    [Summary("Borra la configuración de registro en ese canal específico.")]
    [RequireSudo]
    public async Task ClearLogsAsync()
    {
        var id = Context.Channel.Id;
        if (!Channels.TryGetValue(id, out var log))
        {
            await ReplyAsync("<a:warning:1206483664939126795> No hay eco en este canal.").ConfigureAwait(false);
            return;
        }
        LogUtil.Forwarders.Remove(log);
        Channels.Remove(Context.Channel.Id);
        SysCordSettings.Settings.LoggingChannels.RemoveAll(z => z.ID == id);
        await ReplyAsync($"<a:yes:1206485105674166292> Registro borrado del canal: {Context.Channel.Name}").ConfigureAwait(false);
    }

    [Command("logInfo")]
    [Summary("Dump la configuración de registro.")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    private static void AddLogChannel(ISocketMessageChannel c, ulong cid)
    {
        var logger = new ChannelLogger(cid, c);
        LogUtil.Forwarders.Add(logger);
        Channels.Add(cid, logger);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };
}
