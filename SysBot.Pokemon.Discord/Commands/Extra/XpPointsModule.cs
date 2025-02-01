using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Threading;
using System.Text.RegularExpressions;

namespace SysBot.Pokemon.Discord
{
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private const string StatsFilePath = "user_stats.json";
        private static bool _doubleXPActive = false;
        private static CancellationTokenSource _doubleXPCTS = new();

        [Command("resetpoints")]
        [Alias("rp", "resetp")]
        [Summary("Resets the points of a specific user.")]
        [RequireOwner]
        public async Task ResetPointsAsync(IUser user)
        {
            var stats = LoadOrCreateStats();
            var userId = user.Id.ToString();

            if (stats.ContainsKey(userId))
            {
                stats[userId].Points = 0;
                SaveStats(stats);
                await ReplyAsync($"Los puntos de **{user.Username}** han sido reiniciados a 0.");
            }
            else
            {
                await ReplyAsync($"El usuario **{user.Username}** no tiene estadísticas registradas.");
            }
        }

        [Command("givepoints")]
        [Alias("gp", "addpoints")]
        [Summary("Gives points to a specific user.")]
        [RequireOwner]
        public async Task GivePointsAsync(IUser user, int points)
        {
            if (points < 0)
            {
                await ReplyAsync("No puedes dar puntos negativos.");
                return;
            }

            var stats = LoadOrCreateStats();
            var userId = user.Id.ToString();

            if (!stats.ContainsKey(userId))
            {
                stats[userId] = new UserStats { Wins = 0, Losses = 0, Points = 0, CooldownEnd = DateTime.MinValue };
            }

            stats[userId].Points += points;
            SaveStats(stats);

            await ReplyAsync($"Se han dado **{points}** puntos a **{user.Username}**. Ahora tiene **{stats[userId].Points}** puntos.");
        }

        [Command("subtractpoints")]
        [Alias("sp", "removepoints")]
        [Summary("Subtracts points from a specific user.")]
        [RequireOwner]
        public async Task SubtractPointsAsync(IUser user, int points)
        {
            if (points < 0)
            {
                await ReplyAsync("No puedes restar puntos negativos.");
                return;
            }

            var stats = LoadOrCreateStats();
            var userId = user.Id.ToString();

            if (!stats.ContainsKey(userId))
            {
                await ReplyAsync($"El usuario **{user.Username}** no tiene estadísticas registradas.");
                return;
            }

            if (stats[userId].Points < points)
            {
                stats[userId].Points = 0;
            }
            else
            {
                stats[userId].Points -= points;
            }

            SaveStats(stats);
            await ReplyAsync($"Se han restado **{points}** puntos a **{user.Username}**. Ahora tiene **{stats[userId].Points}** puntos.");
        }

        [Command("rank")]
        [Alias("leaderboard", "top")]
        [Summary("Muestra una lista de los usuarios con más puntos o niveles.")]
        public async Task ShowRankAsync(int page = 1)
        {
            var stats = LoadOrCreateStats();

            if (stats.Count == 0)
            {
                await ReplyAsync("No hay estadísticas disponibles.");
                return;
            }

            // Default to points-based ranking
            var message = await ReplyAsync("Cargando ranking...");
            await ShowRankEmbedAsync(message, page, "points");
        }

        private async Task ShowRankEmbedAsync(IUserMessage message, int page, string rankingType)
        {
            var stats = LoadOrCreateStats();

            var rankedUsers = rankingType == "points"
                ? stats.OrderByDescending(kvp => kvp.Value.Points)
                       .Select((kvp, index) => new { UserId = kvp.Key, Value = kvp.Value.Points, Rank = index + 1 })
                : stats.OrderByDescending(kvp => kvp.Value.Level)
                       .Select((kvp, index) => new { UserId = kvp.Key, Value = kvp.Value.Level, Rank = index + 1 });

            int totalPages = (int)Math.Ceiling(rankedUsers.Count() / 10.0);
            page = Math.Clamp(page, 1, totalPages);

            var usersOnPage = rankedUsers
                .Skip((page - 1) * 10)
                .Take(10)
                .Select(u => $"{u.Rank}. <@{u.UserId}>: **{u.Value}** {(rankingType == "points" ? "puntos" : "nivel")}");

            // Set the title based on the ranking type
            string title = rankingType == "points"
                ? "🏆 Ranking de Usuarios (Puntos) 🏆"
                : "🏆 Ranking de Usuarios (Nivel) 🏆";

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(string.Join("\n", usersOnPage))
                .WithThumbnailUrl("https://i.imgur.com/933Ftu6.gif")
                .WithColor(Color.Gold)
                .WithFooter($"Página {page} de {totalPages}")
                .WithCurrentTimestamp();

            var builder = new ComponentBuilder();

            // Add pagination buttons
            if (totalPages > 1)
            {
                if (page > 1)
                {
                    builder.WithButton("⬅️ Anterior", $"rank_prev_{page - 1}_{rankingType}", ButtonStyle.Secondary);
                }

                if (page < totalPages)
                {
                    builder.WithButton("➡️ Siguiente", $"rank_next_{page + 1}_{rankingType}", ButtonStyle.Secondary);
                }
            }

            // Add ranking type toggle buttons
            builder.WithButton(rankingType == "points" ? "Ver por Nivel" : "Ver por Puntos", $"rank_toggle_{rankingType}", ButtonStyle.Primary);

            // Edit the existing message with the new embed and buttons
            await message.ModifyAsync(msg =>
            {
                msg.Content = ""; // Clear the "Cargando ranking..." message
                msg.Embed = embed.Build();
                msg.Components = builder.Build();
            });

            // Handle button interactions with a 1-minute cooldown
            _ = HandleButtonInteractionAsync(message, page, totalPages, rankingType);
        }

