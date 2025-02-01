using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Discord.GatewayIntents;
using static SysBot.Pokemon.DiscordSettings;
using Discord.Net;
using Newtonsoft.Json;

namespace SysBot.Pokemon.Discord;

public static class SysCordSettings
{
    public static PokeTradeHubConfig HubConfig { get; internal set; } = default!;

    public static DiscordManager Manager { get; internal set; } = default!;

    public static DiscordSettings Settings => Manager.Config;
}

public sealed class SysCord<T> where T : PKM, new()
{
    private const string StatsFilePath = "user_stats.json"; // Path to the stats file
    public readonly PokeTradeHub<T> Hub;
    private readonly ProgramConfig _config;
    private readonly Dictionary<ulong, ulong> _announcementMessageIds = [];
    private readonly DiscordSocketClient _client;
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

    public SysCord(PokeBotRunner<T> runner, ProgramConfig config)
    {
        Runner = runner;
        Hub = runner.Hub;
        Manager = new DiscordManager(Hub.Config.Discord);
        _config = config;

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
            // MessageCacheSize = 50,
        });

        _commands = new CommandService(new CommandServiceConfig
        {
            // Again, log level:
            LogLevel = LogSeverity.Info,

            // This makes commands get run on the task thread pool instead on the websocket read thread.
            // This ensures long-running logic can't block the websocket connection.
            DefaultRunMode = RunMode.Async,

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

        _client.Disconnected += (exception) =>
        {
            LogUtil.LogText($"Se perdió la conexión de Discord. Motivo: {exception?.Message ?? "Desconocido"}");
            Task.Run(() => ReconnectAsync());
            return Task.CompletedTask;
        };
    }

    public static PokeBotRunner<T> Runner { get; private set; } = default!;

    // Track loading of Echo/Logging channels, so they aren't loaded multiple times.
    private bool MessageChannelsLoaded { get; set; }

    private async Task ReconnectAsync()
    {
        const int maxRetries = 5;
        const int delayBetweenRetries = 5000; // 5 seconds
        const int initialDelay = 10000; // 10 seconds

        // Initial delay to allow Discord's automatic reconnection
        await Task.Delay(initialDelay).ConfigureAwait(false);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (_client.ConnectionState == ConnectionState.Connected)
                {
                    LogUtil.LogText("El cliente se reconecta automáticamente.");
                    return; // Already reconnected
                }

                // Check if the client is in the process of reconnecting
                if (_client.ConnectionState == ConnectionState.Connecting)
                {
                    LogUtil.LogText("El cliente ya está intentando reconectarse.");
                    await Task.Delay(delayBetweenRetries).ConfigureAwait(false);
                    continue;
                }

                await _client.LoginAsync(TokenType.Bot, Hub.Config.Discord.Token).ConfigureAwait(false);
                await _client.StartAsync().ConfigureAwait(false);
                LogUtil.LogText("Reconectado exitosamente.");
                return;
            }
            catch (Exception ex)
            {
                LogUtil.LogText($"Intento de reconexión {i + 1} fallido: {ex.Message}");
                if (i < maxRetries - 1)
                    await Task.Delay(delayBetweenRetries).ConfigureAwait(false);
            }
        }

        // If all attempts to reconnect fail, stop and restart the bot
        LogUtil.LogText("No se pudo volver a conectar después de la cantidad máxima de intentos. Reiniciando el bot...");

        // Stop the bot
        await _client.StopAsync().ConfigureAwait(false);

        // Restart the bot
        await _client.LoginAsync(TokenType.Bot, Hub.Config.Discord.Token).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);

        LogUtil.LogText("El bot se reinició correctamente.");
    }

    public async Task AnnounceBotStatus(string status, EmbedColorOption color)
    {
        if (!SysCordSettings.Settings.BotEmbedStatus)
            return;

        var botName = string.IsNullOrEmpty(SysCordSettings.HubConfig.BotName) ? "SysBot" : SysCordSettings.HubConfig.BotName;
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

        foreach (var channelId in SysCordSettings.Manager.WhitelistedChannels.List.Select(channel => channel.ID))
        {
            try
            {
                IMessageChannel? channel = _client.GetChannel(channelId) as IMessageChannel;
                if (channel == null)
                {
                    channel = await _client.Rest.GetChannelAsync(channelId) as IMessageChannel;
                    if (channel == null)
                    {
                        LogUtil.LogInfo("SysCord", $"AnnounceBotStatus: no se pudo encontrar el canal con ID {{channelId}} incluso después de la búsqueda directa.");
                        continue;
                    }
                }

                if (_announcementMessageIds.TryGetValue(channelId, out ulong messageId))
                {
                    try
                    {
                        await channel.DeleteMessageAsync(messageId);
                    }
                    catch
                    {
                        // Ignore exception when deleting previous message
                    }
                }

                var message = await channel.SendMessageAsync(embed: embed);
                _announcementMessageIds[channelId] = message.Id;
                LogUtil.LogInfo("SysCord", $"AnnounceBotStatus: {fullStatusMessage} anunciado en el canal {channelId}.");

                if (SysCordSettings.Settings.ChannelStatusConfig.EnableChannelStatus && channel is ITextChannel textChannel)
                {
                    var emoji = status == "En línea" ? SysCordSettings.Settings.ChannelStatusConfig.OnlineEmoji : SysCordSettings.Settings.ChannelStatusConfig.OfflineEmoji;
                    var updatedChannelName = $"{emoji}{SysCord<T>.TrimStatusEmoji(textChannel.Name)}";
                    await textChannel.ModifyAsync(x => x.Name = updatedChannelName);
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo("SysCord", $"AnnounceBotStatus: Excepción en el canal {channelId}: {ex.Message}");
                // Continue to the next channel despite the exception
            }
        }
    }

    public async Task HandleBotStart()
    {
        try
        {
            await AnnounceBotStatus("En línea", EmbedColorOption.Green);
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"HandleBotStart: Excepción al anunciar el inicio del bot: {ex.Message}");
        }
    }

    public async Task HandleBotStop()
    {
        try
        {
            await AnnounceBotStatus("Desconectado", EmbedColorOption.Red);
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"HandleBotStop: Excepción al anunciar la detención del bot: {ex.Message}");
        }
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

    private static ServiceProvider ConfigureServices()
    {
        var map = new ServiceCollection();//.AddSingleton(new SomeServiceClass());

        // When all your required services are in the collection, build the container.
        // Tip: There's an overload taking in a 'validateScopes' bool to make sure
        // you haven't made any mistakes in your dependency graph.
        return map.BuildServiceProvider();
    }

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
        await Task.Delay(500).ConfigureAwait(false);

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

    private void GrantXP(string userId)
    {
        // Check if the stats file exists, and create it if it doesn't
        if (!File.Exists(StatsFilePath))
        {
            var initialStats = new Dictionary<string, UserStats>();
            var initialJson = JsonConvert.SerializeObject(initialStats, Formatting.Indented);
            File.WriteAllText(StatsFilePath, initialJson);
        }

        var json = File.ReadAllText(StatsFilePath);
        var stats = JsonConvert.DeserializeObject<Dictionary<string, UserStats>>(json);

        if (stats == null)
            return;

        if (!stats.TryGetValue(userId, out var userStats))
        {
            userStats = new UserStats { Level = 1, XP = 0, LastXPGain = DateTime.MinValue };
            stats[userId] = userStats;
        }

        // Check if the user has gained XP in the last 2 minutes
        if (DateTime.UtcNow - userStats.LastXPGain < TimeSpan.FromMinutes(2))
            return;

        // Grant random XP between 5 and 10, doubled if double XP is active
        var random = new Random();
        int xpGained = random.Next(5, 11);
        if (AdminModule.IsDoubleXPActive())
        {
            xpGained *= 2; // Double XP
        }

        userStats.XP += xpGained;
        userStats.LastXPGain = DateTime.UtcNow;

        // Check if the user has enough XP to level up
        int requiredXP = GetRequiredXPForNextLevel(userStats.Level);
        while (userStats.XP >= requiredXP)
        {
            userStats.XP -= requiredXP;
            userStats.Level++;
            requiredXP = GetRequiredXPForNextLevel(userStats.Level);
        }

        // Save the updated stats
        SaveStats(stats);
    }

    private int GetRequiredXPForNextLevel(int currentLevel)
    {
        // Base XP required for level 1 is 100, and it increases by 20% each level
        return (int)(100 * Math.Pow(1.2, currentLevel - 1));
    }

    private async Task HandleMessageAsync(SocketMessage arg)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
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
            if (SysCordSettings.Settings.ReplyToThanks && (thanksText.Contains("thank") || thanksText.Contains("thx") || thanksText.Contains("gracias") || thanksText.Contains("grax")))
            {
                await SysCord<T>.RespondToThanksMessage(msg).ConfigureAwait(false);
                return;
            }

            var correctPrefix = SysCordSettings.Settings.CommandPrefix;
            var content = msg.Content;
            var argPos = 0;

            if (msg.HasMentionPrefix(_client.CurrentUser, ref argPos) || msg.HasStringPrefix(correctPrefix, ref argPos))
            {
                // Grant XP to the user for using a command
                GrantXP(msg.Author.Id.ToString());

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
                    await SafeSendMessageAsync(msg.Channel, $"<a:no:1206485104424128593> Lo siento {msg.Author.Mention}, usaste el prefijo incorrecto! El comando correcto es: **{correctPrefix}{command}**").ConfigureAwait(false);
                    return;
                }
            }

            if (msg.Attachments.Count > 0)
            {
                await TryHandleAttachmentAsync(msg).ConfigureAwait(false);
            }
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.InsufficientPermissions) // Missing Permissions
        {
            await Log(new LogMessage(LogSeverity.Warning, "Command", $"Permisos faltantes para manejar un mensaje en el canal {arg.Channel.Name}")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "Command", $"Excepción no controlada en HandleMessageAsync: {ex.Message}", ex)).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 1000) // Log if processing takes more than 1 second
            {
                await Log(new LogMessage(LogSeverity.Warning, "Gateway",
                    $"Un controlador de mensajes recibidos está bloqueando la tarea de puerta de enlace. " +
                    $"Method: HandleMessageAsync, Tiempo de ejecución: {stopwatch.ElapsedMilliseconds}ms, " +
                    $"Contenido del mensaje: {arg.Content[..Math.Min(arg.Content.Length, 100)]}...")).ConfigureAwait(false);
            }
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
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, $"<a:no:1206485104424128593> Lo siento {msg.Author.Mention}, tienes prohibido usar este bot.").ConfigureAwait(false);
                return true;
            }

            var mgr = Manager;
            if (!mgr.CanUseCommandUser(msg.Author.Id))
            {
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, $"<a:no:1206485104424128593> Lo siento {msg.Author.Mention}, no tiene permitido usar este comando").ConfigureAwait(false);
                return true;
            }

            if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != mgr.Owner)
            {
                if (Hub.Config.Discord.ReplyCannotUseCommandInChannel)
                    await SysCord<T>.SafeSendMessageAsync(msg.Channel, $"<a:no:1206485104424128593> Lo siento {msg.Author.Mention}, no puedes usar ese comando aquí, usalo en un servidor.").ConfigureAwait(false);
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

    private void SaveStats(Dictionary<string, UserStats> stats)
    {
        var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
        File.WriteAllText(StatsFilePath, json);
    }

    public class UserStats
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Points { get; set; }
        public int XP { get; set; } // New property for XP
        public int Level { get; set; } // New property for Level
        public DateTime LastXPGain { get; set; } // New property to track the last time XP was gained
        public DateTime CooldownEnd { get; set; } // Cooldown end time
    }
}
