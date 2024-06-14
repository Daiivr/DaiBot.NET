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
    public class VGCPastes<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        // Uses VGCPastes Repository Spreadsheet in which they keep track of all current teams
        // https://twitter.com/VGCPastes
        private static async Task<string> DownloadSpreadsheetAsCsv()
        {
            var GID = SysCord<T>.Runner.Config.Trade.VGCPastesConfiguration.GID;
            var csvUrl = $"https://docs.google.com/spreadsheets/d/1axlwmzPA49rYkqXh7zHvAtSP-TKbM0ijGYBPRflLSWw/export?format=csv&gid={GID}";
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(csvUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<List<List<string>>> FetchSpreadsheetData()
        {
            var csvData = await VGCPastes<T>.DownloadSpreadsheetAsCsv();
            var rows = csvData.Split('\n');
            return rows.Select(row => row.Split(',').Select(cell => cell.Trim('"')).ToList()).ToList();
        }

        private static List<(string TrainerName, string PokePasteUrl, string TeamDescription, string DateShared, string RentalCode)> ParsePokePasteData(List<List<string>> data, string? pokemonName = null)
        {
            var pokePasteData = new List<(string TrainerName, string PokePasteUrl, string TeamDescription, string DateShared, string RentalCode)>();
            for (int i = 3; i < data.Count; i++)
            {
                var row = data[i];
                if (row.Count > 40) // Ensure row has a sufficient number of columns to avoid index out of range errors
                {
                    // Additional check for PokePaste URL validation
                    var pokePasteUrl = row[24]?.Trim('"');
                    if (string.IsNullOrWhiteSpace(pokePasteUrl) || !Uri.IsWellFormedUriString(pokePasteUrl, UriKind.Absolute))
                    {
                        continue;
                    }

                    if (pokemonName != null)
                    {
                        var pokemonColumns = row.GetRange(37, 5);
                        if (!pokemonColumns.Any(cell => cell.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                    }

                    // Extract other essential fields
                    var trainerName = row[3]?.Trim('"');
                    var teamDescription = row[1]?.Trim('"');
                    var dateShared = row[29]?.Trim('"');
                    var rentalCode = row[28]?.Trim('"');

                    if (!string.IsNullOrEmpty(trainerName) && !string.IsNullOrEmpty(teamDescription) && !string.IsNullOrEmpty(dateShared) && !string.IsNullOrEmpty(rentalCode))
                    {
                        pokePasteData.Add((trainerName, pokePasteUrl, teamDescription, dateShared, rentalCode));
                    }
                }
            }
            return pokePasteData;
        }

        private static (string PokePasteUrl, List<string> RowData) SelectRandomPokePasteUrl(List<List<string>> data, string? pokemonName = null)
        {
            var filteredData = data.Where(row => row.Count > 40 && Uri.IsWellFormedUriString(row[24]?.Trim('"'), UriKind.Absolute));

            // If a Pokémon name is provided, further filter the rows to those containing the Pokémon name
            if (!string.IsNullOrWhiteSpace(pokemonName))
            {
                filteredData = filteredData.Where(row =>
                    row.GetRange(37, 5).Any(cell => cell.Equals(pokemonName, StringComparison.OrdinalIgnoreCase)));
            }

            var validPokePastes = filteredData.ToList();

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            if (validPokePastes.Count == 0) return (null, null);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.

            var random = new Random();
            var randomIndex = random.Next(validPokePastes.Count);
            var selectedRow = validPokePastes[randomIndex];
            var pokePasteUrl = selectedRow[24]?.Trim('"');

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            return (pokePasteUrl, selectedRow);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }

        // Adjusted command method to use the new selection logic with Pokémon name filtering
        [Command("randomteam")]
        [Alias("rt", "RandomTeam", "Rt")]
        [Summary("Genera un equipo VGC aleatorio a partir de la hoja de cálculo de Google especificada y lo envía como archivos a través de DM.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task GenerateSpreadsheetTeamAsync(string? pokemonName = null)
        {
            if (!SysCord<T>.Runner.Config.Trade.VGCPastesConfiguration.AllowRequests)
            {
                await ReplyAsync($"<a:no:1206485104424128593> {Context.User.Mention} Este módulo está actualmente deshabilitado.").ConfigureAwait(false);
                return;
            }
            var generatingMessage = await ReplyAsync($"<a:loading:1210133423050719283> {Context.User.Mention} Generando y enviando tu equipo VGC desde VGCPastes. Espere por favor...");

            try
            {
                var spreadsheetData = await FetchSpreadsheetData();

                // Use the adjusted method to select a random PokePaste URL (and row data) based on the Pokémon name
                var (PokePasteUrl, selectedRow) = VGCPastes<T>.SelectRandomPokePasteUrl(spreadsheetData, pokemonName);
                if (PokePasteUrl == null)
                {
                    await ReplyAsync("<a:warning:1206483664939126795> No se pudo encontrar una URL de Poke Paste válida con el Pokémon especificado.");
                    return;
                }

                // Extract the associated data from the selected row
                var trainerName = selectedRow[3]?.Trim('"');
                var teamDescription = selectedRow[1]?.Trim('"');
                var dateShared = selectedRow[29]?.Trim('"');
                var rentalCode = selectedRow[28]?.Trim('"');

                // Parse the fetched data
                var pokePasteData = ParsePokePasteData(spreadsheetData, pokemonName);

                // Generate and send the team using the existing code from the VGCTeam command
                var showdownSets = await GetShowdownSetsFromPokePasteUrl(PokePasteUrl);

                if (showdownSets.Count == 0)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron conjuntos de enfrentamiento válidos en la URL de pokepaste: {PokePasteUrl}");
                    return;
                }

                var namer = new GengarNamer();
#pragma warning disable CA1416 // Validate platform compatibility
                var pokemonImages = new List<System.Drawing.Image>();
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CS8604 // Possible null reference argument.
                var sanitizedTeamDescription = SanitizeFileName(teamDescription);
#pragma warning restore CS8604 // Possible null reference argument.
                await using var memoryStream = new MemoryStream();
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

                                await ReplyAsync($"<a:warning:1206483664939126795> Fallo al crear {GameInfo.Strings.Species[template.Species]}: {reason}");
                                continue;
                            }

                            var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                            var fileName = namer.GetName(pk);
                            var entry = archive.CreateEntry($"{fileName}.{pk.Extension}");
                            await using var entryStream = entry.Open();
                            await entryStream.WriteAsync(pk.Data.AsMemory(0, pk.Data.Length));

                            string speciesImageUrl = TradeExtensions<PK9>.PokeImg(pk, false, false);
#pragma warning disable CA1416 // Validate platform compatibility
                            var speciesImage = System.Drawing.Image.FromStream(await new HttpClient().GetStreamAsync(speciesImageUrl));
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                            pokemonImages.Add(speciesImage);
#pragma warning restore CA1416 // Validate platform compatibility
                        }
                        catch (Exception ex)
                        {
                            var speciesName = GameInfo.GetStrings("en").Species[set.Species];
                            await ReplyAsync($"<a:warning:1206483664939126795> Se produjo un error durante el procesamiento de {speciesName}: {ex.Message}");
                        }
                    }
                }

                var combinedImage = CombineImages(pokemonImages);

                memoryStream.Position = 0;

                // Send the ZIP file to the user's DM
                var zipFileName = $"{sanitizedTeamDescription}.zip";
                await Context.User.SendFileAsync(memoryStream, zipFileName);

                // Save the combined image as a file
#pragma warning disable CA1416 // Validate platform compatibility
                combinedImage.Save("spreadsheetteam.png");
#pragma warning restore CA1416 // Validate platform compatibility
                await using (var imageStream = new MemoryStream())
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    combinedImage.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
#pragma warning restore CA1416 // Validate platform compatibility
                    imageStream.Position = 0;

                    var embedBuilder = new EmbedBuilder()
                        .WithColor(GetTypeColor())
                        .WithAuthor(
                            author =>
                            {
                                author
                                    .WithName($"Equipo generado para {Context.User.Username}")
                                    .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());
                            })
                        .WithTitle($"Equipo: {teamDescription}")
                        .AddField("__**Nombre del entrenador**__:", trainerName, true)
                        .AddField("__**Fecha compartida**__:", dateShared, true)
                        .WithDescription(
                                $"{(rentalCode != "Ninguno" ? $"**Código de alquiler:** `{rentalCode}`" : "")}"
                            )
                        .WithImageUrl($"attachment://spreadsheetteam.png")
                        .WithFooter($"Equipo Legalizado Enviado al MD de {Context.User.Username}")
                        .WithCurrentTimestamp();

                    var embed = embedBuilder.Build();

                    var embedMessage = await Context.Channel.SendFileAsync(imageStream, "spreadsheetteam.png", embed: embed);

                    // Clean up the messages after 10 seconds
                    await Task.Delay(10000);
                    await generatingMessage.DeleteAsync();
                    if (Context.Message is IUserMessage userMessage)
                        await userMessage.DeleteAsync().ConfigureAwait(false);
                }

                // Clean up the temporary image file
                File.Delete("spreadsheetteam.png");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} Error al generar el equipo VGC desde la hoja de cálculo: {ex.Message}");
            }
        }

        private static async Task<List<ShowdownSet>> GetShowdownSetsFromPokePasteUrl(string pokePasteUrl)
        {
            var httpClient = new HttpClient();
            var pokePasteHtml = await httpClient.GetStringAsync(pokePasteUrl);
            return ParseShowdownSets(pokePasteHtml);
        }

        private static List<ShowdownSet> ParseShowdownSets(string pokePasteHtml)
        {
            var showdownSets = new List<ShowdownSet>();
            var regex = new Regex(@"<pre>(.*?)</pre>", RegexOptions.Singleline);
            var matches = regex.Matches(pokePasteHtml);
            foreach (Match match in matches.Cast<Match>())
            {
                var showdownText = match.Groups[1].Value;
                showdownText = System.Net.WebUtility.HtmlDecode(Regex.Replace(showdownText, "<.*?>", string.Empty));

                // Update the level to 100 in the showdown set since some level's don't meet minimum requirements
                showdownText = Regex.Replace(showdownText, @"(?i)(?<=\bLevel: )\d+", "100");
                var set = new ShowdownSet(showdownText);
                showdownSets.Add(set);
            }

            return showdownSets;
        }

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

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private static DiscordColor GetTypeColor()
        {
            return new DiscordColor(139, 0, 0); // Dark Red
        }
    }
}
