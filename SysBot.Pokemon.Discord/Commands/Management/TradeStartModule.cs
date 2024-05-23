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

    private static DiscordSocketClient? _discordClient;

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
    [Summary("Hace que el bot registre el comercio en el canal.")]
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
            if (detail.Type == PokeTradeType.Random) return;

            var user = _discordClient.GetUser(detail.Trainer.ID);
            if (user == null) { Console.WriteLine($"User not found for ID {detail.Trainer.ID}."); return; }

            string speciesName = detail.TradeData != null ? GameInfo.Strings.Species[detail.TradeData.Species] : "";
            string ballImgUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/36e891cc02fe283cd70d9fc8fef2f3c490096d6c/imgs/difficulty.png";

            if (detail.TradeData != null && detail.Type != PokeTradeType.Clone && detail.Type != PokeTradeType.Dump && detail.Type != PokeTradeType.Seed && detail.Type != PokeTradeType.FixOT)
            {
                var ballName = GameInfo.GetStrings(1).balllist[detail.TradeData.Ball]
                    .Replace(" ", "").Replace("(LA)", "").ToLower();
                ballName = ballName == "pokéball" ? "pokeball" : (ballName.Contains("(la)") ? "la" + ballName : ballName);
                ballImgUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/AltBallImg/28x28/{ballName}.png";
            }
            var embedData = DetailsExtractor<T>.ExtractPokemonDetails(detail.TradeData, user, detail.IsMysteryEgg, false, false, false, false, false, 0, 0, detail.Type);

            // Configura el título y la imagen de acuerdo al tipo de comercio
            string tradeTitle;
            string embedImageUrl;

            if (detail.Type == PokeTradeType.Item)
            {
                tradeTitle = embedData.HeldItem; // Nombre del item como título
                string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                embedImageUrl = $"https://serebii.net/itemdex/sprites/sv/{heldItemName}.png";
                ballImgUrl = AbstractTrade<T>.PokeImg(detail.TradeData, false, true);
            }
            else
            {
                // Comportamiento existente para otros tipos de comercio
                tradeTitle = detail.IsMysteryEgg ? "✨ Huevo misterioso Shiny ✨" : detail.Type switch
                {
                    PokeTradeType.Clone => "Solicitud de Clonación",
                    PokeTradeType.Dump => "Solicitud de Dump",
                    PokeTradeType.FixOT => "Solicitud de FixOT",
                    PokeTradeType.Seed => "Solicitud Especial",
                    _ => GameInfo.Strings.Species[detail.TradeData.Species]
                };
                embedImageUrl = detail.IsMysteryEgg ? "https://i.imgur.com/RAj0syZ.png" : detail.Type switch
                {
                    PokeTradeType.Clone => "https://i.imgur.com/aSTCjUn.png",
                    PokeTradeType.Dump => "https://i.imgur.com/9wfEHwZ.png",
                    PokeTradeType.FixOT => "https://i.imgur.com/gRZGFIi.png",
                    PokeTradeType.Seed => "https://i.imgur.com/EI1BHr5.png",
                    _ => detail.TradeData != null ? AbstractTrade<T>.PokeImg(detail.TradeData, false, true) : ""
                };
            }

            var (r, g, b) = await GetDominantColorAsync(embedImageUrl);

            string footerText = detail.Type == PokeTradeType.Clone || detail.Type == PokeTradeType.Dump || detail.Type == PokeTradeType.Seed || detail.Type == PokeTradeType.FixOT
                ? "Iniciando el comercio ahora."
                : $"Iniciando el comercio ahora.\nDisfrute de su {(detail.IsMysteryEgg ? "✨ Huevo Misterioso ✨" : speciesName)}!";

            var embed = new EmbedBuilder()
                .WithColor(new DiscordColor(r, g, b))
                .WithThumbnailUrl(embedImageUrl)
                .WithAuthor($"Siguiente: {user.Username}", user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithDescription($"**Procesando**: {tradeTitle}\n**Trade ID**: {detail.ID}")
                .WithFooter($"{footerText}\u200B", ballImgUrl)
                .WithTimestamp(DateTime.Now)
                .Build();

            await c.SendMessageAsync(embed: embed);
        }

        SysCord<T>.Runner.Hub.Queues.Forwarders.Add(Logger);
        Channels.Add(cid, new TradeStartAction(cid, Logger, c.Name));
    }

    [Command("startInfo")]
    [Summary("Dump la configuración de Notificación de inicio.")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("startClear")]
    [Summary("Borra la configuración de Notificación de inicio en ese canal específico.")]
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
    [Summary("Borra todas las configuraciones de Notificación de inicio.")]
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
