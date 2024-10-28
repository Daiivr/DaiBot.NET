using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using SysBot.Pokemon.Discord.Commands.Bots;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    // A dictionary to hold batch trade file paths and their deletion status
    private static readonly Dictionary<int, List<string>> batchTradeFiles = [];

    private static readonly Dictionary<ulong, int> userBatchTradeMaxDetailId = [];

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isHiddenTrade = false, bool isMysteryTrade = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync($"<a:warning:1206483664939126795> {context.User.Mention} El código de tradeo debe ser un numero entre: **00000000-99999999**!").ConfigureAwait(false);
            return;
        }

        try
        {
            if (!isBatchTrade || batchTradeNumber == 1)
            {
                if (trade is PB7 && lgcode != null)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, "Tu código de tradeo sera:", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);
                }
            }

            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, isBatchTrade ? PokeTradeType.Batch : type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryTrade, isMysteryEgg, lgcode, ignoreAutoOT, setEdited, isNonNative).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, bool ignoreAutoOT = false)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User, ignoreAutoOT: ignoreAutoOT);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades, bool isHiddenTrade, bool isMysteryTrade = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        string tradingUrl = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ExtraEmbedOptions.TradingBotUrl;
        string NonNative = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ExtraEmbedOptions.NonNativeTexT;
        string AutocorrectText = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.ExtraEmbedOptions.AutocorrectText;
        var user = trader;
        var userID = user.Id;
        var name = user.Username;
        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
