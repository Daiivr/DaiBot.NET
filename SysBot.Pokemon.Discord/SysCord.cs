using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Discord.GatewayIntents;
using static SysBot.Pokemon.DiscordSettings;

namespace SysBot.Pokemon.Discord;

public static class SysCordSettings
{
    public static PokeTradeHubConfig HubConfig { get; internal set; } = default!;

    public static DiscordManager Manager { get; internal set; } = default!;

    public static DiscordSettings Settings => Manager.Config;
}

public sealed class SysCord<T> where T : PKM, new()
{
    public readonly PokeTradeHub<T> Hub;

    private readonly Dictionary<ulong, ulong> _announcementMessageIds = [];

    private readonly DiscordSocketClient _client;

    // Keep the CommandService and DI container around for use with commands.
    // These two types require you install the Discord.Net.Commands package.
    private readonly CommandService _commands;

    private readonly IServiceProvider _services;

    private readonly HashSet<string> _validCommands = new HashSet<string>
    {
        "trade", "t", "clone", "fixOT", "fix", "f", "dittoTrade", "ditto", "dt", "itemTrade", "item", "it",
        "egg", "Egg", "hidetrade", "ht", "batchTrade", "bt", "batchtradezip", "btz", "listevents", "le",
        "eventrequest", "er", "battlereadylist", "brl", "battlereadyrequest", "brr", "pokepaste", "pp",
        "PokePaste", "PP", "randomteam", "rt", "RandomTeam", "Rt", "specialrequestpokemon", "srp",
        "queueStatus", "qs", "queueClear", "qc", "ts", "tc", "deleteTradeCode", "dtc", "mysteryegg", "me"
    };

    private readonly DiscordManager Manager;

