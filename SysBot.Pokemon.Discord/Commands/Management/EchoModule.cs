using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class EchoModule : ModuleBase<SocketCommandContext>
{
    private class EchoChannel(ulong ChannelId, string ChannelName, Action<string> Action)
    {
        public readonly ulong ChannelID = ChannelId;
        public readonly string ChannelName = ChannelName;
        public readonly Action<string> Action = Action;
    }

    private static readonly Dictionary<ulong, EchoChannel> Channels = [];

    public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
    {
        foreach (var ch in cfg.EchoChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddEchoChannel(c, ch.ID);
        }

        EchoUtil.Echo("✔ Añadida notificación de eco a canal(es) de Discord al iniciar el Bot.");
    }

    [Command("echoHere")]
    [Summary("Makes the echo special messages to the channel.")]
    [RequireSudo]
    public async Task AddEchoAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("⚠️ Ya se está notificando aquí").ConfigureAwait(false);
            return;
        }

        AddEchoChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.EchoChannels.AddIfNew(new[] { GetReference(Context.Channel) });
        await ReplyAsync("✔ ¡Añadida la salida Eco a este canal!").ConfigureAwait(false);
    }

    private static void AddEchoChannel(ISocketMessageChannel c, ulong cid)
    {
        void Echo(string msg) => c.SendMessageAsync(msg);

        Action<string> l = Echo;
        EchoUtil.Forwarders.Add(l);
        var entry = new EchoChannel(cid, c.Name, l);
        Channels.Add(cid, entry);
    }

    public static bool IsEchoChannel(ISocketMessageChannel c)
    {
        var cid = c.Id;
        return Channels.TryGetValue(cid, out _);
    }

    [Command("echoInfo")]
    [Summary("Dumps the special message (Echo) settings.")]
    [RequireSudo]
    public async Task DumpEchoInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("echoClear")]
    [Summary("Clears the special message echo settings in that specific channel.")]
    [RequireSudo]
    public async Task ClearEchosAsync()
    {
        var id = Context.Channel.Id;
        if (!Channels.TryGetValue(id, out var echo))
        {
            await ReplyAsync("No hay eco en este canal.").ConfigureAwait(false);
            return;
        }
        EchoUtil.Forwarders.Remove(echo.Action);
        Channels.Remove(Context.Channel.Id);
        SysCordSettings.Settings.EchoChannels.RemoveAll(z => z.ID == id);
        await ReplyAsync($"Ecos eliminados del canal: {Context.Channel.Name}").ConfigureAwait(false);
    }

    [Command("echoClearAll")]
    [Summary("Clears all the special message Echo channel settings.")]
    [RequireSudo]
    public async Task ClearEchosAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"Eco borrado de {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
            EchoUtil.Forwarders.Remove(entry.Action);
        }
        EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
        Channels.Clear();
        SysCordSettings.Settings.EchoChannels.Clear();
        await ReplyAsync("¡Ecos eliminados de todos los canales!").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };
}
