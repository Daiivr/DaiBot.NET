using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using System;

namespace SysBot.Pokemon.Discord
{
    public class StreamModule : ModuleBase<SocketCommandContext>
    {
        private static readonly string[] StreamMessages = new[]
        {
            "¡Dale un vistazo al stream!", "¡No te lo pierdas!", "¡Transmisión en vivo ahora!", "¡Únete a la diversión!", "¡En vivo ahora mismo!"
        };

        [Command("stream")]
        [Alias("streamlink")]
        [Summary("Devuelve el enlace de transmisión del anfitrión.")]
        public async Task PingAsync()
        {
            var settings = SysCordSettings.Settings;
            var streamIconUrl = DiscordSettings.StreamOptions.StreamIconUrls[settings.Stream.StreamIcon];
            var embedColor = GetEmbedColor(settings.Stream.StreamIcon); // Get the color based on the selected icon option

            var random = new Random();
            var streamMessage = StreamMessages[random.Next(StreamMessages.Length)];

            var embed = new EmbedBuilder()
                .WithTitle($"🎥 {GetStreamPlatformName(settings.Stream.StreamIcon)} Stream 🎥")
                .WithDescription($"{streamMessage} \n\n[¡Haz clic aquí para ver el stream!]({settings.Stream.StreamLink})")
                .WithUrl(settings.Stream.StreamLink) // Optional: Add the URL to the stream link here as well
                .WithThumbnailUrl(streamIconUrl)
                .WithColor(embedColor) // Set the color based on the selected icon option
                .WithFooter(footer =>
                {
                    footer.Text = $"Solicitado por {Context.User.Username}";
                    footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                })
                .WithCurrentTimestamp()
                .Build();

            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }

        private Color GetEmbedColor(StreamIconOption streamIconOption)
        {
            // Map the StreamIconOption to the desired color
            switch (streamIconOption)
            {
                case StreamIconOption.Twitch:
                    return new Color(145, 70, 255); // Twitch Purple
                case StreamIconOption.Youtube:
                    return new Color(255, 0, 0); // YouTube Red
                case StreamIconOption.Facebook:
                    return new Color(24, 119, 242); // Facebook Blue
                case StreamIconOption.Kick:
                    return new Color(0, 255, 0); // Kick Green
                case StreamIconOption.TikTok:
                    return new Color(0, 0, 0); // TikTok Black
                default:
                    return Color.Default;
            }
        }

        private string GetStreamPlatformName(StreamIconOption streamIconOption)
        {
            // Map the StreamIconOption to the platform name
            switch (streamIconOption)
            {
                case StreamIconOption.Twitch:
                    return "Twitch";
                case StreamIconOption.Youtube:
                    return "YouTube";
                case StreamIconOption.Facebook:
                    return "Facebook";
                case StreamIconOption.Kick:
                    return "Kick";
                case StreamIconOption.TikTok:
                    return "TikTok";
                default:
                    return "Stream";
            }
        }
    }
}
