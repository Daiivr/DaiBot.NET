using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class StreamModule : ModuleBase<SocketCommandContext>
    {
        [Command("stream")]
        [Alias("streamlink")]
        [Summary("Devuelve el enlace de transmisión del anfitrión.")]
        public async Task PingAsync()
        {
            var settings = SysCordSettings.Settings;
            var streamIconUrl = DiscordSettings.StreamIconUrls[settings.StreamIcon];
            var embedColor = GetEmbedColor(settings.StreamIcon); // Get the color based on the selected icon option

            var embed = new EmbedBuilder()
                .WithTitle("¡Enlace del Stream!")
                .WithDescription($"Aquí está el enlace del Stream, ¡disfrutar! :3 \n{settings.StreamLink}")
                .WithUrl(settings.StreamLink) // Optional: Add the URL to the stream link here as well
                .WithThumbnailUrl(streamIconUrl)
                .WithColor(embedColor) // Set the color based on the selected icon option
                .WithFooter(footer =>
                {
                    footer.Text = $"Solicitado por {Context.User.Username}";
                    footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                })
                .Build();

            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }

        private Color GetEmbedColor(StreamIconOption streamIconOption)
        {
            // Map the StreamIconOption to the desired color
            switch (streamIconOption)
            {
                case StreamIconOption.Twitch:
                    return Color.Purple;
                case StreamIconOption.Youtube:
                    return Color.Red;
                case StreamIconOption.Facebook:
                    return Color.Blue;
                case StreamIconOption.Kick:
                    return Color.Green;
                case StreamIconOption.TikTok:
                    return Color.DarkTeal;
                default:
                    return Color.Default;
            }
        }
    }
}
