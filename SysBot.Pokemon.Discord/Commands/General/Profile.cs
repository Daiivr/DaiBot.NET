using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using System.Net.Http;
using ImageSharp = SixLabors.ImageSharp;

namespace SysBot.Pokemon.Discord;

public class ProfileModule : ModuleBase<SocketCommandContext>
{
    private const string StatsFilePath = "user_stats.json";

    private async Task<Color> GetDominantColorAsync(string imageUrl)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(imageUrl);
        using var stream = await response.Content.ReadAsStreamAsync();

        using var image = ImageSharp.Image.Load<Rgba32>(stream);
        var histogram = new Dictionary<Rgba32, int>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                if (histogram.ContainsKey(pixel))
                    histogram[pixel]++;
                else
                    histogram[pixel] = 1;
            }
        }

        var dominant = histogram.OrderByDescending(kvp => kvp.Value).First().Key;
        return new Color(dominant.R, dominant.G, dominant.B);
    }

    [Command("profile")]
    [Summary("Displays the user's profile information including trades and game stats.")]
    public async Task ProfileAsync(IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var avatarUrl = targetUser.GetAvatarUrl(size: 128) ?? targetUser.GetDefaultAvatarUrl();

        // Get the relative creation time using a Discord timestamp
        var accountCreationTime = targetUser.CreatedAt;
        var discordRelativeTimestamp = $"<t:{accountCreationTime.ToUnixTimeSeconds()}:R>";

        // Get the dominant color from the user's profile picture
        var dominantColor = await GetDominantColorAsync(avatarUrl);

        var tradeCount = GetTradeCountForUser(targetUser.Id);
        var badges = GetBadgesForTradeCount(tradeCount);
        var (wins, losses) = GetGameStatsForUser(targetUser.Id.ToString());

        // Get the server icon URL
        var serverIconUrl = Context.Guild.IconUrl;

        var embed = new EmbedBuilder()
            .WithTitle($"Perfil de {targetUser.Username}")
            .WithThumbnailUrl(avatarUrl)
            .WithColor(dominantColor)  // Use the dominant color
            .AddField("Cuenta creada:", discordRelativeTimestamp)
            .AddField("Insignias", badges)
            .AddField("Tradeos Completados:", tradeCount.ToString())
            .AddField("Ping Pong", $"Victorias: {wins} | Pérdidas: {losses}")
            .WithFooter(footer =>
            {
                footer.WithText("Servidor: " + Context.Guild.Name);
                footer.WithIconUrl(serverIconUrl);  // Set the server icon as the footer image
            })
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    private int GetTradeCountForUser(ulong userId)
    {
        var tradeStorage = new TradeCodeStorage();
        return tradeStorage.GetTradeCount(userId);
    }

    private (int Wins, int Losses) GetGameStatsForUser(string userId)
    {
        if (!File.Exists(StatsFilePath))
            return (0, 0);

        var json = File.ReadAllText(StatsFilePath);
        var stats = JsonConvert.DeserializeObject<Dictionary<string, UserStats>>(json);

        if (stats != null && stats.TryGetValue(userId, out var userStats))
        {
            return (userStats.Wins, userStats.Losses);
        }

        return (0, 0);
    }

    private string GetBadgesForTradeCount(int tradeCount)
    {
        var badges = string.Empty;

        foreach (var badge in SysCordSettings.Settings.CustomBadgeEmojis)
        {
            if (tradeCount >= badge.TradeCount)
            {
                badges += badge.Emoji + " ";
            }
        }

        return string.IsNullOrEmpty(badges) ? "Aún no hay insignias." : badges;
    }
}
