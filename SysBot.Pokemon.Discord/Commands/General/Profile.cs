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
        var userId = targetUser.Id;
        var avatarUrl = targetUser.GetAvatarUrl(size: 128) ?? targetUser.GetDefaultAvatarUrl();

        // Get the relative creation time using a Discord timestamp
        var accountCreationTime = targetUser.CreatedAt;
        var discordRelativeTimestamp = $"<t:{accountCreationTime.ToUnixTimeSeconds()}:R>";

        // Get the dominant color from the user's profile picture
        var dominantColor = await GetDominantColorAsync(avatarUrl);

        var tradeCount = GetTradeCountForUser(targetUser.Id);
        var badges = GetBadgesForTradeCount(tradeCount);
        var (wins, losses) = GetGameStatsForUser(targetUser.Id.ToString());
        var (ot, sid, tid) = GetTrainerInfo(userId);

        // Get the trade code for the user
        var tradeCode = GetTradeCodeForUser(userId);

        // Calculate current status based on trade milestones
        var currentStatus = GetCurrentStatus(tradeCount);

        var embed = new EmbedBuilder()
            .WithTitle($"📝 Perfil de {targetUser.Username}")
            .WithThumbnailUrl(avatarUrl)
            .WithColor(dominantColor)  // Usar el color dominante
            .AddField("Cuenta creada:", discordRelativeTimestamp)
            .AddField("Insignias", $"{badges}\n\n**Estado Actual:** {currentStatus}")
            .AddField("Tradeos Completados:", tradeCount.ToString())
            .AddField("Información de Entrenador", $"**OT**: {ot}\n**SID**: {sid}\n**TID**: {tid}")
            .AddField("Código de Intercambio:", tradeCode ?? "Sin codigo aun.")
            .AddField("Ping Pong", $"Victorias: {wins} | Pérdidas: {losses}")
            .WithFooter(footer =>
            {
                footer.WithText("Servidor: " + Context.Guild.Name);
                footer.WithIconUrl(Context.Guild.IconUrl);  // Usar el icono del servidor como imagen del pie de página
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

    private (string OT, string SID, string TID) GetTrainerInfo(ulong userId)
    {
        var tradeStorage = new TradeCodeStorage();
        var tradeDetails = tradeStorage.GetTradeDetails(userId);

        if (tradeDetails != null)
            return (tradeDetails.OT ?? "N/A", tradeDetails.SID.ToString(), tradeDetails.TID.ToString());

        return ("N/A", "N/A", "N/A");
    }

    // Helper method to get the current status
    private string GetCurrentStatus(int totalTrades)
    {
        return totalTrades switch
        {
            >= 700 => "Dios Pokémon",
            >= 650 => "Maestro Pokémon",
            >= 600 => "Famoso Mundial",
            >= 550 => "Maestro de Intercambios",
            >= 500 => "Maestro Regional",
            >= 450 => "Leyenda Pokémon",
            >= 400 => "Sabio Pokémon",
            >= 350 => "Comerciante Pokémon",
            >= 300 => "Élite Pokémon",
            >= 250 => "Héroe Pokémon",
            >= 200 => "Campeón Pokémon",
            >= 150 => "Especialista Pokémon",
            >= 100 => "Profesor Pokémon",
            >= 50 => "Entrenador Novato",
            >= 1 => "Entrenador Principiante",
            _ => "Entrenador Nuevo"
        };
    }

    // Helper method to get the trade code for the user
    private string? GetTradeCodeForUser(ulong userId)
    {
        var tradeStorage = new TradeCodeStorage();
        var tradeDetails = tradeStorage.GetTradeDetails(userId);

        if (tradeDetails?.Code != null)
        {
            // Insertar un espacio después de los primeros 4 dígitos y envolver en spoiler
            var formattedCode = $"{tradeDetails.Code.Substring(0, 4)} {tradeDetails.Code.Substring(4)}";
            return $"||{formattedCode}||"; // Agregar barras verticales dobles para el spoiler
        }

        return null; // Si no hay código, devolver null
    }
}
