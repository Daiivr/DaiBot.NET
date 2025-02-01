using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using System;

namespace SysBot.Pokemon.Discord
{
    public class DonateModule : ModuleBase<SocketCommandContext>
    {
        private static readonly string[] ThankYouMessages = new[]
        {
            "¡Gracias por tu apoyo!", "¡Tu donación significa mucho!", "¡Eres increíble por apoyarnos!", "¡Gracias por ser parte de esto!", "¡Tu generosidad es apreciada!"
        };

        [Command("donate")]
        [Alias("donation")]
        [Summary("Muestra el enlace de donación del anfitrión.")]
        public async Task PingAsync()
        {
            var settings = SysCordSettings.Settings;
            var random = new Random();
            var thankYouMessage = ThankYouMessages[random.Next(ThankYouMessages.Length)];

            // Fetch donation settings from DonationOptions
            var donationSettings = settings.Donation;

            // Parse donation goal and current donations from settings
            double donationGoal = ParseDonationValue(donationSettings.DonationGoal);
            double currentDonations = ParseDonationValue(donationSettings.DonationCurrent);

            // Calculate progress towards the donation goal
            double progress = donationGoal > 0 ? currentDonations / donationGoal : 0; // Avoid division by zero
            string progressBar = GetProgressBar(progress);
            string progressText = $"**${currentDonations:0.00} / ${donationGoal:0.00}** ({progress * 100:0}%)";

            // Create a new EmbedBuilder
            var embed = new EmbedBuilder
            {
                Color = new Color(255, 59, 48), // A vibrant red color for donations
                Title = "❤️ ¡Enlace de Donación! ❤️", // Set the title of the embed
                Description = $"{thankYouMessage} \n\n[¡Haz clic aquí para donar!]({donationSettings.DonationLink})",
                Url = donationSettings.DonationLink // Set the URL for the title
            };

            // Add a thumbnail to the embed
            embed.WithThumbnailUrl("https://smilingwithjerome.com/wp-content/uploads/2019/04/Donation-icon.png");

            // Add the progress bar and goal tracker to the embed
            embed.AddField("Progreso de la Meta de Donaciones", $"{progressBar}\n{progressText}");

            // Add a footer to the embed with the user's username and avatar
            embed.WithFooter(footer =>
            {
                footer.Text = $"Solicitado por {Context.User.Username}";
                footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
            });

            // Add a timestamp
            embed.WithCurrentTimestamp();

            // Send the embed as a reply
            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        // Helper method to create a progress bar
        private string GetProgressBar(double progress)
        {
            const int totalBlocks = 10; // Number of blocks in the progress bar
            int filledBlocks = (int)(progress * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks); // Filled blocks
            string empty = new string('░', emptyBlocks); // Empty blocks

            return $"{filled}{empty}";
        }

        // Helper method to parse donation values from settings
        private double ParseDonationValue(string value)
        {
            if (double.TryParse(value, out double result))
            {
                return result;
            }
            return 0; // Default to 0 if parsing fails
        }
    }
}
