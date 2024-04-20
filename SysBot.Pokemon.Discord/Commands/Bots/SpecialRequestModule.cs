using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord
{
    /// <summary>
    /// Provides functionality for listing and requesting Pokémon wondercard events via Discord commands.
    /// Users can interact with the system in multiple ways:
    /// 
    /// 1. Listing Events: 
    ///    - Users can list events from a specified generation or game. Optionally, users can filter this list by specifying a Pokémon species name.
    ///    - Command format: .srp {generationOrGame} [speciesName] [pageX]
    ///    - Example: .srp gen9 Mew page2
    ///      This command lists the second page of events for the 'gen9' dataset, specifically filtering for events related to 'Mew'.
    ///
    /// 2. Requesting Specific Events:
    ///    - Users can request a specific event to be processed by providing an event index number.
    ///    - Command format: .srp {generationOrGame} {eventIndex}
    ///    - Example: .srp gen9 26
    ///      This command requests the processing of the event at index 26 within the 'gen9' dataset.
    ///
    /// 3. Pagination:
    ///    - Users can navigate through pages of event listings by specifying the page number after the generation/game and optionally the species.
    ///    - Command format: .srp {generationOrGame} [speciesName] pageX
    ///    - Example: .srp gen9 page3
    ///      This command lists the third page of events for the 'gen9' dataset.
    ///
    /// This module ensures that user inputs are properly validated for the specific commands to manage event data effectively, adjusting listings or processing requests based on user interactions.
    /// </summary>
    public class SpecialRequestModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private const int itemsPerPage = 25;
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        [Command("specialrequestpokemon")]
        [Alias("srp")]
        [Summary("Lists available wondercard events from the specified generation or game or requests a specific event if a number is provided.")]
        public async Task ListSpecialEventsAsync(string generationOrGame, [Remainder] string args = "")
        {
            var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && int.TryParse(parts[0], out int index))
            {
                await SpecialEventRequestAsync(generationOrGame, index.ToString());
                return;
            }

            int page = 1;
            string speciesName = "";

            foreach (string part in parts)
            {
                if (part.StartsWith("page", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.Substring(4), out int pageNumber))
                {
                    page = pageNumber;
                    continue;
                }
                speciesName = part;
            }

            var eventData = GetEventData(generationOrGame);
            if (eventData == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Generación o juego no válido: {generationOrGame}");
                return;
            }

            var allEvents = GetFilteredEvents(eventData, speciesName);
            if (!allEvents.Any())
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se han encontrado eventos para {generationOrGame} con el filtro especificado.");
                return;
            }

            var pageCount = (int)Math.Ceiling((double)allEvents.Count() / itemsPerPage);
            page = Math.Clamp(page, 1, pageCount);
            var embed = BuildEventListEmbed(generationOrGame, allEvents, page, pageCount, botPrefix);
            await SendEventListAsync(embed);
            await CleanupMessagesAsync();
        }

        [Command("specialrequestpokemon")]
        [Alias("srp")]
        [Summary("Downloads wondercard event attachments from the specified generation and adds to trade queue.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task SpecialEventRequestAsync(string generationOrGame, [Remainder] string args = "")
        {
            if (!int.TryParse(args, out int index))
            {
                await ReplyAsync("<a:warning:1206483664939126795> Índice de eventos no válido. Utilice un número de evento válido del comando");
                return;
            }

            var userID = Context.User.Id;
            if (Info.IsUserInQueue(userID))
            {
                var currentTime = DateTime.UtcNow;
                var formattedTime = currentTime.ToString("hh:mm tt");

                var queueEmbed = new EmbedBuilder
                {
                    Color = Color.Red,
                    ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                    ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                };

                queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif");

                // Añadir un field al Embed para indicar el error
                queueEmbed.AddField("__**Error**__:", $"<a:no:1206485104424128593> {Context.User.Mention} No pude agregarte a la cola", true);
                queueEmbed.AddField("__**Razón**__:", "No puedes agregar más operaciones hasta que la actual se procese.", true);
                queueEmbed.AddField("__**Solución**__:", "Espera un poco hasta que la operación existente se termine e intentalo de nuevo.");

                queueEmbed.Footer = new EmbedFooterBuilder
                {
                    Text = $"{Context.User.Username} • {formattedTime}",
                    IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                };

                await ReplyAsync(embed: queueEmbed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                var eventData = GetEventData(generationOrGame);
                if (eventData == null)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> Generación o juego no válido: {generationOrGame}");
                    return;
                }

                var entityEvents = eventData.Where(gift => gift.IsEntity && !gift.IsItem).ToArray();
                if (index < 1 || index > entityEvents.Length)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> Índice de eventos no válido. Utilice un número de evento válido del comando `{SysCord<T>.Runner.Config.Discord.CommandPrefix}srp {generationOrGame}` command.");
                    return;
                }

                var selectedEvent = entityEvents[index - 1];
                var pk = ConvertEventToPKM(selectedEvent);
                if (pk == null)
                {
                    await ReplyAsync("<a:warning:1206483664939126795> Los datos de Wondercard proporcionados no son compatibles con este módulo!");
                    return;
                }

                var code = Info.GetRandomTradeCode(userID);
                var lgcode = Info.GetRandomLGTradeCode();
                var sig = Context.User.GetFavor();
                await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention} Solicitud de evento especial agregada a la cola.");
                await AddTradeToQueueAsync(code, Context.User.Username, pk as T, sig, Context.User, lgcode: lgcode);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"<a:Error:1223766391958671454> Ocurrió un error: {ex.Message}");
            }
            finally
            {
                await CleanupUserMessageAsync();
            }
        }

        private static int GetPageNumber(string[] parts)
        {
            var pagePart = parts.FirstOrDefault(p => p.StartsWith("page", StringComparison.OrdinalIgnoreCase));
            if (pagePart != null && int.TryParse(pagePart.AsSpan(4), out int pageNumber))
                return pageNumber;

            if (parts.Length > 0 && int.TryParse(parts.Last(), out int parsedPage))
                return parsedPage;

            return 1;
        }

        private static MysteryGift[]? GetEventData(string generationOrGame)
        {
            return generationOrGame.ToLowerInvariant() switch
            {
                "3" or "gen3" => EncounterEvent.MGDB_G3,
                "4" or "gen4" => EncounterEvent.MGDB_G4,
                "5" or "gen5" => EncounterEvent.MGDB_G5,
                "6" or "gen6" => EncounterEvent.MGDB_G6,
                "7" or "gen7" => EncounterEvent.MGDB_G7,
                "gg" or "lgpe" => EncounterEvent.MGDB_G7GG,
                "swsh" => EncounterEvent.MGDB_G8,
                "pla" or "la" => EncounterEvent.MGDB_G8A,
                "bdsp" => EncounterEvent.MGDB_G8B,
                "9" or "gen9" => EncounterEvent.MGDB_G9,
                _ => null,
            };
        }

        private static IOrderedEnumerable<(int Index, string EventInfo)> GetFilteredEvents(MysteryGift[] eventData, string speciesName = "")
        {
            return eventData
                .Where(gift =>
                    gift.IsEntity &&
                    !gift.IsItem &&
                    (string.IsNullOrWhiteSpace(speciesName) || GameInfo.Strings.Species[gift.Species].Equals(speciesName, StringComparison.OrdinalIgnoreCase))
                )
                .Select((gift, index) =>
                {
                    string species = GameInfo.Strings.Species[gift.Species];
                    string levelInfo = $"{gift.Level}"; 
                    string formName = ShowdownParsing.GetStringFromForm(gift.Form, GameInfo.Strings, gift.Species, gift.Context);
                    formName = !string.IsNullOrEmpty(formName) ? $"-{formName}" : "";
                    string trainerName = gift.OriginalTrainerName;
                    string eventDetails = $"{gift.CardHeader} - {species}{formName} | Lvl.{levelInfo} | OT: {trainerName}";
                    return (Index: index + 1, EventInfo: eventDetails);
                })
                .OrderBy(x => x.Index);
        }

        private static EmbedBuilder BuildEventListEmbed(string generationOrGame, IOrderedEnumerable<(int Index, string EventInfo)> allEvents, int page, int pageCount, string botPrefix)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"📝 Eventos Disponibles - {generationOrGame.ToUpperInvariant()}")
                .WithDescription($"Pagina {page} de {pageCount}")
                .WithColor(DiscordColor.Blue);

            var pageItems = allEvents.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);
            foreach (var item in pageItems)
            {
                embed.AddField($"{item.Index}. {item.EventInfo}", $"Usa `{botPrefix}srp {generationOrGame} {item.Index}` en el canal correspondiente para solicitar este evento.");
            }

            return embed;
        }

        private async Task SendEventListAsync(EmbedBuilder embed)
        {
            if (Context.User is not IUser user)
            {
                await ReplyAsync("<a:Error:1223766391958671454> **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de Privacidad del Servidor**.");
                return;
            }

            try
            {
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed.Build());
                await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, Te envié un DM con la lista de eventos.");
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
            }
        }

        private async Task CleanupMessagesAsync()
        {
            await Task.Delay(10_000);
            await CleanupUserMessageAsync();
        }

        private async Task CleanupUserMessageAsync()
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);
        }

        private static PKM? ConvertEventToPKM(MysteryGift selectedEvent)
        {
            var download = new Download<PKM>
            {
                Data = selectedEvent.ConvertToPKM(new SimpleTrainerInfo(), EncounterCriteria.Unrestricted),
                Success = true
            };

            return download.Data is null ? null : GetRequest(download);
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, T? pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isMysteryEgg = false, List<Pictocodes> lgcode = null, PokeTradeType tradeType = PokeTradeType.Specific, bool ignoreAutoOT = false, bool isHiddenTrade = false)
        {
            lgcode ??= GenerateRandomPictocodes(3);
            if (!pk.CanBeTraded())
            {
                var errorMessage = $"<a:no:1206485104424128593> {usr.Mention} revisa el conjunto enviado, algun dato esta bloqueando el intercambio.\n\n```📝Soluciones:\n• Revisa detenidamente cada detalle del conjunto y vuelve a intentarlo!```";
                var errorEmbed = new EmbedBuilder
                {
                    Description = errorMessage,
                    Color = Color.Red,
                    ImageUrl = "https://media.tenor.com/vjgjHDFwyOgAAAAM/pysduck-confused.gif",
                    ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                };

                errorEmbed.WithAuthor("Error al crear conjunto!", "https://img.freepik.com/free-icon/warning_318-478601.jpg")
                     .WithFooter(footer =>
                     {
                         footer.Text = $"{Context.User.Username} • {DateTime.UtcNow.ToString("hh:mm tt")}";
                         footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                     });

                var reply = await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                await Task.Delay(6000); // Delay for 6 seconds
                await reply.DeleteAsync().ConfigureAwait(false);
                return;
            }
            var homeLegalityCfg = Info.Hub.Config.Trade.HomeLegalitySettings;
            var la = new LegalityAnalysis(pk);
            if(!la.Valid)
        {
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
                var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
                var customthumbnail = "https://i.imgur.com/DWLEXyu.png";
                string legalityReport = la.Report(verbose: false);

                string responseMessage = pk.IsEgg ? $"<a:no:1206485104424128593> {usr.Mention} El conjunto de showdown __no es válido__ para este **huevo**. Por favor revisa tu __información__ y vuelve a intentarlo." :
                    $"<a:no:1206485104424128593> {usr.Mention} el archivo **{typeof(T).Name}** no es __legal__ y no puede ser tradeado.\n### He aquí la razón:\n```{legalityReport}```\n```🔊Consejo:\n• Por favor verifica detenidamente la informacion en PKHeX e intentalo de nuevo!\n• Puedes utilizar el plugin de ALM para legalizar tus pokemons y ahorrarte estos problemas.```";
                var embedResponse = new EmbedBuilder()
                    .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                    .WithDescription(responseMessage)
                    .WithColor(Color.Red)
                    .WithImageUrl(customImageUrl)
                        .WithThumbnailUrl(customthumbnail);

                // Adding footer with user avatar, username, and current time in 12-hour format
                var footerBuilder1 = new EmbedFooterBuilder()
                    .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithText($"{Context.User.Username} | {DateTimeOffset.Now.ToString("hh:mm tt")}"); // "hh:mm tt" formats time in 12-hour format with AM/PM

                var embed2 = embedResponse.Build();

                var reply = await ReplyAsync(embed: embed2).ConfigureAwait(false);
                await Task.Delay(10000);
                await reply.DeleteAsync().ConfigureAwait(false);
                return;
            }
            if (homeLegalityCfg.DisallowNonNatives && (la.EncounterOriginal.Context != pk.Context || pk.GO))
            {
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
                var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
                var customthumbnail = "https://i.imgur.com/DWLEXyu.png";
                // Allow the owner to prevent trading entities that require a HOME Tracker even if the file has one already.
                var embedBuilder = new EmbedBuilder()
                    .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                    .WithDescription($"<a:no:1206485104424128593> {usr.Mention}, este archivo Pokemon **{typeof(T).Name}** no cuenta con un **HOME TRACKER** y no puede ser tradeado!")
                    .WithColor(Color.Red)
                    .WithImageUrl(customImageUrl)
                    .WithThumbnailUrl(customthumbnail);

                // Adding footer with user avatar, username, and current time in 12-hour format
                var footerBuilder = new EmbedFooterBuilder()
                    .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithText($"{Context.User.Username} | {DateTimeOffset.Now.ToString("hh:mm tt")}"); // "hh:mm tt" formats time in 12-hour format with AM/PM

                embedBuilder.WithFooter(footerBuilder);

                var embed = embedBuilder.Build();

                var reply2 = await ReplyAsync(embed: embed).ConfigureAwait(false);
                await Task.Delay(10000); // Delay for 20 seconds
                await reply2.DeleteAsync().ConfigureAwait(false);
                return;
            }
            if (homeLegalityCfg.DisallowTracked && pk is IHomeTrack { HasTracker: true })
            {
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
                var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
                var customthumbnail = "https://i.imgur.com/DWLEXyu.png";
                // Allow the owner to prevent trading entities that already have a HOME Tracker.
                var embedBuilder = new EmbedBuilder()
                    .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                    .WithDescription($"<a:no:1206485104424128593> {usr.Mention}, este archivo Pokemon **{typeof(T).Name}** ya tiene un **HOME TRACKER** y no puede ser tradeado!")
                    .WithColor(Color.Red)
                    .WithImageUrl(customImageUrl)
                    .WithThumbnailUrl(customthumbnail);

                // Adding footer with user avatar, username, and current time in 12-hour format
                var footerBuilder = new EmbedFooterBuilder()
                    .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithText($"{Context.User.Username} | {DateTimeOffset.Now.ToString("hh:mm tt")}"); // "hh:mm tt" formats time in 12-hour format with AM/PM

                var embed1 = embedBuilder.Build();

                var reply1 = await ReplyAsync(embed: embed1).ConfigureAwait(false);
                await Task.Delay(10000); // Delay for 20 seconds
                await reply1.DeleteAsync().ConfigureAwait(false);
                return;
            }
            // handle past gen file requests
            // thanks manu https://github.com/Manu098vm/SysBot.NET/commit/d8c4b65b94f0300096704390cce998940413cc0d
            if (!la.Valid && la.Results.Any(m => m.Identifier is CheckIdentifier.Memory))
            {
                var clone = (T)pk.Clone();

                clone.HandlingTrainerName = pk.OriginalTrainerName;
                clone.HandlingTrainerGender = pk.OriginalTrainerGender;

                if (clone is PK8 or PA8 or PB8 or PK9)
                    ((dynamic)clone).HandlingTrainerLanguage = (byte)pk.Language;

                clone.CurrentHandler = 1;

                la = new LegalityAnalysis(clone);

                if (la.Valid) pk = clone;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, tradeType, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, isMysteryEgg, lgcode, ignoreAutoOT, isHiddenTrade).ConfigureAwait(false);
        }

        private static List<Pictocodes> GenerateRandomPictocodes(int count)
        {
            Random rnd = new();
            List<Pictocodes> randomPictocodes = [];
            Array pictocodeValues = Enum.GetValues(typeof(Pictocodes));

            for (int i = 0; i < count; i++)
            {
                Pictocodes randomPictocode = (Pictocodes)pictocodeValues.GetValue(rnd.Next(pictocodeValues.Length));
                randomPictocodes.Add(randomPictocode);
            }

            return randomPictocodes;
        }
    }
}

