using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class MysteryEggModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("mysteryegg")]
        [Alias("me")]
        [Summary("Trades a random mystery egg with perfect stats and shiny appearance.")]
        public async Task TradeMysteryEggAsync()
        {
            // Check if the user is already in the queue
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
            var code = Info.GetRandomTradeCode(userID);
            await TradeMysteryEggAsync(code).ConfigureAwait(false);
        }

        [Command("mysteryegg")]
        [Alias("me")]
        [Summary("Trades a random mystery egg with perfect stats and shiny appearance.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeMysteryEggAsync([Summary("Trade Code")] int code)
        {
            // Check if the user is already in the queue
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
                bool validPokemon = false;
                int attempts = 0;
                const int maxAttempts = 15;

                while (!validPokemon && attempts < maxAttempts)
                {
                    attempts++;

                    var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                    var gameVersion = MysteryEggModule<T>.GetGameVersion();
                    var speciesList = GetBreedableSpecies(gameVersion, "en");

                    var randomIndex = new Random().Next(speciesList.Count);
                    ushort speciesId = speciesList[randomIndex];

                    var context = new EntityContext();
                    var IsEgg = new EncounterEgg(speciesId, 0, 1, 9, gameVersion, context);
                    var pk = IsEgg.ConvertToPKM(sav);

                    SetPerfectIVsAndShiny(pk);

                    pk = EntityConverter.ConvertToType(pk, typeof(T), out _) ?? pk;

                    if (pk is not T pkT)
                    {
                        await ReplyAsync($"<a:warning:1206483664939126795> Oops! {Context.User.Mention}, no pude crear el huevo misterioso, inténtelo mas tarde.").ConfigureAwait(false);
                        return;
                    }

                    AbstractTrade<T>.EggTrade(pkT, null);

                    var sig = Context.User.GetFavor();
                    validPokemon = await AddTradeToQueueAsync(code, Context.User.Username, pkT, sig, Context.User, isMysteryEgg: true).ConfigureAwait(false);
                }

                if (!validPokemon)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> Oops! {Context.User.Mention}, nuestra canasta no tiene huevos misteriosos en este momento, inténtelo mas tarde.").ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(MysteryEggModule<T>));
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, se produjo un error al procesar la solicitud.").ConfigureAwait(false);
            }
        }

        private static GameVersion GetGameVersion()
        {
            if (typeof(T) == typeof(PK8))
                return GameVersion.SWSH;
            else if (typeof(T) == typeof(PB8))
                return GameVersion.BDSP;
            else if (typeof(T) == typeof(PA8))
                return GameVersion.PLA;
            else if (typeof(T) == typeof(PK9))
                return GameVersion.SV;
            else
                throw new ArgumentException("Versión del juego no compatible.");
        }

        private static void SetPerfectIVsAndShiny(PKM pk)
        {
            // Set IVs to perfect
            pk.IVs = new[] { 31, 31, 31, 31, 31, 31 };
            // Set as shiny
            pk.SetShiny();
            // Set hidden ability
            pk.RefreshAbility(2);
        }

        public static List<ushort> GetBreedableSpecies(GameVersion gameVersion, string language = "en")
        {
            var gameStrings = GameInfo.GetStrings(language);
            var availableSpeciesList = gameStrings.specieslist
                .Select((name, index) => (Name: name, Index: index))
                .Where(item => item.Name != string.Empty)
                .ToList();

            var breedableSpecies = new List<ushort>();
            var pt = GetPersonalTable(gameVersion);
            foreach (var species in availableSpeciesList)
            {
                var speciesId = (ushort)species.Index;
                var pi = GetFormEntry(pt, speciesId, 0);
                if (IsBreedable(pi) && pi.EvoStage == 1)
                {
                    breedableSpecies.Add(speciesId);
                }
            }

            return breedableSpecies;
        }

        private static bool IsBreedable(PersonalInfo pi)
        {
            return pi.EggGroup1 != 0 || pi.EggGroup2 != 0;
        }

        private static PersonalInfo GetFormEntry(object personalTable, ushort species, byte form)
        {
            return personalTable switch
            {
                PersonalTable9SV pt => pt.GetFormEntry(species, form),
                PersonalTable8SWSH pt => pt.GetFormEntry(species, form),
                PersonalTable8LA pt => pt.GetFormEntry(species, form),
                PersonalTable8BDSP pt => pt.GetFormEntry(species, form),
                _ => throw new ArgumentException("Tipo de tabla personal no admitido."),
            };
        }

        private static object GetPersonalTable(GameVersion gameVersion)
        {
            return gameVersion switch
            {
                GameVersion.SWSH => PersonalTable.SWSH,
                GameVersion.BDSP => PersonalTable.BDSP,
                GameVersion.PLA => PersonalTable.LA,
                GameVersion.SV => PersonalTable.SV,
                _ => throw new ArgumentException("Versión del juego no compatible."),
            };
        }

        private async Task<bool> AddTradeToQueueAsync(int code, string trainerName, T? pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isMysteryEgg = false, List<Pictocodes> lgcode = null, PokeTradeType tradeType = PokeTradeType.Specific, bool ignoreAutoOT = false, bool isHiddenTrade = false)
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
                return false;
            }
            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                string legalityReport = la.Report(verbose: false);
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
                var embedBuilder = new EmbedBuilder(); // Crear el objeto EmbedBuilder
                embedBuilder.WithColor(Color.Red); // Opcional: establecer el color del embed

                if (pk.IsEgg)
                {
                    string speciesName = GameInfo.GetStrings("en").specieslist[pk.Species];
                    embedBuilder.WithAuthor("Conjunto de showdown no válido!", customIconUrl);
                    embedBuilder.WithDescription($"<a:no:1206485104424128593> {usr.Mention} El conjunto de showdown __no es válido__ para un huevo de **{speciesName}**.");
                    embedBuilder.AddField("__**Error**__", $"Puede que __**{speciesName}**__ no se pueda obtener en un huevo o algún dato esté impidiendo el trade.", inline: false);
                    embedBuilder.AddField("__**Solución**__", $"No necesitas hacer nada, el bot intentará generar un huevo misterioso de otro Pokémon constantemente hasta lograrlo.", inline: false);
                }
                else
                {
                    embedBuilder.WithAuthor("Archivo adjunto no valido!", customIconUrl);
                    embedBuilder.WithDescription($"<a:no:1206485104424128593> {usr.Mention} el archivo **{typeof(T).Name}** no es __legal__ y no puede ser tradeado.\n### He aquí la razón:\n```{legalityReport}```\n```🔊Consejo:\n• Por favor verifica detenidamente la informacion en PKHeX e intentalo de nuevo!\n• Puedes utilizar el plugin de ALM para legalizar tus pokemons y ahorrarte estos problemas.```");
                }
                embedBuilder.WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png");
                embedBuilder.WithImageUrl("https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif");
                // Añadir el footer con icono y texto
                embedBuilder.WithFooter(footer => {
                    footer.WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());
                    footer.WithText($"{Context.User.Username} | {DateTimeOffset.Now.ToString("hh:mm tt")}");
                });

                var reply = await ReplyAsync(embed: embedBuilder.Build()).ConfigureAwait(false); // Enviar el embed
                await Task.Delay(6000);
                await reply.DeleteAsync().ConfigureAwait(false);
                return false;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, tradeType, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, isMysteryEgg, lgcode, ignoreAutoOT, isHiddenTrade).ConfigureAwait(false);
            return true;
        }

        private static List<Pictocodes> GenerateRandomPictocodes(int count)
        {
            Random rnd = new();
            List<Pictocodes> randomPictocodes = new List<Pictocodes>();
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