        private async Task HandleButtonInteractionAsync(IUserMessage message, int currentPage, int totalPages, string rankingType)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1), cancellationTokenSource.Token); // 1-minute cooldown

            while (true)
            {
                var interactionTask = WaitForButtonResponseAsync(message, cancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(interactionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout occurred, remove buttons
                    await message.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                    break;
                }

                var interaction = await interactionTask;
                if (interaction != null)
                {
                    // Reset the timeout
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource = new CancellationTokenSource();
                    timeoutTask = Task.Delay(TimeSpan.FromMinutes(1), cancellationTokenSource.Token); // Reset to 1 minute

                    var customId = interaction.Data.CustomId;

                    if (customId.StartsWith("rank_prev_"))
                    {
                        var parts = customId.Split('_');
                        var newPage = int.Parse(parts[2]);
                        rankingType = parts[3];
                        await ShowRankEmbedAsync(message, newPage, rankingType);
                    }
                    else if (customId.StartsWith("rank_next_"))
                    {
                        var parts = customId.Split('_');
                        var newPage = int.Parse(parts[2]);
                        rankingType = parts[3];
                        await ShowRankEmbedAsync(message, newPage, rankingType);
                    }
                    else if (customId.StartsWith("rank_toggle_"))
                    {
                        // Delete the current message
                        await message.DeleteAsync();

                        // Create a new message with the toggled ranking type
                        rankingType = rankingType == "points" ? "level" : "points";
                        var newMessage = await ReplyAsync("Cargando ranking...");
                        await ShowRankEmbedAsync(newMessage, currentPage, rankingType);

                        // Stop handling interactions for the old message
                        return;
                    }

                    // Acknowledge the interaction
                    await interaction.DeferAsync();
                }
            }
        }

        private async Task<SocketMessageComponent?> WaitForButtonResponseAsync(IUserMessage message, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<SocketMessageComponent?>();

            Context.Client.InteractionCreated += OnInteractionCreated;

            try
            {
                using (token.Register(() => tcs.TrySetResult(null)))
                {
                    return await tcs.Task;
                }
            }
            finally
            {
                Context.Client.InteractionCreated -= OnInteractionCreated;
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            async Task OnInteractionCreated(SocketInteraction interaction)
            {
                if (interaction is SocketMessageComponent componentInteraction &&
                    componentInteraction.Message.Id == message.Id)
                {
                    tcs.TrySetResult(componentInteraction);
                }
            }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        [Command("activatedoublexp")]
        [Alias("adxp", "startdoublexp")]
        [Summary("Activa el doble XP por un tiempo limitado. Ejemplo: !activatedoublexp 1d, 30m, 20s")]
        [RequireOwner]
        public async Task ActivateDoubleXPAsync(string duration = "30m")
        {
            if (_doubleXPActive)
            {
                await ReplyAsync("El doble XP ya está activo.");
                return;
            }

            if (!TryParseDuration(duration, out var timeSpan))
            {
                await ReplyAsync("Formato de duración inválido. Usa `1d`, `30m`, o `20s`.");
                return;
            }

            _doubleXPActive = true;
            _doubleXPCTS.Cancel(); // Cancel any existing double XP timer
            _doubleXPCTS = new CancellationTokenSource();

            await ReplyAsync($"¡Doble XP activado! Los usuarios ganarán el doble de XP durante los próximos **{timeSpan:hh\\:mm\\:ss}**.");

            // Deactivate double XP after the specified duration
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(timeSpan, _doubleXPCTS.Token);
                    _doubleXPActive = false;
                    await ReplyAsync("El doble XP ha sido desactivado.");
                }
                catch (TaskCanceledException)
                {
                    // Double XP was manually deactivated or replaced
                }
            });
        }

        [Command("deactivatedoublexp")]
        [Alias("ddxp", "stopdoublexp")]
        [Summary("Desactiva el doble XP manualmente.")]
        [RequireOwner]
        public async Task DeactivateDoubleXPAsync()
        {
            if (!_doubleXPActive)
            {
                await ReplyAsync("El doble XP no está activo.");
                return;
            }

            _doubleXPCTS.Cancel(); // Cancel the double XP timer
            _doubleXPActive = false;
            await ReplyAsync("El doble XP ha sido desactivado manualmente.");
        }

        private bool TryParseDuration(string input, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;

            var regex = new Regex(@"^(?:(?<days>\d+)d)?(?:(?<hours>\d+)h)?(?:(?<minutes>\d+)m)?(?:(?<seconds>\d+)s)?$", RegexOptions.IgnoreCase);
            var match = regex.Match(input);

            if (!match.Success)
                return false;

            int days = match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0;
            int hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
            int minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;
            int seconds = match.Groups["seconds"].Success ? int.Parse(match.Groups["seconds"].Value) : 0;

            timeSpan = new TimeSpan(days, hours, minutes, seconds);
            return true;
        }

        public static bool IsDoubleXPActive()
        {
            return _doubleXPActive;
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
    }
}
