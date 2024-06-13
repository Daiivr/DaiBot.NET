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

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    private const string StatsFilePath = "user_stats.json";

    [Command("ping")]
    [Summary("Hace que el bot responda, indicando que se está ejecutando. O desafía a otro usuario si se menciona.")]
    public async Task PingAsync(IUser? opponent = null)
    {
        var userId = Context.User.Id.ToString();
        var stats = LoadOrCreateStats();

        if (!stats.ContainsKey(userId))
        {
            stats[userId] = new UserStats { Wins = 0, Losses = 0, CooldownEnd = DateTime.MinValue };
        }

        if (DateTime.UtcNow < stats[userId].CooldownEnd)
        {
            var unixTime = ((DateTimeOffset)stats[userId].CooldownEnd).ToUnixTimeSeconds();
            await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, debes esperar un poco antes de volver a usar el comando, podrás volver a usarlo <t:{unixTime}:R>.");
            return;
        }

        // Actualiza el cooldown en las estadísticas solo para el usuario que invocó el comando
        stats[userId].CooldownEnd = DateTime.UtcNow.AddMinutes(3);

        var avatarUrl = Context.User.GetAvatarUrl(size: 128) ?? Context.User.GetDefaultAvatarUrl();
        var color = await GetDominantColorAsync(avatarUrl);

        var userStats = stats[userId];

        var embed = new EmbedBuilder()
            .WithTitle("¡Partida de Ping-Pong!")
            .WithDescription($"Partida de ping-pong entre {Context.User.Mention} y {opponent?.Mention ?? Context.Client.CurrentUser.Mention}")
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithImageUrl("https://i.imgur.com/w7ApP2T.gif")
            .WithFooter(footer =>
            {
                footer.WithText("Partida jugada por " + Context.User.Username);
                footer.WithIconUrl(avatarUrl);
            });

        if (opponent == null)
        {
            // Juego contra el bot
            var random = new Random();
            bool botWins = random.Next(2) == 0;

            if (botWins)
            {
                userStats.Losses++;
                embed.AddField($"Resultado", $"¡**{Context.Client.CurrentUser.Username}** ganó la partida!");
            }
            else
            {
                userStats.Wins++;
                embed.AddField($"Resultado", $"¡**{Context.User.Username}** ganó la partida!");
            }
            // Agregar estadísticas del jugador al embed
            embed.AddField($"Estadísticas de {Context.User.Username}", $"Ganadas: {userStats.Wins} | Perdidas: {userStats.Losses}", true);
        }
        else
        {
            // Juego contra otro usuario
            var opponentId = opponent.Id.ToString();
            if (!stats.ContainsKey(opponentId))
            {
                stats[opponentId] = new UserStats { Wins = 0, Losses = 0, CooldownEnd = DateTime.MinValue };
            }
            var opponentStats = stats[opponentId];

            var random = new Random();
            bool userWins = random.Next(2) == 0;

            if (userWins)
            {
                userStats.Wins++;
                stats[opponentId].Losses++;
                embed.AddField($"Resultado", $"¡**{Context.User.Username}** ganó la partida contra **{opponent.Username}**!");
                embed.AddField($"Estadísticas de {Context.User.Username}", $"Ganadas: {userStats.Wins} | Perdidas: {userStats.Losses}", true);
                embed.AddField($"Estadísticas de {opponent.Username}", $"Ganadas: {opponentStats.Wins} | Perdidas: {opponentStats.Losses}", true);
            }
            else
            {
                userStats.Losses++;
                stats[opponentId].Wins++;
                embed.AddField($"Resultado", $"¡**{opponent.Username}** ganó la partida contra **{Context.User.Username}**!");
                embed.AddField($"Estadísticas de {opponent.Username}", $"Ganadas: {opponentStats.Wins} | Perdidas: {opponentStats.Losses}", true);
                embed.AddField($"Estadísticas de {Context.User.Username}", $"Ganadas: {userStats.Wins} | Perdidas: {userStats.Losses}", true);
            }
        }

        SaveStats(stats);

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
    public DateTime CooldownEnd { get; set; }  // Propiedad añadida para el manejo del cooldown
}
