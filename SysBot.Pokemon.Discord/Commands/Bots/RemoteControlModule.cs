using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Remotely controls a bot.")]
public class RemoteControlModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private string GetRunningBotIP()
    {
        var r = SysCord<T>.Runner;
        var runningBot = r.Bots.Find(x => x.IsRunning);

        // Check if a running bot is found
        if (runningBot != null)
        {
            return runningBot.Bot.Config.Connection.IP;
        }
        else
        {
            // Default IP address or logic if no running bot is found
            return "192.168.1.1";
        }
    }

    [Command("click")]
    [Summary("Clicks the specified button.")]
    [RequireRoleAccess(nameof(DiscordManager.RolesRemoteControl))]
    public async Task ClickAsync(SwitchButton b)
    {
        var bot = SysCord<T>.Runner.Bots.Find(z => IsRemoteControlBot(z.Bot));
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No hay ningún bot disponible para ejecutar tu comando: {b}").ConfigureAwait(false);
            return;
        }

        await ClickAsyncImpl(b, bot).ConfigureAwait(false);
    }

    [Command("click")]
    [Summary("Clicks the specified button.")]
    [RequireSudo]
    public async Task ClickAsync(string ip, SwitchButton b)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No hay ningún bot disponible para ejecutar tu comando: {b}").ConfigureAwait(false);
            return;
        }

        await ClickAsyncImpl(b, bot).ConfigureAwait(false);
    }

    [Command("setStick")]
    [Summary("Sets the stick to the specified position.")]
    [RequireRoleAccess(nameof(DiscordManager.RolesRemoteControl))]
    public async Task SetStickAsync(SwitchStick s, short x, short y, ushort ms = 1_000)
    {
        var bot = SysCord<T>.Runner.Bots.Find(z => IsRemoteControlBot(z.Bot));
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No hay ningún bot disponible para ejecutar tu comando: {s}").ConfigureAwait(false);
            return;
        }

        await SetStickAsyncImpl(s, x, y, ms, bot).ConfigureAwait(false);
    }

    [Command("setStick")]
    [Summary("Sets the stick to the specified position.")]
    [RequireSudo]
    public async Task SetStickAsync(string ip, SwitchStick s, short x, short y, ushort ms = 1_000)
    {
        var bot = SysCord<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
            return;
        }

        await SetStickAsyncImpl(s, x, y, ms, bot).ConfigureAwait(false);
    }

    [Command("setScreenOn")]
    [Alias("screenOn", "scrOn")]
    [Summary("Turns the screen on")]
    [RequireSudo]
    public async Task SetScreenOnAsync()
    {
        await SetScreen(true).ConfigureAwait(false);
    }

    [Command("setScreenOff")]
    [Alias("screenOff", "scrOff")]
    [Summary("Turns the screen off")]
    [RequireSudo]
    public async Task SetScreenOffAsync()
    {
        await SetScreen(false).ConfigureAwait(false);
    }

    private async Task SetScreen(bool on)
    {
        string ip = GetRunningBotIP();
        var bot = GetBot(ip);
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.SetScreen(on ? ScreenState.On : ScreenState.Off, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyAsync("<a:yes:1206485105674166292> Estado de la pantalla ajustado a: " + (on ? "On" : "Off")).ConfigureAwait(false);
    }

    private static BotSource<PokeBotState>? GetBot(string ip)
    {
        var r = SysCord<T>.Runner;
        return r.GetBot(ip) ?? r.Bots.Find(x => x.IsRunning); // safe fallback for users who mistype IP address for single bot instances
    }

    private async Task ClickAsyncImpl(SwitchButton button, BotSource<PokeBotState> bot)
    {
        if (!Enum.IsDefined(typeof(SwitchButton), button))
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Valor del botón desconocido: {button}").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.Click(button, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyAsync($"{b.Connection.Name} ha realizado: {button}").ConfigureAwait(false);
    }

    private async Task SetStickAsyncImpl(SwitchStick s, short x, short y, ushort ms, BotSource<PokeBotState> bot)
    {
        if (!Enum.IsDefined(typeof(SwitchStick), s))
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Stick desconocido: {s}").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.SetStick(s, x, y, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyAsync($"{b.Connection.Name} ha realizado: {s}").ConfigureAwait(false);
        await Task.Delay(ms).ConfigureAwait(false);
        await b.Connection.SendAsync(SwitchCommand.ResetStick(s, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyAsync($"{b.Connection.Name} ha restablecido la posición del stick.").ConfigureAwait(false);
    }

    private static bool IsRemoteControlBot(RoutineExecutor<PokeBotState> botstate)
        => botstate is RemoteControlBotSWSH or RemoteControlBotBS or RemoteControlBotLA or RemoteControlBotSV;
}
