using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord
{
    public class Pokepaste : ModuleBase<SocketCommandContext>
    {
        private static System.Drawing.Image CombineImages(List<System.Drawing.Image> images)
        {
            int width = images.Sum(img => img.Width);
            int height = images.Max(img => img.Height);

            Bitmap combinedImage = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                int offset = 0;
                foreach (System.Drawing.Image img in images)
                {
                    g.DrawImage(img, offset, 0);
                    offset += img.Width;
                }
            }

            return combinedImage;
        }

        [Command("pokepaste")]
        [Alias("pp", "Pokepaste", "PP")]
        [Summary("Genera un equipo a partir de una URL de pokepaste especificada y lo envía como archivos a través de DM.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task GenerateTeamFromUrlAsync(string pokePasteUrl)
        {
            var generatingMessage = await ReplyAsync($"<a:loading:1210133423050719283> {Context.User.Mention} Generando y enviando tu equipo de Pokepaste. Espere por favor...");
            try
            {
                var pokePasteHtml = await GetPokePasteHtml(pokePasteUrl);

                // Extract title from the Pokepaste HTML
                var titleMatch = Regex.Match(pokePasteHtml, @"<h1>(.*?)</h1>");
                var title = titleMatch.Success ? titleMatch.Groups[1].Value : "pokepasteteam";
                // Sanitize the title to make it a valid filename
                title = Regex.Replace(title, "[^a-zA-Z0-9_.-]", "").Trim();
                if (title.Length > 30) title = title[..30]; // Truncate if too long

                var showdownSets = ParseShowdownSets(pokePasteHtml);

                if (showdownSets.Count == 0)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron conjuntos de enfrentamiento válidos en la URL de pokepaste: {pokePasteUrl}");
                    return;
                }

                var namer = new GengarNamer();
                var pokemonImages = new List<System.Drawing.Image>();

                using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var set in showdownSets)
                    {
                        try
                        {
                            var template = AutoLegalityWrapper.GetTemplate(set);
                            var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
                            var pkm = sav.GetLegal(template, out var result);

                            if (pkm is not PK9 pk || !new LegalityAnalysis(pkm).Valid)
                            {
                                var reason = result == "Timeout" ? $"<a:warning:1206483664939126795> Ese conjunto {GameInfo.Strings.Species[template.Species]} tardó demasiado en generarse." :
                                             result == "Failed" ? $"<a:warning:1206483664939126795> No he podido crear un {GameInfo.Strings.Species[template.Species]} a partir de ese conjunto." :
                                             "<a:Error:1223766391958671454> Un error desconocido ocurrió.";

                                await ReplyAsync($"Fallo al crear {GameInfo.Strings.Species[template.Species]}: {reason}");
                                continue;
                            }

                            var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                            var fileName = namer.GetName(pk); // Use GengarNamer to generate the file name
                            var entry = archive.CreateEntry($"{fileName}.{pk.Extension}");
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(pk.Data.AsMemory(0, pk.Data.Length));

                            string speciesImageUrl = AbstractTrade<PK9>.PokeImg(pk, false, false);
                            var speciesImage = System.Drawing.Image.FromStream(await new HttpClient().GetStreamAsync(speciesImageUrl));
                            pokemonImages.Add(speciesImage);
                        }
                        catch (Exception ex)
                        {
                            var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                            await ReplyAsync($"<a:warning:1206483664939126795> Se produjo un error durante el procesamiento. {speciesName}: {ex.Message}");
                        }
                    }
                }

                var combinedImage = CombineImages(pokemonImages);

                memoryStream.Position = 0;

                // Send the ZIP file to the user's DM
                await Context.User.SendFileAsync(memoryStream, $"{title}.zip", text: "Aquí está tu equipo!");

                // Save the combined image as a file
                combinedImage.Save("pokepasteteam.png");
                using (var imageStream = new MemoryStream())
                {
                    combinedImage.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
                    imageStream.Position = 0;

                    // Send the combined image file with an embed to the channel
                    var embedBuilder = new EmbedBuilder()
                        .WithColor(GetTypeColor())
                        .WithAuthor(
                            author =>
                            {
                                author
                                    .WithName($"Equipo generado para {Context.User.Username}")
                                    .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());
                            })
                        .WithImageUrl($"attachment://{title}.png")
                        .WithFooter($"Equipo Legalizado Enviado al MD de {Context.User.Username}")
                        .WithCurrentTimestamp();

                    var embed = embedBuilder.Build();

                    await Context.Channel.SendFileAsync(imageStream, $"{title}.png", embed: embed);

                    // Clean up the messages after 10 seconds
                    await Task.Delay(10000);
                    await generatingMessage.DeleteAsync();
                    if (Context.Message is IUserMessage userMessage)
                        await userMessage.DeleteAsync().ConfigureAwait(false);
                }

                // Clean up the temporary image file
                File.Delete($"{title}.png");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"<a:Error:1223766391958671454> Error al generar el equipo de Pokepaste: {ex.Message}");
            }
        }

        private static async Task<string> GetPokePasteHtml(string pokePasteUrl)
        {
            var httpClient = new HttpClient();
            return await httpClient.GetStringAsync(pokePasteUrl);
        }

        private static List<ShowdownSet> ParseShowdownSets(string pokePasteHtml)
        {
            var showdownSets = new List<ShowdownSet>();
            var regex = new Regex(@"<pre>(.*?)</pre>", RegexOptions.Singleline);
            var matches = regex.Matches(pokePasteHtml);
            foreach (Match match in matches)
            {
                var showdownText = match.Groups[1].Value;
                showdownText = System.Net.WebUtility.HtmlDecode(Regex.Replace(showdownText, "<.*?>", string.Empty));
                showdownText = Regex.Replace(showdownText, @"(?i)(?<=\bLevel: )\d+", "100");
                var set = new ShowdownSet(showdownText);
                showdownSets.Add(set);
            }

            return showdownSets;
        }

        private static DiscordColor GetTypeColor()
        {
            return new DiscordColor(255, 165, 0); // Orange
        }
    }
}
