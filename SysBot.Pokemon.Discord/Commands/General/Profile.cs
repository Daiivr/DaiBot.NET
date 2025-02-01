using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using System.Net.Http;
using ImageSharp = SixLabors.ImageSharp;
using System.Threading;
using System;

namespace SysBot.Pokemon.Discord;

public class ProfileModule : ModuleBase<SocketCommandContext>
{
    private const string StatsFilePath = "user_stats.json";

    // Dictionary to track the last message sent to each user
    private static readonly Dictionary<ulong, IUserMessage> _lastMessages = new();

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
    [Alias("tp")]
    [Summary("Muestra la información del perfil de un usuario, con detalles sensibles visibles solo para el propietario del perfil.")]
    public async Task ProfileAsync(IUser? user = null)
    {
        var targetUser = user ?? Context.User; // Usuario de destino (predeterminado: el invocador del comando)
        var userId = targetUser.Id;
        var avatarUrl = targetUser.GetAvatarUrl(size: 128) ?? targetUser.GetDefaultAvatarUrl();
        var isSelfProfile = targetUser.Id == Context.User.Id; // Comprueba si el usuario está viendo su propio perfil

        var accountCreationTime = targetUser.CreatedAt;
        var discordRelativeTimestamp = $"<t:{accountCreationTime.ToUnixTimeSeconds()}:R>";
        var dominantColor = await GetDominantColorAsync(avatarUrl);

        var tradeCount = GetTradeCountForUser(targetUser.Id);
        var badges = GetBadgesForTradeCount(tradeCount);
        var (wins, losses, points, xp, level) = GetGameStatsForUser(targetUser.Id.ToString()); // Retrieve XP and Level
        var (ot, sid, tid) = GetTrainerInfo(userId);
        var tradeCode = GetTradeCodeForUser(userId);
        var currentStatus = GetCurrentStatus(tradeCount);

        // Calculate XP progress
        int requiredXP = GetRequiredXPForNextLevel(level);
        double xpProgress = (double)xp / requiredXP * 100;

        var embed = new EmbedBuilder()
            .WithTitle($"📝 Perfil de {targetUser.Username}")
            .WithThumbnailUrl(avatarUrl)
            .WithColor(dominantColor)
            .AddField("Cuenta creada:", discordRelativeTimestamp)
            .AddField("Insignias", $"{badges}\n\n**Título Actual:** {currentStatus}")
            .AddField("Tradeos Completados:", tradeCount.ToString())
            .AddField("Nivel", $"{level} (XP: {xp}/{requiredXP})")
            .AddField("Progreso de Nivel", GetProgressBar(xpProgress)); // Add XP progress bar

        if (isSelfProfile)
        {
            embed.AddField("Información de Entrenador  |", $"**OT**: {ot}\n**SID**: {sid}\n**TID**: {tid}", true)
                 .AddField("Código de Intercambio:", tradeCode ?? "Sin codigo aun.", true);
        }

        embed.AddField("Ping Pong", $"**Victorias**: {wins} | **Pérdidas**: {losses}\n**Puntos**: {points}");

        embed.WithFooter(footer =>
        {
            var serverIconUrl = Context.Guild.IconUrl; // Obtener la URL del ícono del servidor
            if (isSelfProfile)
            {
                footer.WithText("Servidor: " + Context.Guild.Name)
                      .WithIconUrl(serverIconUrl); // Añadir la foto del servidor
            }
            else
            {
                var botPrefix = SysCordSettings.HubConfig.Discord.CommandPrefix;
                footer.WithText($"Usa el menú de abajo para ver más detalles de las insignias.\nServidor: {Context.Guild.Name}")
                      .WithIconUrl(serverIconUrl); // Añadir la foto del servidor
            }
        })
        .WithCurrentTimestamp();

        // Add a select menu to view badges
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId("view_badges")
            .WithPlaceholder("📜 Selecciona una opción")
            .AddOption("Ver Insignias", "view_badges", "Muestra más detalles sobre las insignias del usuario.", new Emoji("🎖️"));

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu);

