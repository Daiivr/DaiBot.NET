using Discord;
using Discord.Commands;
using ImageSharp = SixLabors.ImageSharp;  // Alias for ImageSharp namespace
using SixLabors.ImageSharp.PixelFormats;
using Newtonsoft.Json;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System;

public class PingModule : ModuleBase<SocketCommandContext>
{
    private const string StatsFilePath = "user_stats.json";
    private static Dictionary<ulong, DateTime> _cooldowns = new Dictionary<ulong, DateTime>();

    [Command("ping")]
    [Summary("Makes the bot respond, indicating that it is running.")]
    public async Task PingAsync()
    {
        var userId1 = Context.User.Id;
        if (_cooldowns.ContainsKey(userId1))
        {
            var cooldownEnd = _cooldowns[userId1];
            if (DateTime.UtcNow < cooldownEnd)
            {
                var unixTime = ((DateTimeOffset)cooldownEnd).ToUnixTimeSeconds();
                await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, debes esperar un poco antes de volver a usar el comando, podras volver a usarlo <t:{unixTime}:R>.");
                return;
            }
        }

        // Actualiza el cooldown
        _cooldowns[userId1] = DateTime.UtcNow.AddMinutes(3);

        // Aquí continúa tu lógica del comando
        var avatarUrl = Context.User.GetAvatarUrl(size: 128) ?? Context.User.GetDefaultAvatarUrl();
        var color = await GetDominantColorAsync(avatarUrl);

        var stats = LoadOrCreateStats();
        var userId = Context.User.Id.ToString();
        if (!stats.ContainsKey(userId))
        {
            stats[userId] = new UserStats { Wins = 0, Losses = 0 };
        }

        var random = new Random();
        bool botWins = random.Next(2) == 0;

        if (botWins)
        {
            stats[userId].Losses++;
        }
        else
        {
            stats[userId].Wins++;
        }

        SaveStats(stats);

        var userStats = stats[userId];
        var winner = botWins ? Context.Client.CurrentUser.Mention : Context.User.Mention;

        var embed = new EmbedBuilder()
            .WithTitle("¡Partida de Ping-Pong!")
            .WithDescription($"Jugué una partida de ping-pong contra {Context.User.Mention} y ganó {winner}!")
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithImageUrl("https://i.imgur.com/w7ApP2T.gif")
            .AddField($"Estadísticas de {Context.User.Username}", $"Ganadas: {userStats.Wins} | Perdidas: {userStats.Losses}")
            .WithFooter(footer =>
            {
                footer.WithText("Partida jugada por " + Context.User.Username);
                footer.WithIconUrl(avatarUrl);
            });

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    private Dictionary<string, UserStats> LoadOrCreateStats()
    {
        if (!File.Exists(StatsFilePath))
        {
            var emptyStats = new Dictionary<string, UserStats>();
            SaveStats(emptyStats);
            return emptyStats;
        }

        var json = File.ReadAllText(StatsFilePath);
        return JsonConvert.DeserializeObject<Dictionary<string, UserStats>>(json) ?? new Dictionary<string, UserStats>();
    }

    private void SaveStats(Dictionary<string, UserStats> stats)
    {
        var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
        File.WriteAllText(StatsFilePath, json);
    }

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
}

public class UserStats
{
    public int Wins { get; set; }
    public int Losses { get; set; }
}