#pragma warning disable CS8604 // Possible null reference argument.
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades, isMysteryTrade, isMysteryEgg, lgcode: lgcode);
#pragma warning restore CS8604 // Possible null reference argument.
        var uniqueTradeID = GenerateUniqueTradeID();
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored, lgcode, batchTradeNumber, totalBatchTrades, isMysteryTrade, isMysteryEgg, uniqueTradeID, ignoreAutoOT, setEdited);
        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.LinkTrade, name, uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var allowMultiple = isBatchTrade;
        var isSudo = sig == RequestSignificance.Owner;
        var added = Info.AddToTradeQueue(trade, userID, allowMultiple, isSudo);

        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            return new TradeQueueResult(false);
        }

        var embedData = DetailsExtractor<T>.ExtractPokemonDetails(
            pk, trader, isMysteryTrade, isMysteryEgg, type == PokeRoutineType.Clone, type == PokeRoutineType.Dump,
            type == PokeRoutineType.FixOT, type == PokeRoutineType.SeedCheck, isBatchTrade, batchTradeNumber, totalBatchTrades, t
        );

        try
        {
            var itemImageUrl = string.Empty;
            if (t == PokeTradeType.Item && !string.IsNullOrWhiteSpace(embedData.HeldItem))
            {
                string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                itemImageUrl = $"https://serebii.net/itemdex/sprites/sv/{heldItemName}.png";
            }

            (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(pk, t, itemImageUrl);

            embedData.EmbedImageUrl = isMysteryTrade ? "https://i.imgur.com/FdESYAv.png" :
                                      isMysteryEgg ? "https://i.imgur.com/AwuBs4D.png" :
                                       type == PokeRoutineType.Dump ? "https://i.imgur.com/9wfEHwZ.png" :
                                       type == PokeRoutineType.Clone ? "https://i.imgur.com/aSTCjUn.png" :
                                       type == PokeRoutineType.SeedCheck ? "https://i.imgur.com/EI1BHr5.png" :
                                       type == PokeRoutineType.FixOT ? "https://i.imgur.com/gRZGFIi.png" :
                                       embedImageUrl;

            embedData.HeldItemUrl = string.Empty;
            if (!string.IsNullOrWhiteSpace(embedData.HeldItem))
            {
                string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                // Verificar el tipo de intercambio para decidir la URL
                if (t == PokeTradeType.Item)
                {
                    embedData.HeldItemUrl = $"https://serebii.net/itemdex/sprites/sv/{heldItemName}.png";
                }
                else
                {
                    embedData.HeldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
                }
            }

            embedData.IsLocalFile = File.Exists(embedData.EmbedImageUrl);

            var position = Info.CheckPosition(userID, uniqueTradeID, type);
            var botct = Info.Hub.Bots.Count;
            var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;
            var etaMessage = $"Tiempo Estimado: {baseEta:F1} minuto(s) para el trade {batchTradeNumber}/{totalBatchTrades}.";
            string footerText = string.Empty;

            if (totalTradeCount > 0)
            {
                footerText += $"Trades: {totalTradeCount}\n";
            }

            footerText += $"Posición actual: {(position.Position == -1 ? 1 : position.Position)}";

            string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails);
            if (!string.IsNullOrEmpty(userDetailsText))
            {
                footerText += $"\n{userDetailsText}";
            }
            footerText += $"\n{etaMessage}";

            var embedBuilder = new EmbedBuilder()
                .WithColor(embedColor)
                .WithFooter(footerText)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(embedData.AuthorName)
                    .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                    .WithUrl(tradingUrl));

            // Decidir la imagen principal y el thumbnail basado en el tipo de intercambio
            if (t == PokeTradeType.Item && !string.IsNullOrEmpty(embedData.HeldItemUrl))
            {
                embedBuilder.WithImageUrl(embedData.HeldItemUrl);
            }
            else
            {
                if (embedData.IsLocalFile)
                {
                    embedBuilder.WithImageUrl($"attachment://{Path.GetFileName(embedData.EmbedImageUrl)}");
                }
                else
                {
                    embedBuilder.WithImageUrl(embedData.EmbedImageUrl);
                }
            }

            DetailsExtractor<T>.AddAdditionalText(embedBuilder);

            if (!isMysteryTrade && !isMysteryEgg && type != PokeRoutineType.Clone && type != PokeRoutineType.Dump && type != PokeRoutineType.FixOT && type != PokeRoutineType.SeedCheck)
            {
                DetailsExtractor<T>.AddNormalTradeFields(embedBuilder, embedData, trader.Mention, pk);
            }
            else
            {
                DetailsExtractor<T>.AddSpecialTradeFields(embedBuilder, isMysteryTrade, isMysteryEgg, type == PokeRoutineType.SeedCheck, type == PokeRoutineType.Clone, type == PokeRoutineType.FixOT, trader.Mention);
            }

            if (setEdited && Info.Hub.Config.Trade.AutoCorrectConfig.AutoCorrectEmbedIndicator)
            {
                embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/setedited.png";
                embedBuilder.AddField("__**Aviso**__: **Tu conjunto de showdown no era válido.**", $"{AutocorrectText}");
            }
            // Check if the Pokemon is Non-Native and/or has a Home Tracker
            if (pk is IHomeTrack homeTrack)
            {
                if (homeTrack.HasTracker && isNonNative)
                {
                    // Both Non-Native and has Home Tracker
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/exclamation.gif";
                    embedBuilder.AddField("__**Aviso**__: **Este Pokémon no es nativo y tiene rastreador de Home.**", "*AutoOT no fue aplicado.*");
                }
                else if (homeTrack.HasTracker)
                {
                    // Only has Home Tracker
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/exclamation.gif";
                    embedBuilder.AddField("__**Aviso**__: **Rastreador de HOME detectado.**", "*AutoOT no fue aplicado.*");
                }
                else if (isNonNative)
                {
                    // Only Non-Native
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/exclamation.gif";
                    embedBuilder.AddField("__**Aviso**__: **Este Pokémon no es nativo.**", $"{NonNative}");
                }
            }
            else if (isNonNative)
            {
                // Fallback for Non-Native Pokemon that don't implement IHomeTrack
                embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/exclamation.gif";
                embedBuilder.AddField("__**Aviso**__: **Este Pokémon no es nativo.**", $"{NonNative}");
            }

            DetailsExtractor<T>.AddThumbnails(embedBuilder, type == PokeRoutineType.Clone, type == PokeRoutineType.SeedCheck, type == PokeRoutineType.Dump, type == PokeRoutineType.FixOT, embedData.HeldItemUrl, pk, t);

            if (!isHiddenTrade && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
            {
                var embed = embedBuilder.Build();
                if (embed == null)
                {
                    Console.WriteLine("Error: El embed es nulo.");
                    await context.Channel.SendMessageAsync("<a:warning:1206483664939126795> Se produjo un error al preparar los detalles comerciales..");
                    return new TradeQueueResult(false);
                }

                if (t != PokeTradeType.Item && embedData.IsLocalFile)
                {
                    // Envía el archivo local solo si no es un intercambio de tipo Item
                    await context.Channel.SendFileAsync(embedData.EmbedImageUrl, embed: embed);
                    if (isBatchTrade)
                    {
                        userBatchTradeMaxDetailId[userID] = Math.Max(userBatchTradeMaxDetailId.GetValueOrDefault(userID), detail.ID);
                        await ScheduleFileDeletion(embedData.EmbedImageUrl, 0, detail.ID);
                        if (detail.ID == userBatchTradeMaxDetailId[userID] && batchTradeNumber == totalBatchTrades)
                        {
                            DeleteBatchTradeFiles(detail.ID);
                        }
                    }
                    else
                    {
                        await ScheduleFileDeletion(embedData.EmbedImageUrl, 0);
                    }
                }
                else
                {
                    await context.Channel.SendMessageAsync(embed: embed);
                }
            }
            else
            {
                var message = $"{trader.Mention} ➜ Agregado a la cola de intercambio de enlaces. Posicion actual: {position.Position}. Recibiendo: **{embedData.SpeciesName}**.\n{etaMessage}";
                await context.Channel.SendMessageAsync(message);
            }
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex);
            return new TradeQueueResult(false);
        }

        return new TradeQueueResult(true);
    }

    private static int GenerateUniqueTradeID()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomValue = new Random().Next(1000);
        return ((int)(timestamp % int.MaxValue) * 1000) + randomValue;
    }

    private static string GetImageFolderPath()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string imagesFolder = Path.Combine(baseDirectory, "Images");

        if (!Directory.Exists(imagesFolder))
        {
            Directory.CreateDirectory(imagesFolder);
        }

        return imagesFolder;
    }

    private static string SaveImageLocally(System.Drawing.Image image)
    {
        // Get the path to the images folder
        string imagesFolderPath = GetImageFolderPath();

        // Create a unique filename for the image
        string filePath = Path.Combine(imagesFolderPath, $"image_{Guid.NewGuid()}.png");

        // Save the image to the specified path
#pragma warning disable CA1416 // Validate platform compatibility
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
#pragma warning restore CA1416 // Validate platform compatibility

        return filePath;
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetails(T pk, PokeTradeType tradeType, string itemImageUrl = "")
    {
        string embedImageUrl;
        string speciesImageUrl;

        if (pk.IsEgg)
        {
            const string eggImageUrl = "https://i.imgur.com/3Tb8VX3.png";
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, false, true, null);
            System.Drawing.Image combinedImage = await OverlaySpeciesOnEgg(eggImageUrl, speciesImageUrl);
            embedImageUrl = SaveImageLocally(combinedImage);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, canGmax, false, SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.PreferredImageSize);
            embedImageUrl = speciesImageUrl;
        }

        var strings = GameInfo.GetStrings(1);
        string ballName = strings.balllist[pk.Ball];
        if (ballName.Contains("(LA)"))
        {
            ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
        }
        else
        {
            ballName = ballName.Replace(" ", "").ToLower();
        }

        string ballImgUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/AltBallImg/20x20/{ballName}.png";

        if (Uri.TryCreate(embedImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using var localImage = System.Drawing.Image.FromFile(uri.LocalPath);
#pragma warning restore CA1416 // Validate platform compatibility
            using var ballImage = await LoadImageFromUrl(ballImgUrl);
            if (ballImage != null)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                using (var graphics = Graphics.FromImage(localImage))
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var ballPosition = new Point(localImage.Width - ballImage.Width, localImage.Height - ballImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    graphics.DrawImage(ballImage, ballPosition);
#pragma warning restore CA1416 // Validate platform compatibility
                }
#pragma warning restore CA1416 // Validate platform compatibility
                embedImageUrl = SaveImageLocally(localImage);
            }
        }
        else
        {
            (System.Drawing.Image finalCombinedImage, bool ballImageLoaded) = await OverlayBallOnSpecies(speciesImageUrl, ballImgUrl);
            embedImageUrl = SaveImageLocally(finalCombinedImage);

            if (!ballImageLoaded)
            {
                Console.WriteLine($"No se pudo cargar la imagen de la pokeball: {ballImgUrl}");
            }
        }

        DiscordColor embedColor;

        if (tradeType == PokeTradeType.Item && !string.IsNullOrEmpty(itemImageUrl))
        {
            (int R, int G, int B) = await GetDominantColorAsync(itemImageUrl);
            embedColor = new DiscordColor(R, G, B);
        }
        else
        {
            string colorImageUrl = pk.IsEgg ? speciesImageUrl : embedImageUrl;
            (int R, int G, int B) = await GetDominantColorAsync(colorImageUrl);
            embedColor = new DiscordColor(R, G, B);
        }

        return (embedImageUrl, embedColor);
    }

    private static async Task<(System.Drawing.Image, bool)> OverlayBallOnSpecies(string speciesImageUrl, string ballImageUrl)
    {
        using (var speciesImage = await LoadImageFromUrl(speciesImageUrl))
        {
            if (speciesImage == null)
            {
                Console.WriteLine("No se pudo cargar la imagen de la especie.");
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
                return (null, false);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
            }

            var ballImage = await LoadImageFromUrl(ballImageUrl);
            if (ballImage == null)
            {
                Console.WriteLine($"No se pudo cargar la imagen de la pokeball: {ballImageUrl}");
#pragma warning disable CA1416 // Validate platform compatibility
                return ((System.Drawing.Image)speciesImage.Clone(), false);
#pragma warning restore CA1416 // Validate platform compatibility
            }

            using (ballImage)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                using (var graphics = Graphics.FromImage(speciesImage))
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var ballPosition = new Point(speciesImage.Width - ballImage.Width, speciesImage.Height - ballImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    graphics.DrawImage(ballImage, ballPosition);
#pragma warning restore CA1416 // Validate platform compatibility
                }

#pragma warning disable CA1416 // Validate platform compatibility
                return ((System.Drawing.Image)speciesImage.Clone(), true);
#pragma warning restore CA1416 // Validate platform compatibility
            }
        }
    }

    private static async Task<System.Drawing.Image> OverlaySpeciesOnEgg(string eggImageUrl, string speciesImageUrl)
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        System.Drawing.Image eggImage = await LoadImageFromUrl(eggImageUrl);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        System.Drawing.Image speciesImage = await LoadImageFromUrl(speciesImageUrl);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1416 // Validate platform compatibility
        double scaleRatio = Math.Min((double)eggImage.Width / speciesImage.Width, (double)eggImage.Height / speciesImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1416 // Validate platform compatibility
        Size newSize = new Size((int)(speciesImage.Width * scaleRatio), (int)(speciesImage.Height * scaleRatio));
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        System.Drawing.Image resizedSpeciesImage = new Bitmap(speciesImage, newSize);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics g = Graphics.FromImage(eggImage))
        {
            // Calculate the position to center the species image on the egg image
#pragma warning disable CA1416 // Validate platform compatibility
            int speciesX = (eggImage.Width - resizedSpeciesImage.Width) / 2;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            int speciesY = (eggImage.Height - resizedSpeciesImage.Height) / 2;
#pragma warning restore CA1416 // Validate platform compatibility

            // Draw the resized and centered species image over the egg image
#pragma warning disable CA1416 // Validate platform compatibility
            g.DrawImage(resizedSpeciesImage, speciesX, speciesY, resizedSpeciesImage.Width, resizedSpeciesImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        // Dispose of the species image and the resized species image if they're no longer needed
#pragma warning disable CA1416 // Validate platform compatibility
        speciesImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        resizedSpeciesImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

        // Calculate scale factor for resizing while maintaining aspect ratio
#pragma warning disable CA1416 // Validate platform compatibility
        double scale = Math.Min(128.0 / eggImage.Width, 128.0 / eggImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility

        // Calculate new dimensions
#pragma warning disable CA1416 // Validate platform compatibility
        int newWidth = (int)(eggImage.Width * scale);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        int newHeight = (int)(eggImage.Height * scale);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        Bitmap finalImage = new Bitmap(128, 128);
#pragma warning restore CA1416 // Validate platform compatibility

        // Draw the resized egg image onto the new bitmap, centered
#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics g = Graphics.FromImage(finalImage))
        {
            // Calculate centering position
            int x = (128 - newWidth) / 2;
            int y = (128 - newHeight) / 2;

            // Draw the image
#pragma warning disable CA1416 // Validate platform compatibility
            g.DrawImage(eggImage, x, y, newWidth, newHeight);
#pragma warning restore CA1416 // Validate platform compatibility
        }
#pragma warning disable CA1416 // Validate platform compatibility
        eggImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
        return finalImage;
    }

    private static async Task<System.Drawing.Image?> LoadImageFromUrl(string url)
    {
        using HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"No se pudo cargar la imagen desde {url}. Código de estado: {response.StatusCode}");
            return null;
        }

        Stream stream = await response.Content.ReadAsStreamAsync();
        if (stream == null || stream.Length == 0)
        {
            Console.WriteLine($"No se recibieron datos o flujo vacío de {url}");
            return null;
        }

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return System.Drawing.Image.FromStream(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"No se pudo crear la imagen a partir de la transmisión. URL: {url}, excepción: {ex}");
            return null;
        }
    }

    private static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds, int batchTradeId = -1)
    {
        if (batchTradeId != -1)
        {
            // If this is part of a batch trade, add the file path to the dictionary
            if (!batchTradeFiles.TryGetValue(batchTradeId, out List<string>? value))
            {
                value = ([]);
                batchTradeFiles[batchTradeId] = value;
            }

            value.Add(filePath);
        }
        else
        {
            // If this is not part of a batch trade, delete the file after the delay
            await Task.Delay(delayInMilliseconds);
            DeleteFile(filePath);
        }
    }

    private static void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error al eliminar el archivo: {ex.Message}");
            }
        }
    }

    // Call this method after the last trade in a batch is completed
    private static void DeleteBatchTradeFiles(int batchTradeId)
    {
        if (batchTradeFiles.TryGetValue(batchTradeId, out var files))
        {
            foreach (var filePath in files)
            {
                DeleteFile(filePath);
            }
            batchTradeFiles.Remove(batchTradeId);
        }
    }

    public enum AlcremieDecoration
    {
        Strawberry = 0,

        Berry = 1,

        Love = 2,

        Star = 3,

        Clover = 4,

        Flower = 5,

        Ribbon = 6,
    }

    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
