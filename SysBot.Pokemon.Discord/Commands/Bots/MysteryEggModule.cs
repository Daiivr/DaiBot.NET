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
        [Summary("Intercambia un huevo generado a partir de un Pokémon aleatorio.")]
        public async Task TradeMysteryEggAsync()
        {
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

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessMysteryEggTradeAsync(code).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogUtil.LogSafe(ex, nameof(MysteryEggModule<T>));
                    await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, se produjo un error al procesar la solicitud.").ConfigureAwait(false);
                }
            });
        }

        public static T? GenerateLegalMysteryEgg(int maxAttempts = 5)
        {
            var gameVersion = GetGameVersion();
            var speciesList = GetBreedableSpecies(gameVersion, "en");
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                var randomIndex = new Random().Next(speciesList.Count);
                ushort speciesId = speciesList[randomIndex];
                var speciesName = GameInfo.GetStrings("en").specieslist[speciesId];
                var showdownSet = new ShowdownSet(speciesName);
                var template = AutoLegalityWrapper.GetTemplate(showdownSet);
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pk = sav.GetLegal(template, out _);
                if (pk != null)
                {
                    pk = EntityConverter.ConvertToType(pk, typeof(T), out _) ?? pk;
                    if (pk is T validPk)
                    {
                        var la = new LegalityAnalysis(validPk);
                        if (la.Valid)
                        {
                            TradeExtensions<T>.EggTrade(validPk, template);
                            SetHaX(validPk);
                            la = new LegalityAnalysis(validPk);
                            if (!la.Valid)
                            {
                                attempts++;
                                continue;
                            }

                            return validPk;
                        }
                    }
                }
                attempts++;
            }
            return null;
        }
        private async Task ProcessMysteryEggTradeAsync(int code)
        {
            var mysteryEgg = GenerateLegalMysteryEgg(5);
            if (mysteryEgg == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, no se pudo generar un huevo misterioso legal después de 5 intentos. Por favor, inténtelo de nuevo más tarde.").ConfigureAwait(false);
                return;
            }
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, mysteryEgg, sig, Context.User, isMysteryEgg: true).ConfigureAwait(false);
            if (Context.Message is IUserMessage userMessage)
            {
                await DeleteMessageAfterDelay(userMessage, 2000).ConfigureAwait(false);
            }
        }

        private static async Task DeleteMessageAfterDelay(IUserMessage message, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds).ConfigureAwait(false);
            await message.DeleteAsync().ConfigureAwait(false);
        }

        public static void SetHaX(PKM pk)
        {
            pk.IVs = new[] { 31, 31, 31, 31, 31, 31 };
            pk.SetShiny();
            pk.RefreshAbility(2);
            pk.MaximizeFriendship();
            pk.RefreshChecksum();
        }

        public static GameVersion GetGameVersion()
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

        public static List<ushort> GetBreedableSpecies(GameVersion gameVersion, string language = "en")
        {
            var gameStrings = GameInfo.GetStrings(language);
            var availableSpeciesList = gameStrings.specieslist
                .Select((name, index) => (Name: name, Index: index))
                .Where(item => !string.IsNullOrEmpty(item.Name))
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
                _ => throw new ArgumentException("Tipo de tabla personal no compatible."),
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

        private async Task AddTradeToQueueAsync(
           int code,
           string trainerName,
           T pk,
           RequestSignificance sig,
           SocketUser usr,
           bool isBatchTrade = false,
           int batchTradeNumber = 1,
           int totalBatchTrades = 1,
           bool isHiddenTrade = false,
           bool isMysteryEgg = false,
           List<Pictocodes>? lgcode = null,
           PokeTradeType tradeType = PokeTradeType.Specific,
           bool ignoreAutoOT = false)
        {
            lgcode ??= GenerateRandomPictocodes(3);
            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                string responseMessage = "⚠️ Se ha producido un error inesperado. Por favor, inténtelo de nuevo.";
                var reply = await ReplyAsync(responseMessage).ConfigureAwait(false);
                await Task.Delay(6000).ConfigureAwait(false);
                await reply.DeleteAsync().ConfigureAwait(false);
                return;
            }

            await QueueHelper<T>.AddToQueueAsync(
                Context,
                code,
                trainerName,
                sig,
                pk,
                PokeRoutineType.LinkTrade,
                tradeType,
                usr,
                isBatchTrade,
                batchTradeNumber,
                totalBatchTrades,
                isHiddenTrade,
                false,
                isMysteryEgg,
                lgcode: lgcode,
                ignoreAutoOT: ignoreAutoOT).ConfigureAwait(false);
        }

        private static List<Pictocodes> GenerateRandomPictocodes(int count)
        {
            Random rnd = new();
            List<Pictocodes> randomPictocodes = new();
            Array pictocodeValues = Enum.GetValues(typeof(Pictocodes));

            for (int i = 0; i < count; i++)
            {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                Pictocodes randomPictocode = (Pictocodes)pictocodeValues.GetValue(rnd.Next(pictocodeValues.Length));
#pragma warning restore CS8605 // Unboxing a possibly null value.
                randomPictocodes.Add(randomPictocode);
            }

            return randomPictocodes;
        }
    }
}