        if (isSelfProfile)
        {
            try
            {
                // Send the profile embed with the select menu to the user's DM
                var dmChannel = await targetUser.CreateDMChannelAsync();
                var message = await dmChannel.SendMessageAsync(embed: embed.Build(), components: component.Build());

                // Send confirmation message and delete the user's command
                var confirmationMessage = await ReplyAsync($"📩 {targetUser.Mention} Tu perfil ha sido enviado por mensaje directo.").ConfigureAwait(false);
                _ = Task.Delay(10000).ContinueWith(_ => confirmationMessage.DeleteAsync());

                await Context.Message.DeleteAsync();

                // Handle select menu interactions with a 1-minute timeout
                await HandleSelectMenuInteractions(message, Context.User.Id, TimeSpan.FromMinutes(1), targetUser.Id);
            }
            catch
            {
                var errorMessage = await ReplyAsync($"❌ {targetUser.Mention} No se pudo enviar tu perfil por mensaje directo. Por favor, habilita los mensajes directos.").ConfigureAwait(false);
                _ = Task.Delay(10000).ContinueWith(_ => errorMessage.DeleteAsync());

                await Context.Message.DeleteAsync();
            }
        }
        else
        {
            // Send the profile embed with the select menu to the channel
            var message = await ReplyAsync(embed: embed.Build(), components: component.Build());

            // Handle select menu interactions with a 1-minute timeout
            await HandleSelectMenuInteractions(message, Context.User.Id, TimeSpan.FromMinutes(1), targetUser.Id);
        }
    }

    private string GetProgressBar(double percentage)
    {
        const int totalBlocks = 10;
        int filledBlocks = (int)(percentage / 10);
        string progressBar = new string('█', filledBlocks) + new string('░', totalBlocks - filledBlocks);
        return $"[{progressBar}] {percentage:0.00}%";
    }

    private int GetRequiredXPForNextLevel(int currentLevel)
    {
        // Base XP required for level 1 is 100, and it increases by 20% each level
        return (int)(100 * Math.Pow(1.2, currentLevel - 1));
    }

    // Method to retrieve Ping Pong stats (Wins, Losses, Points, XP, Level)
    private (int Wins, int Losses, int Points, int XP, int Level) GetGameStatsForUser(string userId)
    {
        if (!File.Exists(StatsFilePath))
            return (0, 0, 0, 0, 1); // Default level is 1

        var json = File.ReadAllText(StatsFilePath);
        var stats = JsonConvert.DeserializeObject<Dictionary<string, UserStats>>(json);

        if (stats != null && stats.TryGetValue(userId, out var userStats))
        {
            return (userStats.Wins, userStats.Losses, userStats.Points, userStats.XP, userStats.Level);
        }

        return (0, 0, 0, 0, 1); // Default level is 1
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

    private async Task HandleSelectMenuInteractions(IUserMessage message, ulong userId, TimeSpan timeout, ulong targetUserId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var timeoutTask = Task.Delay(timeout, cancellationTokenSource.Token);

        while (true)
        {
            var interactionTask = WaitForSelectMenuResponseAsync(message, userId, timeout);
            var completedTask = await Task.WhenAny(interactionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred, remove the menu
                await message.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                _lastMessages.Remove(userId);
                break;
            }

            var interaction = await interactionTask;
            if (interaction != null)
            {
                // Reset the timeout
                cancellationTokenSource.Cancel();
                cancellationTokenSource = new CancellationTokenSource();
                timeoutTask = Task.Delay(timeout, cancellationTokenSource.Token);

                // Acknowledge the interaction immediately
                await interaction.DeferAsync();

                if (interaction.Data.CustomId == "view_badges")
                {
                    var selectedOption = interaction.Data.Values.FirstOrDefault();

                    if (selectedOption == "view_badges")
                    {
                        // Fetch the trade count and badge information for the TARGET USER
                        var tradeCount = GetTradeCountForUser(targetUserId);
                        var badgeList = SysCordSettings.Settings.CustomBadgeEmojis;
                        var nextBadge = badgeList.FirstOrDefault(b => b.TradeCount > tradeCount);

                        string nextBadgeInfo = nextBadge != null
                            ? $"**Trades restantes para desbloquear la próxima insignia:** {nextBadge.TradeCount - tradeCount}\n({nextBadge.Emoji} {nextBadge.TradeCount} trades)"
                            : "¡Has desbloqueado todas las insignias disponibles!";

                        string nextTitle = nextBadge != null ? GetCurrentStatus(nextBadge.TradeCount) : "¡Máximo Título Alcanzado!";

                        var badgesEmbed = new EmbedBuilder()
                            .WithTitle($"🎖️ Insignias de {Context.Guild.GetUser(targetUserId).Username}")
                            .WithDescription(GetEarnedBadgesWithDescriptions(tradeCount))
                            .WithColor(Color.Gold)
                            .WithThumbnailUrl(Context.Guild.GetUser(targetUserId).GetAvatarUrl() ?? Context.Guild.GetUser(targetUserId).GetDefaultAvatarUrl())
                            .AddField("\u200B", "\u200B") // Separador como campo vacío
                            .AddField("Próxima Insignia", nextBadgeInfo, true) // Campo para los trades restantes y título
                            .AddField("Próximo Título", nextTitle, true) // Campo para el próximo título
                            .WithFooter(footer => footer.WithText($"Total de trades: {tradeCount}\n¡Sigue intercambiando para desbloquear más insignias!")
                                                        .WithIconUrl(Context.Guild.IconUrl))
                            .WithCurrentTimestamp();

                        // Add a select menu to go back to the profile
                        var selectMenu = new SelectMenuBuilder()
                            .WithCustomId("view_badges")
                            .WithPlaceholder("Selecciona una opción")
                            .AddOption("Volver al Perfil", "back_to_profile", "Regresa a la información del perfil", new Emoji("🪪"));

                        var component = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);

                        // Update the original message with the new embed and select menu
                        await message.ModifyAsync(msg =>
                        {
                            msg.Embed = badgesEmbed.Build();
                            msg.Components = component.Build();
                        });

                        // Track the last message
                        _lastMessages[userId] = message;
                    }
                    else if (selectedOption == "back_to_profile")
                    {
                        // Fetch the original profile information
                        var targetUser = Context.Guild.GetUser(targetUserId);
                        var avatarUrl = targetUser.GetAvatarUrl(size: 128) ?? targetUser.GetDefaultAvatarUrl();
                        var accountCreationTime = targetUser.CreatedAt;
                        var discordRelativeTimestamp = $"<t:{accountCreationTime.ToUnixTimeSeconds()}:R>";
                        var dominantColor = await GetDominantColorAsync(avatarUrl);

                        var tradeCount = GetTradeCountForUser(targetUserId);
                        var badges = GetBadgesForTradeCount(tradeCount);
                        var (wins, losses, points, xp, level) = GetGameStatsForUser(targetUserId.ToString());
                        var (ot, sid, tid) = GetTrainerInfo(targetUserId);
                        var tradeCode = GetTradeCodeForUser(targetUserId);
                        var currentStatus = GetCurrentStatus(tradeCount);

                        // Calculate XP progress
                        int requiredXP = GetRequiredXPForNextLevel(level);
                        double xpProgress = (double)xp / requiredXP * 100;

                        var profileEmbed = new EmbedBuilder()
                            .WithTitle($"📝 Perfil de {targetUser.Username}")
                            .WithThumbnailUrl(avatarUrl)
                            .WithColor(dominantColor)
                            .AddField("Cuenta creada:", discordRelativeTimestamp)
                            .AddField("Insignias", $"{badges}\n\n**Título Actual:** {currentStatus}")
                            .AddField("Tradeos Completados:", tradeCount.ToString())
                            .AddField("Nivel", $"{level} (XP: {xp}/{requiredXP})")
                            .AddField("Progreso de Nivel", GetProgressBar(xpProgress));

                        if (targetUser.Id == Context.User.Id)
                        {
                            profileEmbed.AddField("Información de Entrenador  |", $"**OT**: {ot}\n**SID**: {sid}\n**TID**: {tid}", true)
                                 .AddField("Código de Intercambio:", tradeCode ?? "Sin codigo aun.", true);
                        }

                        profileEmbed.AddField("Ping Pong", $"**Victorias**: {wins} | **Pérdidas**: {losses}\n**Puntos**: {points}");

                        profileEmbed.WithFooter(footer =>
                        {
                            var serverIconUrl = Context.Guild.IconUrl; // Obtener la URL del ícono del servidor
                            if (targetUser.Id == Context.User.Id)
                            {
                                footer.WithText("Servidor: " + Context.Guild.Name)
                                      .WithIconUrl(serverIconUrl); // Añadir la foto del servidor
                            }
                            else
                            {
                                var botPrefix = SysCordSettings.HubConfig.Discord.CommandPrefix;
                                footer.WithText($"Usa el menú de abajo para ver más detalles de las insignias.\nServidor: {Context.Guild.Name}")
                                      .WithIconUrl(serverIconUrl); // Añadir la foto del servidor
                            }
                        })
                        .WithCurrentTimestamp();

                        // Add a select menu to view badges
                        var selectMenu = new SelectMenuBuilder()
                            .WithCustomId("view_badges")
                            .WithPlaceholder("Selecciona una opción")
                            .AddOption("Ver Insignias", "view_badges", "Muestra más detalles sobre las insignias del usuario.", new Emoji("🎖️"));

                        var component = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);

                        // Update the original message with the profile embed and select menu
                        await message.ModifyAsync(msg =>
                        {
                            msg.Embed = profileEmbed.Build();
                            msg.Components = component.Build();
                        });

                        // Track the last message
                        _lastMessages[userId] = message;
                    }
                }
            }
        }
    }

    private async Task<SocketMessageComponent?> WaitForSelectMenuResponseAsync(IUserMessage message, ulong userId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<SocketMessageComponent?>();
        var cancellationTokenSource = new CancellationTokenSource(timeout);

        Context.Client.InteractionCreated += OnInteractionCreated;

        try
        {
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async Task OnInteractionCreated(SocketInteraction interaction)
        {
            if (interaction is SocketMessageComponent componentInteraction &&
                componentInteraction.User.Id == userId &&
                componentInteraction.Message.Id == message.Id)
            {
                tcs.TrySetResult(componentInteraction);
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    private string GetEarnedBadgesWithDescriptions(int tradeCount)
    {
        var badgeList = SysCordSettings.Settings.CustomBadgeEmojis;

        // Filter the badges that the user has earned
        var earnedBadges = badgeList
            .Where(b => tradeCount >= b.TradeCount)
            .Select(b => b.TradeCount == 1
                ? $"# {b.Emoji} {b.TradeCount} intercambio"  // Singular for 1 trade
                : $"# {b.Emoji} {b.TradeCount} intercambios") // Plural for more than 1 trade
            .ToList();

        return earnedBadges.Any() ? string.Join("\n", earnedBadges) : "Aún no hay insignias.";
    }

    private int GetTradeCountForUser(ulong userId)
    {
        var tradeStorage = new TradeCodeStorage();
        return tradeStorage.GetTradeCount(userId);
    }
}