    public SysCord(PokeBotRunner<T> runner)
    {
        Runner = runner;
        Hub = runner.Hub;
        Manager = new DiscordManager(Hub.Config.Discord);
        foreach (var bot in runner.Hub.Bots.ToArray())
        {
            if (bot is ITradeBot tradeBot)
            {
                tradeBot.ConnectionError += async (sender, ex) => await HandleBotStop();
                tradeBot.ConnectionSuccess += async (sender, e) => await HandleBotStart();
            }
        }
        SysCordSettings.Manager = Manager;
        SysCordSettings.HubConfig = Hub.Config;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            // How much logging do you want to see?
            LogLevel = LogSeverity.Info,
            GatewayIntents = Guilds | GuildMessages | DirectMessages | GuildMembers | GuildPresences | MessageContent,

            // If you or another service needs to do anything with messages
            // (ex. checking Reactions, checking the content of edited/deleted messages),
            // you must set the MessageCacheSize. You may adjust the number as needed.
            MessageCacheSize = 500,
        });

        _commands = new CommandService(new CommandServiceConfig
        {
            // Again, log level:
            LogLevel = LogSeverity.Info,

            // This makes commands get run on the task thread pool instead on the websocket read thread.
            // This ensures long-running logic can't block the websocket connection.
            DefaultRunMode = Hub.Config.Discord.AsyncCommands ? RunMode.Async : RunMode.Sync,

            // There's a few more properties you can set,
            // for example, case-insensitive commands.
            CaseSensitiveCommands = false,
        });

        // Subscribe the logging handler to both the client and the CommandService.
        _client.Log += Log;
        _commands.Log += Log;

        // Setup your DI container.
        _services = ConfigureServices();

        _client.PresenceUpdated += Client_PresenceUpdated;
    }

    public static PokeBotRunner<T> Runner { get; private set; } = default!;

    // Track loading of Echo/Logging channels, so they aren't loaded multiple times.
    private bool MessageChannelsLoaded { get; set; }

    public async Task AnnounceBotStatus(string status, EmbedColorOption color)
    {
        // Check the BotEmbedStatus setting before proceeding
        if (!SysCordSettings.Settings.BotEmbedStatus)
            return;

        var botName = SysCordSettings.HubConfig.BotName;
        if (string.IsNullOrEmpty(botName))
            botName = "SysBot";

        var fullStatusMessage = $"**Estado**: {botName} esta {status}!";

        var thumbnailUrl = status == "En línea"
            ? "https://raw.githubusercontent.com/bdawg1989/sprites/main/botgo.png"
            : "https://raw.githubusercontent.com/bdawg1989/sprites/main/botstop.png";

        var embed = new EmbedBuilder()
            .WithTitle("Informe de estado del bot")
            .WithDescription(fullStatusMessage)
            .WithColor(EmbedColorConverter.ToDiscordColor(color))
            .WithThumbnailUrl(thumbnailUrl)
            .WithTimestamp(DateTimeOffset.Now)
            .Build();

        // Iterate over whitelisted channels and send the announcement
        foreach (var channelId in SysCordSettings.Manager.WhitelistedChannels.List.Select(channel => channel.ID))
        {
            IMessageChannel? channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                // If not found in cache, try fetching directly
                channel = await _client.Rest.GetChannelAsync(channelId) as IMessageChannel;
                if (channel == null)
                {
                    LogUtil.LogText($"AnnounceBotStatus: no se pudo encontrar el canal con ID {channelId} incluso después de la búsqueda directa.");
                    continue;
                }
            }

            try
            {
                // Check if there's a previous announcement message in this channel
                if (_announcementMessageIds.TryGetValue(channelId, out ulong messageId))
                {
                    // Try to delete the previous announcement message
                    try
                    {
                        await channel.DeleteMessageAsync(messageId);
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogText($"AnnounceBotStatus: excepción al eliminar el mensaje anterior en el canal {channelId}: {ex.Message}");
                    }
                }

                // Send the new announcement and store the message ID
                var message = await channel.SendMessageAsync(embed: embed);
                _announcementMessageIds[channelId] = message.Id;
                LogUtil.LogText($"AnnounceBotStatus: {fullStatusMessage} anunciado en el canal {channelId}.");

                // Update channel name with emoji based on bot status
                if (SysCordSettings.Settings.ChannelStatusConfig.EnableChannelStatus)
                {
                    var emoji = status == "En línea" ? SysCordSettings.Settings.ChannelStatusConfig.OnlineEmoji : SysCordSettings.Settings.ChannelStatusConfig.OfflineEmoji;
                    var channelName = ((ITextChannel)channel).Name;
                    var updatedChannelName = $"{emoji}{SysCord<T>.TrimStatusEmoji(channelName)}";
                    await ((ITextChannel)channel).ModifyAsync(x => x.Name = updatedChannelName);
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogText($"AnnounceBotStatus: Excepción al enviar mensaje al canal {channelId}: {ex.Message}");
            }
        }
    }

    public async Task HandleBotStart()
    {
        await AnnounceBotStatus("En línea", EmbedColorOption.Green);
    }

    public async Task HandleBotStop()
    {
        await AnnounceBotStatus("Desconectado", EmbedColorOption.Red);
    }

    public async Task InitCommands()
    {
        var assembly = Assembly.GetExecutingAssembly();

        await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
        foreach (var t in assembly.DefinedTypes.Where(z => z.IsSubclassOf(typeof(ModuleBase<SocketCommandContext>)) && z.IsGenericType))
        {
            var genModule = t.MakeGenericType(typeof(T));
            await _commands.AddModuleAsync(genModule, _services).ConfigureAwait(false);
        }
        var modules = _commands.Modules.ToList();

        var blacklist = Hub.Config.Discord.ModuleBlacklist
            .Replace("Module", "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(z => z.Trim()).ToList();

        foreach (var module in modules)
        {
            var name = module.Name;
            name = name.Replace("Module", "");
            var gen = name.IndexOf('`');
            if (gen != -1)
                name = name[..gen];
            if (blacklist.Any(z => z.Equals(name, StringComparison.OrdinalIgnoreCase)))
                await _commands.RemoveModuleAsync(module).ConfigureAwait(false);
        }

        // Subscribe a handler to see if a message invokes a command.
        _client.Ready += LoadLoggingAndEcho;
        _client.MessageReceived += HandleMessageAsync;
    }

    public async Task MainAsync(string apiToken, CancellationToken token)
    {
        // Centralize the logic for commands into a separate method.
        await InitCommands().ConfigureAwait(false);

        // Login and connect.
        await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);

        var app = await _client.GetApplicationInfoAsync().ConfigureAwait(false);
        Manager.Owner = app.Owner.Id;

        try
        {
            // Wait infinitely so your bot actually stays connected.
            await MonitorStatusAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Handle the cancellation and perform cleanup tasks
            LogUtil.LogText("MainAsync: El bot se está desconectando debido a una cancelación...");
            await AnnounceBotStatus("Offline", EmbedColorOption.Red);
            LogUtil.LogText("MainAsync: Tareas de limpieza completadas.");
        }
        finally
        {
            // Disconnect the bot
            await _client.StopAsync();
        }
    }

    // If any services require the client, or the CommandService, or something else you keep on hand,
    // pass them as parameters into this method as needed.
    // If this method is getting pretty long, you can separate it out into another file using partials.
    private static ServiceProvider ConfigureServices()
    {
        var map = new ServiceCollection();//.AddSingleton(new SomeServiceClass());

        // When all your required services are in the collection, build the container.
        // Tip: There's an overload taking in a 'validateScopes' bool to make sure
        // you haven't made any mistakes in your dependency graph.
        return map.BuildServiceProvider();
    }

    // Example of a logging handler. This can be reused by add-ons
    // that ask for a Func<LogMessage, Task>.

    private static ConsoleColor GetTextColor(LogSeverity sv) => sv switch
    {
        LogSeverity.Critical => ConsoleColor.Red,
        LogSeverity.Error => ConsoleColor.Red,

        LogSeverity.Warning => ConsoleColor.Yellow,
        LogSeverity.Info => ConsoleColor.White,

        LogSeverity.Verbose => ConsoleColor.DarkGray,
        LogSeverity.Debug => ConsoleColor.DarkGray,
        _ => Console.ForegroundColor,
    };

    private static Task Log(LogMessage msg)
    {
        var text = $"[{msg.Severity,8}] {msg.Source}: {msg.Message} {msg.Exception}";
        Console.ForegroundColor = GetTextColor(msg.Severity);
        Console.WriteLine($"{DateTime.Now,-19} {text}");
        Console.ResetColor();

        LogUtil.LogText($"SysCord: {text}");

        return Task.CompletedTask;
    }

    private static async Task RespondToThanksMessage(SocketUserMessage msg)
    {
        var channel = msg.Channel;
        await channel.TriggerTypingAsync();
        await Task.Delay(1500);

        var responses = new List<string>
        {
            "De nada! ❤️",
            "No hay problema!",
            "En cualquier momento, encantado de ayudar.!",
            "Es un placer! ❤️",
            "No hay problema. De nada!",
            "Siempre a su disposición.",
            "Me alegra haber podido ayudar.",
            "¡Feliz de servir!",
            "Por supuesto. No hay de qué.",
            "¡Claro que sí!"
        };

        var randomResponse = responses[new Random().Next(responses.Count)];
        var finalResponse = $"{randomResponse}";

        await msg.Channel.SendMessageAsync(finalResponse).ConfigureAwait(false);
    }

    private static string TrimStatusEmoji(string channelName)
    {
        var onlineEmoji = SysCordSettings.Settings.ChannelStatusConfig.OnlineEmoji;
        var offlineEmoji = SysCordSettings.Settings.ChannelStatusConfig.OfflineEmoji;

        if (channelName.StartsWith(onlineEmoji))
        {
            return channelName[onlineEmoji.Length..].Trim();
        }

        if (channelName.StartsWith(offlineEmoji))
        {
            return channelName[offlineEmoji.Length..].Trim();
        }

        return channelName.Trim();
    }

    private Task Client_PresenceUpdated(SocketUser user, SocketPresence before, SocketPresence after)
    {
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage msg)
            return;

        if (msg.Channel is SocketGuildChannel guildChannel)
        {
            if (Manager.BlacklistedServers.Contains(guildChannel.Guild.Id))
            {
                await guildChannel.Guild.LeaveAsync();
                return;
            }
        }

        if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot)
            return;

        string thanksText = msg.Content.ToLower();
        if (SysCordSettings.Settings.ReplyToThanks && (thanksText.Contains("thank") || thanksText.Contains("thx") || thanksText.Contains("gracias")))
        {
            await SysCord<T>.RespondToThanksMessage(msg).ConfigureAwait(false);
            return;
        }

        var correctPrefix = SysCordSettings.Settings.CommandPrefix;
        var content = msg.Content;
        var argPos = 0;

        if (msg.HasMentionPrefix(_client.CurrentUser, ref argPos) || msg.HasStringPrefix(correctPrefix, ref argPos))
        {
            var context = new SocketCommandContext(_client, msg);
            var handled = await TryHandleCommandAsync(msg, context, argPos);
            if (handled)
                return;
        }
        else if (content.Length > 1 && content[0] != correctPrefix[0])
        {
            var potentialPrefix = content[0].ToString();
            var command = content.Split(' ')[0][1..];

            if (_validCommands.Contains(command))
            {
                var response = await msg.Channel.SendMessageAsync($"<a:no:1206485104424128593> Lo siento {msg.Author.Mention}, usaste el prefijo incorrecto! El comando correcto es: **{correctPrefix}{command}**").ConfigureAwait(false);
                _ = Task.Delay(5000).ContinueWith(async _ =>
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                    await response.DeleteAsync().ConfigureAwait(false);
                });
                return;
            }
        }

        if (msg.Attachments.Count > 0)
        {
            await TryHandleAttachmentAsync(msg).ConfigureAwait(false);
        }
    }

    private async Task LoadLoggingAndEcho()
    {
        if (MessageChannelsLoaded)
            return;

        // Restore Echoes
        EchoModule.RestoreChannels(_client, Hub.Config.Discord);

        // Restore Logging
        LogModule.RestoreLogging(_client, Hub.Config.Discord);
        TradeStartModule<T>.RestoreTradeStarting(_client);

        // Don't let it load more than once in case of Discord hiccups.
        await Log(new LogMessage(LogSeverity.Info, "LoadLoggingAndEcho()", "Canales de registro y eco cargados!")).ConfigureAwait(false);
        MessageChannelsLoaded = true;

        var game = Hub.Config.Discord.BotGameStatus;
        if (!string.IsNullOrWhiteSpace(game))
            await _client.SetGameAsync(game).ConfigureAwait(false);
    }

    private async Task MonitorStatusAsync(CancellationToken token)
    {
        const int Interval = 20; // seconds

        // Check datetime for update
        UserStatus state = UserStatus.Idle;
        while (!token.IsCancellationRequested)
        {
            var time = DateTime.Now;
            var lastLogged = LogUtil.LastLogged;
            if (Hub.Config.Discord.BotColorStatusTradeOnly)
            {
                var recent = Hub.Bots.ToArray()
                    .Where(z => z.Config.InitialRoutine.IsTradeBot())
                    .MaxBy(z => z.LastTime);
                lastLogged = recent?.LastTime ?? time;
            }
            var delta = time - lastLogged;
            var gap = TimeSpan.FromSeconds(Interval) - delta;

            bool noQueue = !Hub.Queues.Info.GetCanQueue();
            if (gap <= TimeSpan.Zero)
            {
                var idle = noQueue ? UserStatus.DoNotDisturb : UserStatus.Idle;
                if (idle != state)
                {
                    state = idle;
                    await _client.SetStatusAsync(state).ConfigureAwait(false);
                }
                await Task.Delay(2_000, token).ConfigureAwait(false);
                continue;
            }

            var active = noQueue ? UserStatus.DoNotDisturb : UserStatus.Online;
            if (active != state)
            {
                state = active;
                await _client.SetStatusAsync(state).ConfigureAwait(false);
            }
            await Task.Delay(gap, token).ConfigureAwait(false);
        }
    }

    private async Task TryHandleAttachmentAsync(SocketMessage msg)
    {
        var mgr = Manager;
        var cfg = mgr.Config;
        if (cfg.ConvertPKMToShowdownSet && (cfg.ConvertPKMReplyAnyChannel || mgr.CanUseCommandChannel(msg.Channel.Id)))
        {
            if (msg is SocketUserMessage userMessage)
            {
                foreach (var att in msg.Attachments)
                    await msg.Channel.RepostPKMAsShowdownAsync(att, userMessage).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, SocketCommandContext context, int pos)
    {
        try
        {
            var AbuseSettings = Hub.Config.TradeAbuse;
            // Check if the user is in the bannedIDs list
            if (msg.Author is SocketGuildUser user && AbuseSettings.BannedIDs.List.Any(z => z.ID == user.Id))
            {
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, "Tienes prohibido usar este bot.").ConfigureAwait(false);
                return true;
            }

            var mgr = Manager;
            if (!mgr.CanUseCommandUser(msg.Author.Id))
            {
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, "No tiene permitido usar este comando").ConfigureAwait(false);
                return true;
            }

            if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != mgr.Owner)
            {
                if (Hub.Config.Discord.ReplyCannotUseCommandInChannel)
                    await SysCord<T>.SafeSendMessageAsync(msg.Channel, "No puedes usar ese comando aquí.").ConfigureAwait(false);
                return true;
            }

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Servidor desconocido";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"Ejecutando el comando desde {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Contenido: {msg}")).ConfigureAwait(false);

            var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

            if (result.Error == CommandError.UnknownCommand)
                return false;

            if (!result.IsSuccess)
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, result.ErrorReason).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "Command", $"Error al ejecutar el comando: {ex.Message}", ex)).ConfigureAwait(false);
            return false;
        }
    }

    private static async Task SafeSendMessageAsync(IMessageChannel channel, string message)
    {
        try
        {
            await channel.SendMessageAsync(message).ConfigureAwait(false);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.InsufficientPermissions) // Missing Permissions
        {
            await Log(new LogMessage(LogSeverity.Warning, "Command", $"Faltan permisos para poder enviar mensajes en el canal {channel.Name}")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "Command", $"Error al enviar el mensaje: {ex.Message}", ex)).ConfigureAwait(false);
        }
    }
}
