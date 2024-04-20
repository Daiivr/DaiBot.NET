using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public class TradeStartModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private class TradeStartAction(ulong ChannelId, Action<PokeRoutineExecutorBase, PokeTradeDetail<T>> messager, string channel)
        : ChannelAction<PokeRoutineExecutorBase, PokeTradeDetail<T>>(ChannelId, messager, channel);

    private static DiscordSocketClient _discordClient;

    private static readonly Dictionary<ulong, TradeStartAction> Channels = [];

    private static void Remove(TradeStartAction entry)
    {
        Channels.Remove(entry.ChannelID);
        SysCord<T>.Runner.Hub.Queues.Forwarders.Remove(entry.Action);
    }

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
    public static void RestoreTradeStarting(DiscordSocketClient discord)
    {
        _discordClient = discord; // Store the DiscordSocketClient instance

        var cfg = SysCordSettings.Settings;
        foreach (var ch in cfg.TradeStartingChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddLogChannel(c, ch.ID);
        }

        LogUtil.LogInfo("Added Trade Start Notification to Discord channel(s) on Bot startup.", "Discord");
    }

    public static bool IsStartChannel(ulong cid)
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
    {
        return Channels.TryGetValue(cid, out _);
    }

    [Command("startHere")]
    [Summary("Makes the bot log trade starts to the channel.")]
    [RequireSudo]
    public async Task AddLogAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("<a:warning:1206483664939126795> Ya se está registrando aquí.").ConfigureAwait(false);
            return;
        }

        AddLogChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.TradeStartingChannels.AddIfNew(new[] { GetReference(Context.Channel) });
        await ReplyAsync("<a:yes:1206485105674166292> ¡Añadida salida de Notificación de Inicio a este canal!").ConfigureAwait(false);
    }

    private static void AddLogChannel(ISocketMessageChannel c, ulong cid)
    {
        async void Logger(PokeRoutineExecutorBase bot, PokeTradeDetail<T> detail)
        {
            if (detail.Type == PokeTradeType.Random)
                return;

            var user = _discordClient.GetUser(detail.Trainer.ID);

            if (user == null)
            {
                Console.WriteLine($"User not found for ID {detail.Trainer.ID}.");
                return;
            }
            string speciesName = "";
            if (detail.TradeData != null)
            {
                speciesName = GameInfo.Strings.Species[detail.TradeData.Species];
            }
            string ballName = "";
            if (detail.TradeData != null)
            {
                var strings = GameInfo.GetStrings(1);
                ballName = strings.balllist[detail.TradeData.Ball];

                if (ballName.Contains("(LA)"))
                {
                    ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
                }
                else
                {
                    ballName = ballName.Replace(" ", "").ToLower();
                }
            }
            string ballImgUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/AltBallImg/28x28/{ballName}.png";

            string tradeTitle, embedImageUrl;
            if (detail.IsMysteryEgg)
            {
                tradeTitle = "✨ Huevo misterioso Shiny ✨";
                embedImageUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/mysteryegg2.png";
            }
            else
            {
                switch (detail.Type)
                {
                    case PokeTradeType.Clone:
                        tradeTitle = "Solicitud de Clonació";
                        embedImageUrl = "https://i.imgur.com/aSTCjUn.png";
                        break;
                    case PokeTradeType.Dump:
                        tradeTitle = "Solicitud de Dump";
                        embedImageUrl = "https://i.imgur.com/9wfEHwZ.png";
                        break;
                    case PokeTradeType.FixOT:
                        tradeTitle = "Solicitud de FixOT";
                        embedImageUrl = "https://i.imgur.com/gRZGFIi.png";
                        break;
                    case PokeTradeType.Seed:
                        tradeTitle = "Solicitud Especial";
                        embedImageUrl = "https://i.imgur.com/EI1BHr5.png";
                        break;
                    default:
                        tradeTitle = $"{speciesName}";
                        embedImageUrl = "";
                        break;
                }
            }
            if (string.IsNullOrEmpty(embedImageUrl) && detail.TradeData != null)
            {
                embedImageUrl = AbstractTrade<T>.PokeImg(detail.TradeData, false, true);
            }

            var (r, g, b) = await GetDominantColorAsync(embedImageUrl);
            var embedColor = new DiscordColor(r, g, b);

            var embedBuilder = new EmbedBuilder()
                .WithColor(embedColor)
                .WithThumbnailUrl(embedImageUrl)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName($"Siguiente: {user.Username}")
                    .WithIconUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()))
                .WithDescription($"**Procesando**: {tradeTitle}\n**Trade ID**: {detail.ID}")
                .WithFooter(new EmbedFooterBuilder()
                    .WithText($"Iniciando el comercio ahora. Disfrute de su {speciesName}!\u200B")
                    .WithIconUrl(ballImgUrl))
                .WithTimestamp(DateTime.Now);


            var embed = embedBuilder.Build();
            await c.SendMessageAsync(embed: embed);
        }

        Action<PokeRoutineExecutorBase, PokeTradeDetail<T>> l = Logger;
        SysCord<T>.Runner.Hub.Queues.Forwarders.Add(l);

        var entry = new TradeStartAction(cid, l, c.Name);
        Channels.Add(cid, entry);
    }

    private static Embed CreateTradeEmbed(PokeRoutineExecutorBase bot, PokeTradeDetail<T> detail)
    {
        var builder = new EmbedBuilder
        {
            Color = DiscordColor.Green, // Customize the color of the embed
            Description = $"> <a:yes:1206485105674166292> **{bot.Connection.Label}** ahora está tradeando con **{detail.Trainer.TrainerName}**.",
            Footer = new EmbedFooterBuilder
            {
                Text = $"ID: {detail.ID} | {DateTime.Now:hh:mm:ss}",
            }
        };

        // Add an icon for the embed title
        var iconUrl = "https://styles.redditmedia.com/t5_2rmov/styles/communityIcon_8jj0o28zosp21.png?width=256&s=8a23b0dcec4abbaa98df5c1f9270b08da7f960e3"; // Replace with the URL of the icon you want to use for the embed
        builder.WithAuthor("Notificacion de Inicio de Tradeo", iconUrl);

        return builder.Build();
    }

    [Command("startInfo")]
    [Summary("Dumps the Start Notification settings.")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("startClear")]
    [Summary("Clears the Start Notification settings in that specific channel.")]
    [RequireSudo]
    public async Task ClearLogsAsync()
    {
        var cfg = SysCordSettings.Settings;
        if (Channels.TryGetValue(Context.Channel.Id, out var entry))
            Remove(entry);
        cfg.TradeStartingChannels.RemoveAll(z => z.ID == Context.Channel.Id);
        await ReplyAsync($"<a:yes:1206485105674166292> Inicio Notificaciones borradas del canal: {Context.Channel.Name}").ConfigureAwait(false);
    }

    [Command("startClearAll")]
    [Summary("Clears all the Start Notification settings.")]
    [RequireSudo]
    public async Task ClearLogsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"<a:yes:1206485105674166292> Registro borrado de: {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
            SysCord<T>.Runner.Hub.Queues.Forwarders.Remove(entry.Action);
        }
        Channels.Clear();
        SysCordSettings.Settings.TradeStartingChannels.Clear();
        await ReplyAsync("<a:yes:1206485105674166292> ¡Notificaciones de inicio borradas de todos los canales!").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };
    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixelColor = image.GetPixel(x, y);

                    if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                    var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                    var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                    var combinedFactor = brightnessFactor + saturationFactor;

                    var quantizedColor = Color.FromArgb(
                        pixelColor.R / 10 * 10,
                        pixelColor.G / 10 * 10,
                        pixelColor.B / 10 * 10
                    );

                    if (colorCount.ContainsKey(quantizedColor))
                    {
                        colorCount[quantizedColor] += combinedFactor;
                    }
                    else
                    {
                        colorCount[quantizedColor] = combinedFactor;
                    }
                }
            }

            image.Dispose();

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"Error processing image from {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            using var stream = await response.Content.ReadAsStreamAsync();
            return new Bitmap(stream);
        }
        else
        {
            return new Bitmap(imagePath);
        }
    }
}
