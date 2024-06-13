using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord
{
    public class Pokepaste : ModuleBase<SocketCommandContext>
    {
        private static System.Drawing.Image CombineImages(List<System.Drawing.Image> images)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            int width = images.Sum(img => img.Width);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            int height = images.Max(img => img.Height);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            Bitmap combinedImage = new Bitmap(width, height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                int offset = 0;
                foreach (System.Drawing.Image img in images)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    g.DrawImage(img, offset, 0);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    offset += img.Width;
#pragma warning restore CA1416 // Validate platform compatibility
                }
            }
#pragma warning restore CA1416 // Validate platform compatibility

            return combinedImage;
        }

        [Command("pokepaste")]
        [Alias("pp", "Pokepaste", "PP")]
        [Summary("Genera un equipo a partir de una URL de pokepaste especificada y lo envía como archivos a través de DM.")]
        public async Task GenerateTeamFromUrlAsync(string pokePasteUrl)
        {
            var generatingMessage = await ReplyAsync("<a:loading:1210133423050719283> {Context.User.Mention} Generando y enviando tu equipo de Pokepaste. Espere por favor...");
            try
            {
                await Task.Run(async () =>
                {
                    var pokePasteHtml = await Task.Run(() => GetPokePasteHtml(pokePasteUrl)).ConfigureAwait(false);

                    // Extract title from the Pokepaste HTML
                    var titleMatch = Regex.Match(pokePasteHtml, @"<h1>(.*?)</h1>");
                    var title = titleMatch.Success ? titleMatch.Groups[1].Value : "pokepasteteam";

                    // Sanitize the title to make it a valid filename
                    title = Regex.Replace(title, "[^a-zA-Z0-9_.-]", "").Trim();
                    if (title.Length > 30) title = title[..30]; // Truncate if too long

                    var showdownSets = ParseShowdownSets(pokePasteHtml);

                    if (showdownSets.Count == 0)
                    {
                        await ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron conjuntos de enfrentamiento válidos en la URL de pokepaste: {pokePasteUrl}", 10, generatingMessage).ConfigureAwait(false);
                        return;
                    }

                    var namer = new GengarNamer();
#pragma warning disable CA1416 // Validate platform compatibility
                    var pokemonImages = new List<System.Drawing.Image>();
#pragma warning restore CA1416 // Validate platform compatibility

                    await using var memoryStream = new MemoryStream();
                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        foreach (var set in showdownSets)
                        {
                            try
                            {
                                var template = AutoLegalityWrapper.GetTemplate(set);
                                var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();

                                PK9? pk = null;
                                var timeout = TimeSpan.FromSeconds(10); // Adjust the timeout as needed
                                using (var cts = new CancellationTokenSource(timeout))
                                {
                                    try
                                    {
                                        pk = (PK9?)await Task.Run(() => sav.GetLegal(template, out _), cts.Token).ConfigureAwait(false);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        await ReplyAndDeleteAsync($"<a:warning:1206483664939126795> Se produjo un tiempo de espera durante la generación {GameInfo.Strings.Species[template.Species]}. Salteando...", 10, generatingMessage).ConfigureAwait(false);
                                        continue;
                                    }
                                }

                                if (pk == null || !new LegalityAnalysis(pk).Valid)
                                {
                                    await ReplyAndDeleteAsync($"<a:warning:1206483664939126795> Fallo al crear {GameInfo.Strings.Species[template.Species]}. Salteando...", 10, generatingMessage).ConfigureAwait(false);
                                    continue;
                                }

                                var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                                var fileName = namer.GetName(pk); // Use GengarNamer to generate the file name
                                var entry = archive.CreateEntry($"{fileName}.{pk.Extension}");
                                await using var entryStream = entry.Open();
                                await entryStream.WriteAsync(pk.Data.AsMemory(0, pk.Data.Length)).ConfigureAwait(false);

                                string speciesImageUrl = AbstractTrade<PK9>.PokeImg(pk, false, false);
#pragma warning disable CA1416 // Validate platform compatibility
                                var speciesImage = await Task.Run(() => System.Drawing.Image.FromStream(new HttpClient().GetStreamAsync(speciesImageUrl).Result)).ConfigureAwait(false);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                                pokemonImages.Add(speciesImage);
#pragma warning restore CA1416 // Validate platform compatibility
                            }
                            catch (Exception ex)
                            {
                                var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                                await ReplyAndDeleteAsync($"<a:warning:1206483664939126795> Se produjo un error durante el procesamiento. {speciesName}: {ex.Message}", 10, generatingMessage).ConfigureAwait(false);
                            }
                        }
                    }

                    var combinedImage = CombineImages(pokemonImages);

                    memoryStream.Position = 0;

                    // Send the ZIP file to the user's DM
                    await Context.User.SendFileAsync(memoryStream, $"{title}.zip", text: "Aquí está tu equipo!").ConfigureAwait(false);

                    // Save the combined image as a file
#pragma warning disable CA1416 // Validate platform compatibility
                    combinedImage.Save($"{title}.png");
#pragma warning restore CA1416 // Validate platform compatibility
                    await using (var imageStream = new MemoryStream())
                    {
#pragma warning disable CA1416 // Validate platform compatibility
                        combinedImage.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
#pragma warning restore CA1416 // Validate platform compatibility
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
                            .WithFooter($"Equipo Legalizado y Enviado al MD de {Context.User.Username}")
                            .WithCurrentTimestamp();

                        var embed = embedBuilder.Build();

                        await Context.Channel.SendFileAsync(imageStream, $"{title}.png", embed: embed).ConfigureAwait(false);

                        // Clean up the messages after 10 seconds
                        await DeleteMessagesAfterDelayAsync(generatingMessage, Context.Message, 10).ConfigureAwait(false);
                    }

                    // Clean up the temporary image file
                    File.Delete($"{title}.png");
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyAndDeleteAsync($"<a:Error:1223766391958671454> Error al generar el equipo de Pokepaste: {ex.Message}", 10, generatingMessage).ConfigureAwait(false);
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
            foreach (Match match in regex.Matches(pokePasteHtml))
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

        private async Task ReplyAndDeleteAsync(string message, int delaySeconds, IMessage? messageToDelete = null)
        {
            try
            {
                var sentMessage = await ReplyAsync(message).ConfigureAwait(false);
                _ = DeleteMessagesAfterDelayAsync(sentMessage, messageToDelete, delaySeconds);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(Pokepaste));
            }
        }

        private async Task DeleteMessagesAfterDelayAsync(IMessage sentMessage, IMessage? messageToDelete, int delaySeconds)
        {
            try
            {
                await Task.Delay(delaySeconds * 1000).ConfigureAwait(false);
                await sentMessage.DeleteAsync().ConfigureAwait(false);
                if (messageToDelete != null)
                    await messageToDelete.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(Pokepaste));
            }
        }
    }
}
