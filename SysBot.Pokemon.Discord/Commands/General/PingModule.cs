using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ImageSharp = SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Newtonsoft.Json;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Threading;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    private const string StatsFilePath = "user_stats.json";
    private static readonly Dictionary<ulong, ChallengeRequest> ActiveChallenges = new();

    [Command("ping")]
    [Summary("Hace que el bot responda, indicando que se está ejecutando. O desafía a otro usuario si se menciona, apostando puntos opcionales.")]
    public async Task PingAsync(IUser? opponent = null, int betPoints = 0)
    {
        var userId = Context.User.Id.ToString();
        var stats = LoadOrCreateStats();

        if (!stats.ContainsKey(userId))
        {
            stats[userId] = new UserStats { Wins = 0, Losses = 0, Points = 0, CooldownEnd = DateTime.MinValue };
        }

        var userStats = stats[userId];

        // Check if the user already has an active challenge
        if (ActiveChallenges.ContainsKey(Context.User.Id))
        {
            // Delete the user's command message
            await Context.Message.DeleteAsync().ConfigureAwait(false);

            // Send a message indicating they must wait
            var waitMessage = await ReplyAsync($"<a:no:1206485104424128593> {Context.User.Mention}, ya tienes un desafío pendiente. Espera a que el oponente responda antes de iniciar otro.");

            // Delete the bot's response message after 30 seconds
            _ = DeleteMessageAfterDelayAsync(waitMessage, TimeSpan.FromSeconds(30));

            return;
        }

        // Skip cooldown check if the user is trying to play against themselves
        if (opponent?.Id != Context.User.Id)
        {
            // Check if the user is on cooldown
            if (DateTime.UtcNow < userStats.CooldownEnd)
            {
                var unixTime = ((DateTimeOffset)userStats.CooldownEnd).ToUnixTimeSeconds();

                // Delete the user's command message
                await Context.Message.DeleteAsync().ConfigureAwait(false);

                // Send a message indicating they are on cooldown
                var cooldownMessage = await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, debes esperar un poco antes de volver a usar el comando, podrás volver a usarlo <t:{unixTime}:R>.");

                // Delete the bot's response message after 30 seconds
                _ = DeleteMessageAfterDelayAsync(cooldownMessage, TimeSpan.FromSeconds(30));

                return;
            }
        }

        var avatarUrl = Context.User.GetAvatarUrl(size: 128) ?? Context.User.GetDefaultAvatarUrl();
        var color = await GetDominantColorAsync(avatarUrl);
        var serverIconUrl = Context.Guild.IconUrl;

        // Lista de GIFs
        var gifs = new List<string>
        {
            "https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/anime.gif?raw=true",
            "https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/dandidave-pingpong.gif?raw=true",
            "https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/nxnOJPf.gif?raw=true",
            "https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/tumblr_p591nlIE3R1tpvtc4o1_r1_540.gif?raw=true",
            "https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/tumblr_p591nlIE3R1tpvtc4o4_r1_540.gif?raw=true"
        };

        // Selección aleatoria de GIF
        var randomgif = new Random();
        string selectedGif = gifs[randomgif.Next(gifs.Count)];

        // Verificar si el usuario intenta jugar contra sí mismo
        if (opponent?.Id == Context.User.Id)
        {
            var embed = new EmbedBuilder()
                .WithTitle("¿Jugar contra ti mismo? 🤔")
                .WithDescription($"<a:no:1206485104424128593> {Context.User.Mention}, eso es un nivel de soledad que ni yo puedo manejar. Encuentra a alguien más para jugar. 🏓")
                .WithImageUrl("https://github.com/Daiivr/SysBot-Images/blob/main/Ping%20Pong/IFMCv6Y.gif?raw=true")
                .WithThumbnailUrl(avatarUrl)
                .WithColor(color)
                .WithFooter(footer =>
                {
                    footer.WithText("Un consejo de tu bot favorito");
                    footer.WithIconUrl(serverIconUrl);
                });

            await ReplyAsync(embed: embed.Build());
            return;
        }

        if (opponent == null || opponent.IsBot)
        {
            // Juego contra el bot (sin apuestas ni impacto en puntos)
            var random = new Random();
            bool botWins = random.Next(2) == 0;

            var embed = new EmbedBuilder()
                .WithTitle("¡Partida de Ping-Pong!")
                .WithDescription($"Partida de ping-pong entre {Context.User.Mention} y el bot {Context.Client.CurrentUser.Mention}")
                .WithImageUrl(selectedGif)
                .WithColor(color)
                .WithCurrentTimestamp()
                .WithFooter(footer =>
                {
                    footer.WithText("Partida jugada por " + Context.User.Username);
                    footer.WithIconUrl(avatarUrl);
                });

            if (botWins)
            {
                embed.AddField($"Resultado", $"¡**{Context.Client.CurrentUser.Username}** ganó la partida!");
            }
            else
            {
                embed.AddField($"Resultado", $"¡**{Context.User.Username}** ganó la partida!");
            }

            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
            return;
        }

        var opponentId = opponent.Id.ToString();
        if (!stats.ContainsKey(opponentId))
        {
            stats[opponentId] = new UserStats { Wins = 0, Losses = 0, Points = 0, CooldownEnd = DateTime.MinValue };
        }

        var opponentStats = stats[opponentId];

        // Validar puntos disponibles para la apuesta
        if (betPoints > 0)
        {
            if (userStats.Points < betPoints)
            {
                await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, no tienes suficientes puntos para apostar {betPoints} puntos. Tus puntos actuales: {userStats.Points}");
                return;
            }

            if (opponentStats.Points < betPoints)
            {
                await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, {opponent.Username} no tiene suficientes puntos para igualar tu apuesta de {betPoints} puntos. Sus puntos actuales: {opponentStats.Points}");
                return;
            }
        }

        // Crear una solicitud de desafío
        var challenge = new ChallengeRequest
        {
            ChallengerId = Context.User.Id,
            OpponentId = opponent.Id,
            BetPoints = betPoints,
            ChallengeTime = DateTime.UtcNow
        };

        // Add the challenge to the ActiveChallenges dictionary
        ActiveChallenges[Context.User.Id] = challenge;

        var betText = betPoints > 0
            ? $"**+ {betPoints} puntos** si ganas. | **- {betPoints} puntos** si pierdes."
            : "No se han apostado puntos.";

        var challengeEmbed = new EmbedBuilder()
            .WithTitle("¡Desafío de Ping-Pong!")
            .WithDescription($"{Context.User.Mention} ha desafiado a {opponent.Mention} a un partido de ping-pong.")
            .AddField("Apuesta:", betText)
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithFooter(footer =>
            {
                footer.WithText("Tienes 30 segundos para responder.");
                footer.WithIconUrl(serverIconUrl);
            });

        // Crear botones de aceptar y rechazar
        var buttonYes = new ButtonBuilder()
            .WithLabel("✅ Sí")
            .WithStyle(ButtonStyle.Success)
            .WithCustomId("accept_challenge");

        var buttonNo = new ButtonBuilder()
            .WithLabel("❎ No")
            .WithStyle(ButtonStyle.Danger)
            .WithCustomId("decline_challenge");

        var buttonComponent = new ComponentBuilder()
            .WithButton(buttonYes)
            .WithButton(buttonNo);

        var challengeMessage = await ReplyAsync(embed: challengeEmbed.Build(), components: buttonComponent.Build());

        // Esperar la respuesta del oponente
        var response = await WaitForButtonResponseAsync(challengeMessage, opponent.Id, TimeSpan.FromSeconds(30));

        if (response == null || response.Data.CustomId != "accept_challenge")
        {
            await ReplyAsync($"{opponent.Mention} ha rechazado el desafío o no respondió a tiempo.");
            ActiveChallenges.Remove(Context.User.Id); // Remove the challenge since it was not accepted
            return; // Skip cooldown application
        }

        ActiveChallenges.Remove(Context.User.Id); // Remove the challenge since it was accepted

        // Juego con apuesta
        var randomMatch = new Random();
        bool userWinsMatch = randomMatch.Next(2) == 0;

        var embedDescription = $"Partida de ping-pong entre {Context.User.Mention} y {opponent.Mention}";
        if (betPoints > 0)
        {
            embedDescription += $". Apuesta: {betPoints} puntos";
        }

        var embedMatch = new EmbedBuilder()
            .WithTitle("¡Partida de Ping-Pong!")
            .WithDescription(embedDescription)
            .WithImageUrl(selectedGif)
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithFooter(footer =>
            {
                footer.WithText("Partida jugada por " + Context.User.Username);
                footer.WithIconUrl(avatarUrl);
            });

        if (userWinsMatch)
        {
            userStats.Wins++;
            userStats.Points += betPoints + 10; // Suma puntos por ganar y la apuesta
            opponentStats.Losses++;

            // Only subtract points if the opponent has enough points
            int pointsLost = betPoints > 0 ? betPoints : 5;
            if (opponentStats.Points >= pointsLost)
            {
                opponentStats.Points -= pointsLost;
                embedMatch.AddField($"Resultado", $"**{Context.User.Username}** ganó la partida contra **{opponent.Username}** y ganó **{betPoints + 10}** puntos!\n**{opponent.Username}** perdió **{pointsLost}** puntos!");
            }
            else
            {
                opponentStats.Points = 0; // Set to 0 if they don't have enough points
                embedMatch.AddField($"Resultado", $"**{Context.User.Username}** ganó la partida contra **{opponent.Username}** y ganó **{betPoints + 10}** puntos!\n**{opponent.Username}** perdió **0** puntos!");
            }

            // Grant XP reward if the bet is 100 points or more
            if (betPoints >= 100)
            {
                int xpReward = (int)(betPoints * 0.05); // 5% of the bet as XP
                userStats.XP += xpReward;
                embedMatch.AddField("Recompensa de XP", $"¡**{Context.User.Username}** ganó **{xpReward} XP** como recompensa por la apuesta de **{betPoints}** puntos!");
            }
        }
        else
        {
            userStats.Losses++;
            opponentStats.Wins++;
            opponentStats.Points += betPoints + 10; // Suma puntos al ganador

            // Only subtract points if the user has enough points
            int pointsLost = betPoints > 0 ? betPoints : 5;
            if (userStats.Points >= pointsLost)
            {
                userStats.Points -= pointsLost;
                embedMatch.AddField($"Resultado", $"**{Context.User.Username}** perdió la partida contra **{opponent.Username}** y perdió **{pointsLost}** puntos!\n**{opponent.Username}** ganó **{betPoints + 10}** puntos!");
            }
            else
            {
                userStats.Points = 0; // Set to 0 if they don't have enough points
                embedMatch.AddField($"Resultado", $"**{Context.User.Username}** perdió la partida contra **{opponent.Username}** y perdió **0** puntos!\n**{opponent.Username}** ganó **{betPoints + 10}** puntos!");
            }

            // Grant XP reward if the bet is 100 points or more
            if (betPoints >= 100)
            {
                int xpReward = (int)(betPoints * 0.05); // 5% of the bet as XP
                opponentStats.XP += xpReward;
                embedMatch.AddField("Recompensa de XP", $"¡**{opponent.Username}** ganó **{xpReward} XP** como recompensa por la apuesta de **{betPoints}** puntos!");
            }
        }

        embedMatch.AddField($"Estadísticas de {Context.User.Username}",
            $"Ganadas: {userStats.Wins} | Perdidas: {userStats.Losses}\nPuntos: {userStats.Points}", true);
        embedMatch.AddField($"Estadísticas de {opponent.Username}",
            $"Ganadas: {opponentStats.Wins} | Perdidas: {opponentStats.Losses}\nPuntos: {opponentStats.Points}", true);

        SaveStats(stats);

        await ReplyAsync(embed: embedMatch.Build()).ConfigureAwait(false);

        // Apply cooldown only if the match was played
        if (opponent?.Id != Context.User.Id)
        {
            userStats.CooldownEnd = DateTime.UtcNow.AddMinutes(3);
            SaveStats(stats);
        }
    }

    private async Task<SocketMessageComponent?> WaitForButtonResponseAsync(IUserMessage message, ulong userId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<SocketMessageComponent?>();
        var cancellationTokenSource = new CancellationTokenSource(timeout);

        Context.Client.InteractionCreated += OnInteractionCreated;

        try
        {
            // Set a timeout task that will complete after the specified timeout
            var timeoutTask = Task.Delay(timeout, cancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred, no interaction was provided
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("¡Desafío de Ping-Pong!")
                    .WithDescription($"{Context.User.Mention} ha desafiado a <@{userId}> a un partido de ping-pong.\n\n" +
                                    $"<a:no:1206485104424128593> **<@{userId}> no respondió a tiempo.**")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp();

                // Update the embed to show that the user did not respond in time
                await message.ModifyAsync(msg =>
                {
                    msg.Embed = embedBuilder.Build();
                    msg.Components = new ComponentBuilder().Build(); // Remove the buttons
                });

                return null;
            }

            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            Context.Client.InteractionCreated -= OnInteractionCreated;
            cancellationTokenSource.Dispose();
        }

        async Task OnInteractionCreated(SocketInteraction interaction)
        {
            if (interaction is SocketMessageComponent componentInteraction &&
                componentInteraction.User.Id == userId &&
                componentInteraction.Message.Id == message.Id)
            {
                // Acknowledge the interaction to prevent the "This interaction failed" message
                await componentInteraction.DeferAsync();

                // Update the embed based on the user's response
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("¡Desafío de Ping-Pong!")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                if (componentInteraction.Data.CustomId == "accept_challenge")
                {
                    embedBuilder.WithDescription($"{Context.User.Mention} ha desafiado a {componentInteraction.User.Mention} a un partido de ping-pong.\n\n" +
                                                $"<a:yes:1206485105674166292> **{componentInteraction.User.Username} ha aceptado el desafío.**\n" +
                                                "El partido comenzará en **5 segundos**...");

                    // Update the embed with the acceptance message
                    await message.ModifyAsync(msg =>
                    {
                        msg.Embed = embedBuilder.Build();
                        msg.Components = new ComponentBuilder().Build(); // Remove the buttons
                    });

                    // Wait for 5 seconds before starting the match
                    await Task.Delay(5000);

                    // Set the result for the task
                    tcs.TrySetResult(componentInteraction);
                }
                else if (componentInteraction.Data.CustomId == "decline_challenge")
                {
                    embedBuilder.WithDescription($"{Context.User.Mention} ha desafiado a {componentInteraction.User.Mention} a un partido de ping-pong.\n\n" +
                                                $"<a:no:1206485104424128593> **{componentInteraction.User.Username} ha rechazado el desafío.**");

                    // Update the embed with the decline message
                    await message.ModifyAsync(msg =>
                    {
                        msg.Embed = embedBuilder.Build();
                        msg.Components = new ComponentBuilder().Build(); // Remove the buttons
                    });

                    // Set the result as null to indicate the challenge was declined
                    tcs.TrySetResult(null);
                }
            }
        }
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


    // Helper method to delete a message after a delay
    private async Task DeleteMessageAfterDelayAsync(IUserMessage message, TimeSpan delay)
    {
        await Task.Delay(delay).ConfigureAwait(false);
        await message.DeleteAsync().ConfigureAwait(false);
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
    public int Points { get; set; }
    public int XP { get; set; } // New property for XP
    public int Level { get; set; } // New property for Level
    public DateTime LastXPGain { get; set; } // New property to track the last time XP was gained
    public DateTime CooldownEnd { get; set; } // Cooldown end time
}

public class ChallengeRequest
{
    public ulong ChallengerId { get; set; }
    public ulong OpponentId { get; set; }
    public int BetPoints { get; set; }
    public DateTime ChallengeTime { get; set; }
}
