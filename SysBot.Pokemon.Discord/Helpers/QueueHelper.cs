using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Discord.Commands.Bots;
using System.Collections.Generic;
using System;
using System.Drawing;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using SysBot.Pokemon.Helpers;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using static TeraTypeDictionaries;
using static MovesTranslationDictionary;
using static AbilityTranslationDictionary;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    // A dictionary to hold batch trade file paths and their deletion status
    private static Dictionary<int, List<string>> batchTradeFiles = [];
    private static Dictionary<ulong, int> userBatchTradeMaxDetailId = [];

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, int formArgument = 0, bool isMysteryEgg = false, List<Pictocodes> lgcode = null)
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
                const string helper = "<a:yes:1206485105674166292> Te he añadido a la __lista__! Te enviaré un __mensaje__ aquí cuando comience tu operación...";
                IUserMessage test = await trader.SendMessageAsync(helper).ConfigureAwait(false);
                if (trade is PB7 && lgcode != null)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, $"Tu código de tradeo sera: ", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await trader.SendMessageAsync($"Tu código de tradeo sera: **{code:0000 0000}**").ConfigureAwait(false);
                }
            }

            // Add to trade queue and get the result
            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, formArgument, isMysteryEgg, lgcode).ConfigureAwait(false);
            // Delete the user's join message for privacy
            if(!isBatchTrade && !context.IsPrivate)
                await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades, int formArgument = 0, bool isMysteryEgg = false, List<Pictocodes> lgcode = null)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades, isMysteryEgg, lgcode);
        var uniqueTradeID = GenerateUniqueTradeID();
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored, lgcode, batchTradeNumber, totalBatchTrades, isMysteryEgg, uniqueTradeID);
        var trade = new TradeEntry<T>(detail, userID, type, name, uniqueTradeID);
        var strings = GameInfo.GetStrings(1);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var canAddMultiple = isBatchTrade || sig == RequestSignificance.None;
        var added = Info.AddToTradeQueue(trade, userID, canAddMultiple);
        bool useTypeEmojis = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MoveTypeEmojis;
        string maleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MaleEmoji.EmojiString;
        string femaleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.FemaleEmoji.EmojiString;

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            return new TradeQueueResult(false);
        }

        var position = Info.CheckPosition(userID, uniqueTradeID, type);
        var botct = Info.Hub.Bots.Count;
        var etaMessage = "";
        if (position.Position > botct)
        {
            var baseEta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
            // Increment ETA by 1 minute for each batch trade
            var adjustedEta = baseEta + (batchTradeNumber - 1);
            etaMessage = $"Estimado: {adjustedEta:F1} min(s) para el tradeo {batchTradeNumber}/{totalBatchTrades}.";
        }
        else
        {
            var adjustedEta = (batchTradeNumber - 1); // Add 1 minute for each subsequent batch trade
            etaMessage = $"Estimado: {adjustedEta:F1} minuto(s) para el tradeo {batchTradeNumber}/{totalBatchTrades}.";
        }

        var scaleEmojis = ScaleEmojisDictionary.ScaleEmojis;
        string scale = "";

        if (pk is PA8 fin8a)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8a.Scale).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8a.Scale})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8a.Scale})";
            }
        }
        else if (pk is PB7 fin7b)
        {
            // For PB7 type, do nothing to exclude the scale from the embed
        }
        if (pk is PB8 fin8b)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8b.HeightScalar).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8b.HeightScalar})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8b.HeightScalar})";
            }
        }
        if (pk is PK8 fin8)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin8.HeightScalar).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin8.HeightScalar})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin8.HeightScalar})";
            }
        }
        if (pk is PK9 fin9)
        {
            string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin9.Scale).ToString();

            // Check if the scale value has a corresponding emoji
            if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
            {
                // Use the emoji code in the message
                scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin9.Scale})";
            }
            else
            {
                // If no emoji is found, just display the scale text
                scale = $"**Tamaño**: {scaleRating} ({fin9.Scale})";
            }
        }

        var typeEmojis = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.CustomTypeEmojis
                             .Where(e => !string.IsNullOrEmpty(e.EmojiName) && !string.IsNullOrEmpty(e.ID))
                             .ToDictionary(
                                 e => e.MoveType,
                                 e => $"<:{e.EmojiName}:{e.ID}>"
                             );

        // Format IVs for display
        int[] ivs = pk.IVs;
        string ivsDisplay = $"{ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}";

        ushort[] moves = new ushort[4];
        pk.GetMoves(moves.AsSpan());
        List<int> movePPs = [pk.Move1_PP, pk.Move2_PP, pk.Move3_PP, pk.Move4_PP];
        List<string> moveNames = [];
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == 0) continue;
            string moveName = GameInfo.MoveDataSource.FirstOrDefault(m => m.Value == moves[i])?.Text ?? "";
            string translatedMoveName = MovesTranslation.ContainsKey(moveName) ? MovesTranslation[moveName] : moveName;
            byte moveTypeId = MoveInfo.GetType(moves[i], default);
            MoveType moveType = (MoveType)moveTypeId;
            string formattedMove = $"{translatedMoveName} ({movePPs[i]}pp)";
            if (useTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
            {
                formattedMove = $"{moveEmoji} {formattedMove}";
            }
            moveNames.Add($"{formattedMove}"); // Adding a zero-width space for formatting purposes if needed
        }

        string movesDisplay = string.Join("\n", moveNames);
        string abilityName = GameInfo.AbilityDataSource.FirstOrDefault(a => a.Value == pk.Ability)?.Text ?? "";
        string translatedAbility = AbilityTranslation.ContainsKey(abilityName) ? AbilityTranslation[abilityName] : abilityName;
        string natureName = GameInfo.NatureDataSource.FirstOrDefault(n => n.Value == (int)pk.Nature)?.Text ?? "";
        string teraTypeString;
        if (pk is PK9 pk9)
        {
            teraTypeString = pk9.TeraTypeOverride == (MoveType)99 ? "Stellar" : pk9.TeraType.ToString();
        }
        else
        {
            teraTypeString = ""; // or another default value as needed
        }

        var traduccionesNaturalezas = NatureTranslations.TraduccionesNaturalezas;

        int level = pk.CurrentLevel;
        string speciesName = GameInfo.GetStrings(1).Species[pk.Species];
        string traduccionNature = traduccionesNaturalezas.ContainsKey(natureName) ? traduccionesNaturalezas[natureName] : natureName;
        string alphaMarkSymbol = string.Empty;
        string mightyMarkSymbol = string.Empty;
        if (pk is IRibbonSetMark9 ribbonSetMark)
        {
            alphaMarkSymbol = ribbonSetMark.RibbonMarkAlpha ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.AlphaMarkEmoji.EmojiString : string.Empty;
            mightyMarkSymbol = ribbonSetMark.RibbonMarkMightiest ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MightiestMarkEmoji.EmojiString : string.Empty;
        }
        string alphaSymbol = (pk is IAlpha alpha && alpha.IsAlpha) ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.AlphaPLAEmoji.EmojiString : string.Empty;
        string shinySymbol = pk.ShinyXor == 0 ? "<:square:1134580807529398392> " : pk.IsShiny ? "<:shiny:1134580552926777385> " : string.Empty;
        string genderSymbol = GameInfo.GenderSymbolASCII[pk.Gender];
        string displayGender = genderSymbol switch
        {
            "M" => !string.IsNullOrEmpty(maleEmojiString) ? maleEmojiString : "(M) ",
            "F" => !string.IsNullOrEmpty(femaleEmojiString) ? femaleEmojiString : "(F) ",
            _ => ""
        };
        string mysteryGiftEmoji = pk.FatefulEncounter ? SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MysteryGiftEmoji.EmojiString : "";
        displayGender += alphaSymbol + mightyMarkSymbol + alphaMarkSymbol + mysteryGiftEmoji;
        string formName = ShowdownParsing.GetStringFromForm(pk.Form, strings, pk.Species, pk.Context);
        string speciesAndForm = $"**{shinySymbol}{speciesName}{(string.IsNullOrEmpty(formName) ? "" : $"-{formName}")} {displayGender}**";
        string heldItemName = strings.itemlist[pk.HeldItem];
        string ballName = strings.balllist[pk.Ball];


        string formDecoration = "";
        if (pk.Species == (int)Species.Alcremie && formArgument != 0)
        {
            formDecoration = $"{(AlcremieDecoration)formArgument}";
        }

        // Determine if this is a clone or dump request
        bool isCloneRequest = type == PokeRoutineType.Clone;
        bool isDumpRequest = type == PokeRoutineType.Dump;
        bool FixOT = type == PokeRoutineType.FixOT;
        bool isSpecialRequest = type == PokeRoutineType.SeedCheck;

        // Check if the Pokémon is shiny and prepend the shiny emoji
        string shinyEmoji = pk.IsShiny ? "✨ " : "";
        string pokemonDisplayName = pk.IsNicknamed ? pk.Nickname : GameInfo.GetStrings(1).Species[pk.Species];
        string tradeTitle;

        if (isMysteryEgg)
        {
            tradeTitle = "✨ Huevo Misterioso Shiny ✨ de";
        }
        else if (isBatchTrade)
        {
            tradeTitle = $"Comercio por lotes #{batchTradeNumber} - {shinyEmoji}{pokemonDisplayName} de";
        }
        else if (FixOT)
        {
            tradeTitle = $"Solicitud de FixOT de ";
        }
        else if (isSpecialRequest)
        {
            tradeTitle = $"Solicitud Especial de";
        }
        else if (isCloneRequest)
        {
            tradeTitle = "Capsula de Clonación activada para";
        }
        else if (isDumpRequest)
        {
            tradeTitle = "Solicitud de Dump de";
        }
        else
        {
            tradeTitle = $"";
        }

        // Get the Pokémon's image URL and dominant color
        (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(context, pk, isCloneRequest || isDumpRequest, formName, formArgument);

        // Adjust the image URL for dump request
        if (isMysteryEgg)
        {
            embedImageUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/mysteryegg2.png"; // URL for mystery egg
        }
        else if (isDumpRequest)
        {
            embedImageUrl = "https://i.imgur.com/9wfEHwZ.png"; // URL for dump request
        }
        else if (isCloneRequest)
        {
            embedImageUrl = "https://i.imgur.com/aSTCjUn.png"; // URL for clone request
        }
        else if (isSpecialRequest)
        {
            embedImageUrl = "https://i.imgur.com/EI1BHr5.png"; // URL for clone request
        }
        else if (FixOT)
        {
            embedImageUrl = "https://i.imgur.com/gRZGFIi.png"; // URL for fixot request
        }
        string heldItemUrl = string.Empty;

        if (!string.IsNullOrWhiteSpace(heldItemName))
        {
            // Convert to lowercase and remove spaces
            heldItemName = heldItemName.ToLower().Replace(" ", "");
            heldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
        }
        // Check if the image URL is a local file path
        bool isLocalFile = File.Exists(embedImageUrl);
        string userName = user.Username;
        string isPkmShiny = pk.IsShiny ? "✨" : "";
        // Build the embed with the author title image
        string authorName;

        // Determine the author's name based on trade type
        if (isMysteryEgg || FixOT || isCloneRequest || isDumpRequest || isSpecialRequest || isBatchTrade)
        {
            authorName = $"{tradeTitle} {userName}";
        }
        else // Normal trade
        {
            authorName = $"{isPkmShiny} {pokemonDisplayName} de {userName} ";
        }
        var embedBuilder = new EmbedBuilder()
            .WithColor(embedColor)
            .WithImageUrl(embedImageUrl)
            .WithFooter($"Posición actual: {position.Position}\n{etaMessage}")
            .WithAuthor(new EmbedAuthorBuilder()
                .WithName(authorName)
                .WithIconUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()));

        // Add the additional text at the top as its own field
        string additionalText = string.Join("\n", SysCordSettings.Settings.AdditionalEmbedText);
        if (!string.IsNullOrEmpty(additionalText))
        {
            embedBuilder.AddField("\u200B", additionalText, inline: false); // '\u200B' is a zero-width space to create an empty title
        }

        // Conditionally add the 'Trainer' and 'Moves' fields based on trade type
        if (!isMysteryEgg && !isCloneRequest && !isDumpRequest && !FixOT && !isSpecialRequest)
        {
            // Prepare the left side content
            string leftSideContent = $"**Entrenador**: {user.Mention}\n" +
                                     $"**Nivel**: {level}\n";

            // Add Tera Type information if the Pokémon is PK9 and the game version supports it
            if (pk is PK9 pk9Instance && (pk.Version == GameVersion.SL || pk.Version == GameVersion.VL))
            {
                var tera = pk9Instance.TeraType.ToString();
                var teraEmojis = TeraTypeDictionaries.TeraEmojis;
                var teraTranslations = TeraTypeDictionaries.TeraTranslations;

                // Check if the Tera Type has a corresponding emoji and translation
                if (teraEmojis.TryGetValue(tera, out string emojiID) && teraTranslations.TryGetValue(tera, out string teraEsp))
                {
                    leftSideContent += $"**Tera Tipo**: {emojiID} {teraEsp}\n";
                }
                else if (tera == "99") // Special case for Stellar
                {
                    leftSideContent += $"**Tera Tipo**: <:Stellar:1186199337177468929> Estelar\n";
                }
                else
                {
                    // If no corresponding emoji or translation found, just display the Tera Type in English
                    leftSideContent += $"**Tera Tipo**: {tera}\n";
                }
            }
            leftSideContent += $"**Habilidad**: {translatedAbility}\n";
            if (!(pk is PB7)) // Exclude scale for PB7 type
            {
                leftSideContent += $"{scale}\n";
            };
            leftSideContent += $"**Naturaleza**: {traduccionNature}\n" +
                               $"**IVs**: {ivsDisplay}\n";
            var evs = new List<string>();

            // Agregar los EVs no nulos al listado
            if (pk.EV_HP != 0)
                evs.Add($"{pk.EV_HP} HP");

            if (pk.EV_ATK != 0)
                evs.Add($"{pk.EV_ATK} Atk");

            if (pk.EV_DEF != 0)
                evs.Add($"{pk.EV_DEF} Def");

            if (pk.EV_SPA != 0)
                evs.Add($"{pk.EV_SPA} SpA");

            if (pk.EV_SPD != 0)
                evs.Add($"{pk.EV_SPD} SpD");

            if (pk.EV_SPE != 0)
                evs.Add($"{pk.EV_SPE} Spe");

            // Comprobar si hay EVs para agregarlos al mensaje
            if (evs.Any())
            {
                leftSideContent += "**EVs**: " + string.Join(" / ", evs) + "\n";
            }
            // Add the field to the embed
            embedBuilder.AddField($"{speciesAndForm}", leftSideContent, inline: true);
            // Add a blank field to align with the 'Trainer' field on the left
            embedBuilder.AddField("\u200B", "\u200B", inline: true); // First empty field for spacing
            // 'Moves' as another inline field, ensuring it's aligned with the content on the left
            embedBuilder.AddField("Movimientos", movesDisplay, inline: true);
        }
        else
        {
            // For special cases, add only the special description
            string specialDescription = $"**Entrenador**: {user.Mention}\n" +
                                        (isMysteryEgg ? "Huevo Misterioso" : isSpecialRequest ? "Solicitud Especial" : isCloneRequest ? "Solicitud de clonación" : FixOT ? "Solicitud de FixOT" : "Solicitud de Dump");
            embedBuilder.AddField("\u200B", specialDescription, inline: false);
        }

        // Set thumbnail images
        if (isCloneRequest || isSpecialRequest || isDumpRequest || FixOT)
        {
            embedBuilder.WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png");
        }
        else if (!string.IsNullOrEmpty(heldItemUrl))
        {
            embedBuilder.WithThumbnailUrl(heldItemUrl);
        }

        // If the image is a local file, set the image URL to the attachment reference
        if (isLocalFile)
        {
            embedBuilder.WithImageUrl($"attachment://{Path.GetFileName(embedImageUrl)}");
        }

        var embed = embedBuilder.Build();
        if (embed == null)
        {
            Console.WriteLine("Error: Embed is null.");
            await context.Channel.SendMessageAsync("Se produjo un error al preparar los detalles del trade.");
            return new TradeQueueResult(false);
        }

        if (isLocalFile)
        {
            // Send the message with the file and embed, referencing the file in the embed
            await context.Channel.SendFileAsync(embedImageUrl, embed: embed);

            if (isBatchTrade)
            {
                // Update the highest detail.ID for this user's batch trades
                if (!userBatchTradeMaxDetailId.ContainsKey(userID) || userBatchTradeMaxDetailId[userID] < detail.ID)
                {
                    userBatchTradeMaxDetailId[userID] = detail.ID;
                }

                // Schedule file deletion for batch trade
                await ScheduleFileDeletion(embedImageUrl, 0, detail.ID);

                // Check if this is the last trade in the batch for the user
                if (detail.ID == userBatchTradeMaxDetailId[userID] && batchTradeNumber == totalBatchTrades)
                {
                    DeleteBatchTradeFiles(detail.ID);
                }
            }
            else
            {
                // For non-batch trades, just schedule file deletion normally
                await ScheduleFileDeletion(embedImageUrl, 0);
            }
        }
        else
        {
            await context.Channel.SendMessageAsync(embed: embed);
        }

        return new TradeQueueResult(true);
    }

    private static int GenerateUniqueTradeID()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomValue = new Random().Next(1000);
        int uniqueTradeID = (int)(timestamp % int.MaxValue) * 1000 + randomValue;
        return uniqueTradeID;
    }

    private static string GetImageFolderPath()
    {
        // Get the base directory where the executable is located
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Define the path for the images subfolder
        string imagesFolder = Path.Combine(baseDirectory, "Images");

        // Check if the folder exists, if not, create it
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
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        return filePath;
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetails(SocketCommandContext context, T pk, bool isCloneRequest, string formName, int formArgument = 0)
    {
        string embedImageUrl;
        string speciesImageUrl;

        if (pk.IsEgg)
        {
            string eggImageUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/egg.png";
            speciesImageUrl = AbstractTrade<T>.PokeImg(pk, false, true);
            System.Drawing.Image combinedImage = await OverlaySpeciesOnEgg(eggImageUrl, speciesImageUrl);
            embedImageUrl = SaveImageLocally(combinedImage);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            speciesImageUrl = AbstractTrade<T>.PokeImg(pk, canGmax, false);
            embedImageUrl = speciesImageUrl;
        }

        // Determine ball image URL
        var strings = GameInfo.GetStrings(1);
        string ballName = strings.balllist[pk.Ball];

        // Check for "(LA)" in the ball name
        if (ballName.Contains("(LA)"))
        {
            ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
        }
        else
        {
            ballName = ballName.Replace(" ", "").ToLower();
        }

        string ballImgUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/AltBallImg/28x28/{ballName}.png";

        // Check if embedImageUrl is a local file or a web URL
        if (Uri.TryCreate(embedImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
            // Load local image directly
            using (var localImage = System.Drawing.Image.FromFile(uri.LocalPath))
            using (var ballImage = await LoadImageFromUrl(ballImgUrl))
            {
                if (ballImage != null)
                {
                    using (var graphics = Graphics.FromImage(localImage))
                    {
                        var ballPosition = new Point(localImage.Width - ballImage.Width, localImage.Height - ballImage.Height);
                        graphics.DrawImage(ballImage, ballPosition);
                    }
                    embedImageUrl = SaveImageLocally(localImage);
                }
            }
        }
        else
        {
            // Load web image and overlay ball
            (System.Drawing.Image finalCombinedImage, bool ballImageLoaded) = await OverlayBallOnSpecies(speciesImageUrl, ballImgUrl);
            embedImageUrl = SaveImageLocally(finalCombinedImage);

            if (!ballImageLoaded)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImgUrl}");
               // await context.Channel.SendMessageAsync($"Ball image could not be loaded: {ballImgUrl}");
            }
        }

        (int R, int G, int B) = await GetDominantColorAsync(embedImageUrl);
        return (embedImageUrl, new DiscordColor(R, G, B));
    }

    private static async Task<(System.Drawing.Image, bool)> OverlayBallOnSpecies(string speciesImageUrl, string ballImageUrl)
    {
        using (var speciesImage = await LoadImageFromUrl(speciesImageUrl))
        {
            if (speciesImage == null)
            {
                Console.WriteLine("Species image could not be loaded.");
                return (null, false);
            }

            var ballImage = await LoadImageFromUrl(ballImageUrl);
            if (ballImage == null)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImageUrl}");
                return ((System.Drawing.Image)speciesImage.Clone(), false); // Return false indicating failure
            }

            using (ballImage)
            {
                using (var graphics = Graphics.FromImage(speciesImage))
                {
                    var ballPosition = new Point(speciesImage.Width - ballImage.Width, speciesImage.Height - ballImage.Height);
                    graphics.DrawImage(ballImage, ballPosition);
                }

                return ((System.Drawing.Image)speciesImage.Clone(), true); // Return true indicating success
            }
        }
    }
    private static async Task<System.Drawing.Image> OverlaySpeciesOnEgg(string eggImageUrl, string speciesImageUrl)
    {
        // Load both images
        System.Drawing.Image eggImage = await LoadImageFromUrl(eggImageUrl);
        System.Drawing.Image speciesImage = await LoadImageFromUrl(speciesImageUrl);

        // Calculate the ratio to scale the species image to fit within the egg image size
        double scaleRatio = Math.Min((double)eggImage.Width / speciesImage.Width, (double)eggImage.Height / speciesImage.Height);

        // Create a new size for the species image, ensuring it does not exceed the egg dimensions
        Size newSize = new Size((int)(speciesImage.Width * scaleRatio), (int)(speciesImage.Height * scaleRatio));

        // Resize species image
        System.Drawing.Image resizedSpeciesImage = new Bitmap(speciesImage, newSize);

        // Create a graphics object for the egg image
        using (Graphics g = Graphics.FromImage(eggImage))
        {
            // Calculate the position to center the species image on the egg image
            int speciesX = (eggImage.Width - resizedSpeciesImage.Width) / 2;
            int speciesY = (eggImage.Height - resizedSpeciesImage.Height) / 2;

            // Draw the resized and centered species image over the egg image
            g.DrawImage(resizedSpeciesImage, speciesX, speciesY, resizedSpeciesImage.Width, resizedSpeciesImage.Height);
        }

        // Dispose of the species image and the resized species image if they're no longer needed
        speciesImage.Dispose();
        resizedSpeciesImage.Dispose();

        // Calculate scale factor for resizing while maintaining aspect ratio
        double scale = Math.Min(128.0 / eggImage.Width, 128.0 / eggImage.Height);

        // Calculate new dimensions
        int newWidth = (int)(eggImage.Width * scale);
        int newHeight = (int)(eggImage.Height * scale);

        // Create a new 128x128 bitmap
        Bitmap finalImage = new Bitmap(128, 128);

        // Draw the resized egg image onto the new bitmap, centered
        using (Graphics g = Graphics.FromImage(finalImage))
        {
            // Calculate centering position
            int x = (128 - newWidth) / 2;
            int y = (128 - newHeight) / 2;

            // Draw the image
            g.DrawImage(eggImage, x, y, newWidth, newHeight);
        }

        // Dispose of the original egg image if it's no longer needed
        eggImage.Dispose();

        // The finalImage now contains the overlay, is resized, and maintains aspect ratio
        return finalImage;
    }

    private static async Task<System.Drawing.Image> LoadImageFromUrl(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to load image from {url}. Status code: {response.StatusCode}");
                return null;
            }

            Stream stream = await response.Content.ReadAsStreamAsync();
            if (stream == null || stream.Length == 0)
            {
                Console.WriteLine($"No data or empty stream received from {url}");
                return null;
            }

            try
            {
                return System.Drawing.Image.FromStream(stream);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Failed to create image from stream. URL: {url}, Exception: {ex}");
                return null;
            }
        }
    }

    private static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds, int batchTradeId = -1)
    {
        if (batchTradeId != -1)
        {
            // If this is part of a batch trade, add the file path to the dictionary
            if (!batchTradeFiles.ContainsKey(batchTradeId))
            {
                batchTradeFiles[batchTradeId] = [];
            }

            batchTradeFiles[batchTradeId].Add(filePath);
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
                Console.WriteLine($"Error deleting file: {ex.Message}");
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
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixelColor = image.GetPixel(x, y);

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

            image.Dispose();

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"Error processing image from {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            using var stream = await response.Content.ReadAsStreamAsync();
            return new Bitmap(stream);
        }
        else
        {
            return new Bitmap(imagePath);
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
            System.Drawing.Image png = pk.Sprite();
            var destRect = new Rectangle(-40, -65, 137, 130);
            var destImage = new Bitmap(137, 130);

            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);

            }
            png = destImage;
            spritearray.Add(png);
            codecount++;
        }
        int outputImageWidth = spritearray[0].Width + 20;

        int outputImageHeight = spritearray[0].Height - 65;

        Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
        }
        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png";
        finalembedpic.Save(filename);
        filename = System.IO.Path.GetFileName($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }
}