#pragma warning disable CA1416 // Validate platform compatibility
            for (int y = 0; y < image.Height; y++)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                for (int x = 0; x < image.Width; x++)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var pixelColor = image.GetPixel(x, y);
#pragma warning restore CA1416 // Validate platform compatibility

                    if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                    var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                    var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                    var combinedFactor = brightnessFactor + saturationFactor;

                    var quantizedColor = Color.FromArgb(
                        pixelColor.R / 10 * 10,
                        pixelColor.G / 10 * 10,
                        pixelColor.B / 10 * 10
                    );

                    if (colorCount.ContainsKey(quantizedColor))
                    {
                        colorCount[quantizedColor] += combinedFactor;
                    }
                    else
                    {
                        colorCount[quantizedColor] = combinedFactor;
                    }
                }
            }

#pragma warning disable CA1416 // Validate platform compatibility
            image.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"Error al procesar la imagen desde {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            await using var stream = await response.Content.ReadAsStreamAsync();
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        else
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(imagePath);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        // Nag the owner in logs.
                        message = "¡Debes otorgarme permisos para \"Enviar mensajes\"!";
                        Base.LogUtil.LogError(message, "QueueHelper");
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> ¡Debes otorgarme permisos de \"Administrar mensajes\"!";
                    }
                }
                break;

            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    // The user either has DMs turned off, or Discord thinks they do.
                    message = context.User == trader ? "Debes habilitar los mensajes privados para estar en la cola.!" : "El usuario mencionado debe habilitar los mensajes privados para que estén en cola.!";
                }
                break;

            default:
                {
                    // Send a generic error message.
                    message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    public static (string, Embed) CreateLGLinkCodeSpriteEmbed(List<Pictocodes> lgcode)
    {
        int codecount = 0;
        List<System.Drawing.Image> spritearray = [];
        foreach (Pictocodes cd in lgcode)
        {
            var showdown = new ShowdownSet(cd.ToString());
            var sav = SaveUtil.GetBlankSAV(EntityContext.Gen7b, "pip");
            PKM pk = sav.GetLegalFromSet(showdown).Created;
#pragma warning disable CA1416 // Validate platform compatibility
            System.Drawing.Image png = pk.Sprite();
#pragma warning restore CA1416 // Validate platform compatibility
            var destRect = new Rectangle(-40, -65, 137, 130);
#pragma warning disable CA1416 // Validate platform compatibility
            var destImage = new Bitmap(137, 130);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            using (var graphics = Graphics.FromImage(destImage))
            {
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
            }
#pragma warning restore CA1416 // Validate platform compatibility
            png = destImage;
#pragma warning disable CA1416 // Validate platform compatibility
            spritearray.Add(png);
#pragma warning restore CA1416 // Validate platform compatibility
            codecount++;
        }
#pragma warning disable CA1416 // Validate platform compatibility
        int outputImageWidth = spritearray[0].Width + 20;
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        int outputImageHeight = spritearray[0].Height - 65;
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{Directory.GetCurrentDirectory()}//finalcode.png";
#pragma warning disable CA1416 // Validate platform compatibility
        finalembedpic.Save(filename);
#pragma warning restore CA1416 // Validate platform compatibility
        filename = Path.GetFileName($"{Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }
}
