using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class DonateModule : ModuleBase<SocketCommandContext>
    {
        [Command("donate")]
        [Alias("donation")]
        [Summary("Muestra el enlace de donación del anfitrión.")]
        public async Task PingAsync()
        {
            var settings = SysCordSettings.Settings;
            // Create a new EmbedBuilder
            var embed = new EmbedBuilder
            {
                Color = new Color(255, 0, 0), // Set the color of the embed (optional)
                Title = "¡Enlace de donación!", // Set the title of the embed
                Description = $"¡Aquí está el enlace de donación! Gracias por tu apoyo :3 \n{settings.DonationLink}",
                Url = SysCordSettings.Settings.DonationLink // Set the URL for the title (optional)
            };

            // Add a thumbnail to the embed (optional)
            embed.WithThumbnailUrl("https://smilingwithjerome.com/wp-content/uploads/2019/04/Donation-icon.png");

            // Add a footer to the embed with the bot's username and avatar
            embed.Footer = new EmbedFooterBuilder
            {
                Text = $"Solicitado por {Context.User.Username}",
                IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
            };

            // Send the embed as a reply
            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
