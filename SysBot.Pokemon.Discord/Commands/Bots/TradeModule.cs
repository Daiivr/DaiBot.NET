using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static SysBot.Pokemon.TradeSettings.TradeSettingsCategory;

namespace SysBot.Pokemon.Discord;

[Summary("Pone en cola nuevos intercambios de códigos de enlace")]
public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    private static readonly char[] separator = [' '];

    private static readonly char[] separatorArray = [' '];

    private static readonly char[] separatorArray0 = [' '];

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Corrige el OT y el apodo de un Pokémon que muestras a través de Link Trade si se detecta un anuncio.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT()
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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
        var trainerName = Context.User.Username;
        var lgcode = Info.GetRandomLGTradeCode();
        var sig = Context.User.GetFavor();

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT, Context.User, false, 1, 1, false, false, false, lgcode).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Corrige el OT y el apodo de un Pokémon que muestras a través de Link Trade si se detecta un anuncio.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT([Summary("Trade Code")] int code)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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

        var trainerName = Context.User.Username;
        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT, Context.User, false, 1, 1, false, false, false, lgcode).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("fixOTList")]
    [Alias("fl", "fq")]
    [Summary("Muestra los usuarios en la cola Fix OT.")]
    [RequireSudo]
    public async Task GetFixListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.FixOT);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "📝 Operaciones pendientes";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("📝 Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("dittoTrade")]
    [Alias("dt", "ditto")]
    [Summary("Hace que el bot te intercambie un Ditto con un idioma y una extensión de estadísticas solicitados.")]
    public async Task DittoTrade([Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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
        await DittoTrade(code, keyword, language, nature).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("dittoTrade")]
    [Alias("dt", "ditto")]
    [Summary("Hace que el bot te intercambie un Ditto con un idioma y una extensión de estadísticas solicitados.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("Una combinación de \"ATK/SPA/SPE\" or \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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

        keyword = keyword.ToLower().Trim();

        if (Enum.TryParse(language, true, out LanguageID lang))
        {
            language = lang.ToString();
        }
        else
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> No pude reconocer el idioma solicitado: {language}.", 2, Context.Message);
            return;
        }

        nature = nature.Trim()[..1].ToUpper() + nature.Trim()[1..].ToLower();
        var set = new ShowdownSet($"{keyword}(Ditto)\nLanguage: {language}\nNature: {nature}");
        var template = AutoLegalityWrapper.GetTemplate(set);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        var pkm = sav.GetLegal(template, out var result);
        TradeExtensions<T>.DittoTrade((T)pkm);
        var la = new LegalityAnalysis(pkm);

        if (pkm is not T pk || !la.Valid)
        {
            var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
            var imsg = $"<a:warning:1206483664939126795> Oops! {reason} Aquí está mi mejor intento para ese **Ditto**!";
            await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
            return;
        }

        pk.ResetPartyStats();

        // Ad Name Check
        if (Info.Hub.Config.Trade.TradeConfiguration.EnableSpamCheck)
        {
            if (TradeExtensions<T>.HasAdName(pk, out string ad))
            {
                await ReplyAndDeleteAsync("❌ Nombre de anuncio detectado en el nombre del Pokémon o en el nombre del entrenador, lo cual no está permitido.", 5);
                return;
            }
        }

        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("itemTrade")]
    [Alias("it", "item")]
    [Summary("Hace que el bot te intercambie un Pokémon que tenga el objeto solicitado, o un ditto si se proporciona la palabra clave de distribución de estadísticas.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task ItemTrade([Remainder] string item)
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
        await ItemTrade(code, item).ConfigureAwait(false);
    }

    [Command("itemTrade")]
    [Alias("it", "item")]
    [Summary("Hace que el robot te intercambie un Pokémon que tenga el objeto solicitado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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

        Species species = Info.Hub.Config.Trade.TradeConfiguration.ItemTradeSpecies == Species.None ? Species.Diglett : Info.Hub.Config.Trade.TradeConfiguration.ItemTradeSpecies;
        var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
        var template = AutoLegalityWrapper.GetTemplate(set);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        var pkm = sav.GetLegal(template, out var result);
        pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;

        if (pkm.HeldItem == 0)
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, el item que has solicitado no ha sido reconocido.", 2, Context.Message);
            return;
        }

        var la = new LegalityAnalysis(pkm);
        if (pkm is not T pk || !la.Valid)
        {
            var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
            var imsg = $"<a:warning:1206483664939126795> Oops! {reason} Aquí está mi mejor intento para: **{species}**!";
            await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
            return;
        }

        pk.ResetPartyStats();
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Item).ConfigureAwait(false);

        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("tradeList")]
    [Alias("tl")]
    [Summary("Muestra los usuarios en las colas comerciales.")]
    [RequireSudo]
    public async Task GetTradeListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "📝 Operaciones pendientes";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("📝 Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("egg")]
    [Alias("Egg")]
    [Summary("Intercambia un huevo generado a partir del nombre de Pokémon proporcionado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeEgg([Remainder] string egg)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        await TradeEggAsync(code, egg).ConfigureAwait(false);
    }

    [Command("egg")]
    [Alias("Egg")]
    [Summary("Intercambia un huevo generado a partir del nombre de Pokémon proporcionado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeEggAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);
        _ = Task.Run(async () =>
        {
            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;

                if (pkm is not T pk)
                {
                    _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> Oops! {Context.User.Mention}, No pude crear un huevo con el pokemon solicitado.", 2, Context.Message);
                    return;
                }

                // Use the EggTrade method without setting the nickname
                pk.IsNicknamed = false; // Make sure we don't set a nickname
                TradeExtensions<T>.EggTrade(pk, template);

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);

                _ = DeleteMessagesAfterDelayAsync(null, Context.Message, 2);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Se produjo un error al procesar la solicitud.", 2, Context.Message);
            }
            if (Context.Message is IUserMessage userMessage)
            {
                _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
            }
        });

        // Return immediately to avoid blocking
        await Task.CompletedTask;
    }

    [Command("hidetrade")]
    [Alias("ht")]
    [Summary("Hace que el bot te intercambie un Pokémon convertido del conjunto showdown proporcionado sin mostrar los detalles del intercambio.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task HideTradeAsync([Summary("Showdown Set")][Remainder] string content)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return HideTradeAsync(code, content);
    }

    [Command("hidetrade")]
    [Alias("ht")]
    [Summary("Hace que el bot te intercambie un Pokémon convertido del conjunto de showdown proporcionado sin mostrar los detalles del intercambio.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task HideTradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
        List<Pictocodes>? lgcode = null;
        var userID = Context.User.Id;

        /// Check if the user is already in the queue
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
        var ignoreAutoOT = content.Contains("OT:") || content.Contains("TID:") || content.Contains("SID:");
        content = ReusableActions.StripCodeBlock(content);

        // Check if the showdown set contains "Egg"
        bool isEgg = TradeExtensions<T>.IsEggCheck(content);

        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);

        if (set.InvalidLines.Count != 0)
        {
            var invalidLines = string.Join("\n", set.InvalidLines);
            var embed = new EmbedBuilder
            {
                Description = $"<a:warning:1206483664939126795> No se puede analizar el conjunto showdown:\n{invalidLines}",
                Color = Color.Red,
                ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            embed.WithAuthor("Error", "https://img.freepik.com/free-icon/warning_318-478601.jpg")
                 .WithFooter(footer =>
                 {
                     footer.Text = $"{Context.User.Username} • {DateTime.UtcNow.ToString("hh:mm tt")}";
                     footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                 });

            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
            await Task.Delay(2000);
            await Context.Message.DeleteAsync();
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];

                if (isEgg && pkm is T eggPk)
                {
                    eggPk.IsNicknamed = false; // Make sure we don't set a nickname
                    TradeExtensions<T>.EggTrade(eggPk, template);
                    pkm = eggPk; // Update the pkm reference
                    la = new LegalityAnalysis(pkm); // Re-analyze legality
                }
                else
                {
                    if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.SuggestRelearnMoves)
                    {
                        switch (pkm)
                        {
                            case PK9 pk9:
                                pk9.SetRecordFlagsAll();
                                break;

                            case PK8 pk8:
                                pk8.SetRecordFlagsAll();
                                break;

                            case PB8 pb8:
                                pb8.SetRecordFlagsAll();
                                break;

                            case PB7 pb7:
                            case PA8 pa8:
                                break;
                        }
                    }

                    pkm.HeldItem = pkm switch
                    {
                        PA8 => (int)HeldItem.None,
                        _ when pkm.HeldItem == 0 && !pkm.IsEgg => (int)SysCord<T>.Runner.Config.Trade.TradeConfiguration.DefaultHeldItem,
                        _ => pkm.HeldItem
                    };

                    if (pkm is PB7)
                    {
                        lgcode = TradeModule<T>.GenerateRandomPictocodes(3);
                        if (pkm.Species == (int)Species.Mew && pkm.IsShiny)
                        {
                            await ReplyAsync($"<a:warning:1206483664939126795> Lo siento {Context.User.Mention}, Mew **no** puede ser Shiny en LGPE. PoGo Mew no se transfiere y Pokeball Plus Mew tiene shiny lock.");
                            return;
                        }
                    }
                }
                bool setEdited = false;
                if (pkm is not T pk || !la.Valid || !string.IsNullOrEmpty(set.Form.ToString()))
                {
                    // Perform auto correct if it's on and send that shit through again
                    if (SysCord<T>.Runner.Config.Trade.AutoCorrectConfig.EnableAutoCorrect && !la.Valid)
                    {
                        var (finalShowdownSet, correctionMessages) = await AutoCorrectShowdown<T>.PerformAutoCorrect(content, pkm, la);
                        set = new ShowdownSet(finalShowdownSet);
                        template = AutoLegalityWrapper.GetTemplate(set);
                        pkm = sav.GetLegal(template, out result);
                        la = new LegalityAnalysis(pkm);
                        setEdited = true;
                        if (correctionMessages.Count > 0 && la.Valid)
                        {
                            var userName = Context.User.Mention;
                            var changesEmbed = new EmbedBuilder()
                                .WithTitle("Correcciones del set de Showdown")
                                .WithColor(Color.Orange)
                                .WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png")
                                .WithDescription(string.Join("\n", correctionMessages))
                                .AddField("Set de Showdown corregido:", $"```{finalShowdownSet}```")
                                .Build();
                            var correctionMessage = await ReplyAsync($"{userName}, tu conjunto de showdown era incorrecto o inválido y lo hemos corregido.\nAquí están las correcciones hechas:", embed: changesEmbed).ConfigureAwait(false);
                            _ = DeleteMessagesAfterDelayAsync(correctionMessage, Context.Message, 30);
                        }
                    }

                    if (pkm is not T correctedPk || !la.Valid)
                    {
                        var reason = result switch
                        {
                            "Timeout" => $"El **{spec}** tardó demasiado en generarse y se canceló.",
                            "VersionMismatch" => "❌ **Solicitud denegada:** La versión de **PKHeX** y **Auto-Legality Mod** no coinciden.",
                            _ => $"{Context.User.Mention}, no se pudo crear un **{spec}** con los datos proporcionados."
                        };

                        var embed = new EmbedBuilder
                        {
                            Title = "⚠️ Error en la Legalidad del Conjunto",
                            Description = $"<a:no:1206485104424128593> **Oops!** {reason}",
                            Color = new Color(255, 0, 0), // Bright red for better visibility
                            ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                            ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                        };

                        if (result == "Failed")
                            embed.AddField("🔍 Sugerencia:", AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm), false);

                        embed.WithAuthor(Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                             .WithFooter(footer =>
                             {
                                 footer.Text = $"Solicitado por {Context.User.Username} • {DateTime.UtcNow:hh:mm tt} UTC";
                                 footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                             });

                        // Enviar el mensaje y almacenar la referencia
                        var message = await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
                        // Esperar 2 segundos antes de eliminar el mensaje original
                        await Task.Delay(2000);
                        await Context.Message.DeleteAsync();

                        // Esperar 20 segundos antes de eliminar el mensaje de error
                        await Task.Delay(20000);  // Se ajusta el tiempo a 20 segundos 
                        await message.DeleteAsync();

                        return;
                    }
                    pk = correctedPk;
                }
                // Set correct MetDate for Mightiest Mark
                TradeExtensions<T>.CheckAndSetUnrivaledDate(pk);
                if (pk.WasEgg)
                    pk.EggMetDate = pk.MetDate;
                pk.ResetPartyStats();

                // Ad Name Check
                if (Info.Hub.Config.Trade.TradeConfiguration.EnableSpamCheck)
                {
                    if (TradeExtensions<T>.HasAdName(pk, out string ad))
                    {
                        await ReplyAndDeleteAsync("❌ Nombre de anuncio detectado en el nombre del Pokémon o en el nombre del entrenador, lo cual no está permitido.", 5);
                        return;
                    }
                }

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, isBatchTrade: false, batchTradeNumber: 1, totalBatchTrades: 1, true, false, lgcode: lgcode, ignoreAutoOT: ignoreAutoOT, setEdited: setEdited).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                var msg = $"<a:warning:1206483664939126795> ¡Oops! Ocurrió un problema inesperado con este conjunto de showdown:\n```{string.Join("\n", set.GetSetLines())}```";

                _ = ReplyAndDeleteAsync(msg, 2, Context.Message);
            }
            if (Context.Message is IUserMessage userMessage)
            {
                _ = DeleteMessagesAfterDelayAsync(userMessage, null, 0);
            }
        });

        // Return immediately to avoid blocking
        await Task.CompletedTask;
    }

    [Command("hidetrade")]
    [Alias("ht")]
    [Summary("Hace que el bot te intercambie el archivo Pokémon proporcionado sin mostrar los detalles del intercambio.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task HideTradeAsyncAttach(
            [Summary("Trade Code")] int code,
            [Summary("Ignore AutoOT")] bool ignoreAutoOT = false)
    {
        var sig = Context.User.GetFavor();
        return HideTradeAsyncAttach(code, sig, Context.User, ignoreAutoOT: ignoreAutoOT);
    }

    [Command("hidetrade")]
    [Alias("ht")]
    [Summary("Hace que el bot le intercambie el archivo adjunto sin mostrar los detalles de la inserción comercial.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    private async Task HideTradeAsyncAttach([Summary("Ignore AutoOT")] bool ignoreAutoOT = false)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        var sig = Context.User.GetFavor();
        await HideTradeAsyncAttach(code, sig, Context.User, ignoreAutoOT: ignoreAutoOT).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Hace que el bot te intercambie un Pokémon convertido del conjunto showdown proporcionado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return TradeAsync(code, content);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Hace que el robot te intercambie un Pokémon convertido del conjunto de showdown proporcionado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
        List<Pictocodes>? lgcode = null;

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

        var ignoreAutoOT = content.Contains("OT:") || content.Contains("TID:") || content.Contains("SID:");
        content = ReusableActions.StripCodeBlock(content);

        // Check if the showdown set contains "Egg"
        bool isEgg = TradeExtensions<T>.IsEggCheck(content);

        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);

        if (set.InvalidLines.Count != 0)
        {
            var invalidLines = string.Join("\n", set.InvalidLines);
            var embed = new EmbedBuilder
            {
                Description = $"<a:warning:1206483664939126795> No se puede analizar el conjunto showdown:\n{invalidLines}",
                Color = Color.Red,
                ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            embed.WithAuthor("Error", "https://img.freepik.com/free-icon/warning_318-478601.jpg")
                 .WithFooter(footer =>
                 {
                     footer.Text = $"{Context.User.Username} • {DateTime.UtcNow.ToString("hh:mm tt")}";
                     footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                 });

            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
            await Task.Delay(2000);
            await Context.Message.DeleteAsync();
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];

                if (isEgg && pkm is T eggPk)
                {
                    eggPk.IsNicknamed = false; // Make sure we don't set a nickname
                    TradeExtensions<T>.EggTrade(eggPk, template);
                    pkm = eggPk; // Update the pkm reference
                    la = new LegalityAnalysis(pkm); // Re-analyze legality
                }
                else
                {
                    if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.SuggestRelearnMoves)
                    {
                        switch (pkm)
                        {
                            case PK9 pk9:
                                pk9.SetRecordFlagsAll();
                                break;
                            case PK8 pk8:
                                pk8.SetRecordFlagsAll();
                                break;
                            case PB8 pb8:
                                pb8.SetRecordFlagsAll();
                                break;
                            case PB7 pb7:
                            case PA8 pa8:
                                break;
                        }
                    }

                    pkm.HeldItem = pkm switch
                    {
                        PA8 => (int)HeldItem.None,
                        _ when pkm.HeldItem == 0 && !pkm.IsEgg => (int)SysCord<T>.Runner.Config.Trade.TradeConfiguration.DefaultHeldItem,
                        _ => pkm.HeldItem
                    };

                    if (pkm is PB7)
                    {
                        lgcode = TradeModule<T>.GenerateRandomPictocodes(3);
                        if (pkm.Species == (int)Species.Mew && pkm.IsShiny)
                        {
                            await ReplyAsync($"<a:warning:1206483664939126795> Lo siento {Context.User.Mention}, Mew **no** puede ser Shiny en LGPE. PoGo Mew no se transfiere y Pokeball Plus Mew tiene shiny lock.");
                            return;
                        }
                    }
                }

                bool setEdited = false;
                if (pkm is not T pk || !la.Valid || !string.IsNullOrEmpty(set.Form.ToString()))
                {
                    // Perform auto correct if it's on and send that shit through again
                    if (SysCord<T>.Runner.Config.Trade.AutoCorrectConfig.EnableAutoCorrect && !la.Valid)
                    {
                        var (finalShowdownSet, correctionMessages) = await AutoCorrectShowdown<T>.PerformAutoCorrect(content, pkm, la);
                        set = new ShowdownSet(finalShowdownSet);
                        template = AutoLegalityWrapper.GetTemplate(set);
                        pkm = sav.GetLegal(template, out result);
                        la = new LegalityAnalysis(pkm);
                        setEdited = true;
                        if (correctionMessages.Count > 0 && la.Valid)
                        {
                            var userName = Context.User.Mention;
                            var changesEmbed = new EmbedBuilder()
                                .WithTitle("Correcciones del set de Showdown")
                                .WithColor(Color.Orange)
                                .WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png")
                                .WithDescription(string.Join("\n", correctionMessages))
                                .AddField("Set de Showdown corregido:", $"```{finalShowdownSet}```")
                                .Build();
                            var correctionMessage = await ReplyAsync($"{userName}, tu conjunto de showdown era incorrecto o inválido y lo hemos corregido.\nAquí están las correcciones hechas:", embed: changesEmbed).ConfigureAwait(false);
                            _ = DeleteMessagesAfterDelayAsync(correctionMessage, Context.Message, 30);
                        }
                    }

                    if (pkm is not T correctedPk || !la.Valid)
                    {
                        var reason = result switch
                        {
                            "Timeout" => $"El **{spec}** tardó demasiado en generarse y se canceló.",
                            "VersionMismatch" => "❌ **Solicitud denegada:** La versión de **PKHeX** y **Auto-Legality Mod** no coinciden.",
                            _ => $"{Context.User.Mention}, no se pudo crear un **{spec}** con los datos proporcionados."
                        };

                        var embed = new EmbedBuilder
                        {
                            Title = "⚠️ Error en la Legalidad del Conjunto",
                            Description = $"<a:no:1206485104424128593> **Oops!** {reason}",
                            Color = new Color(255, 0, 0), // Bright red for better visibility
                            ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                            ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                        };

                        if (result == "Failed")
                            embed.AddField("🔍 Sugerencia:", AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm), false);

                        embed.WithAuthor(Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                             .WithFooter(footer =>
                             {
                                 footer.Text = $"Solicitado por {Context.User.Username} • {DateTime.UtcNow:hh:mm tt} UTC";
                                 footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                             });

                        // Enviar el mensaje y almacenar la referencia
                        var message = await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
                        // Esperar 2 segundos antes de eliminar el mensaje original
                        await Task.Delay(2000);
                        await Context.Message.DeleteAsync();

                        // Esperar 20 segundos antes de eliminar el mensaje de error
                        await Task.Delay(20000);  // Se ajusta el tiempo a 20 segundos 
                        await message.DeleteAsync();

                        return;
                    }
                    pk = correctedPk;
                }
                // Set correct MetDate for Mightiest Mark
                TradeExtensions<T>.CheckAndSetUnrivaledDate(pk);
                if (pk.WasEgg)
                    pk.EggMetDate = pk.MetDate;
                pk.ResetPartyStats();

                // Ad Name Check
                if (Info.Hub.Config.Trade.TradeConfiguration.EnableSpamCheck)
                {
                    if (TradeExtensions<T>.HasAdName(pk, out string ad))
                    {
                        await ReplyAndDeleteAsync("❌ Nombre de anuncio detectado en el nombre del Pokémon o en el nombre del entrenador, lo cual no está permitido.", 5);
                        return;
                    }
                }

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, isBatchTrade: false, batchTradeNumber: 1, totalBatchTrades: 1, lgcode: lgcode, ignoreAutoOT: ignoreAutoOT, setEdited: setEdited).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                var msg = $"<a:warning:1206483664939126795> ¡Oops! Ocurrió un problema inesperado con este conjunto de showdown:\n```{string.Join("\n", set.GetSetLines())}```";

                _ = ReplyAndDeleteAsync(msg, 2, null);
            }
            if (Context.Message is IUserMessage userMessage)
            {
                _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
            }
        });

        // Return immediately to avoid blocking
        await Task.CompletedTask;
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Hace que el bot te intercambie el archivo Pokémon proporcionado.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsyncAttach(
    [Summary("Trade Code")] int code,
    [Summary("Ignore AutoOT")] bool ignoreAutoOT = false)
    {
        var sig = Context.User.GetFavor();
        return TradeAsyncAttachInternal(code, sig, Context.User, ignoreAutoOT: ignoreAutoOT);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Hace que el bot le intercambie el archivo adjunto.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeAsyncAttach([Summary("Ignore AutoOT")] bool ignoreAutoOT = false)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        var sig = Context.User.GetFavor();
        await Task.Run(async () =>
        {
            await TradeAsyncAttachInternal(code, sig, Context.User, ignoreAutoOT).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    [Command("batchTrade")]
    [Alias("bt")]
    [Summary("Hace que el bot intercambie varios Pokémon de la lista proporcionada, hasta un máximo de 3 intercambios.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeAsync([Summary("Lista de conjuntos de showdowns separados por '---'")][Remainder] string content)
    {
        // First, check if batch trades are allowed
        if (!SysCord<T>.Runner.Config.Trade.TradeConfiguration.AllowBatchTrades)
        {
            _ = ReplyAndDeleteAsync("<a:warning:1206483664939126795> Los intercambios por lotes están actualmente deshabilitados.", 2);
            return;
        }

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

        content = ReusableActions.StripCodeBlock(content);
        var trades = TradeModule<T>.ParseBatchTradeContent(content);
        var maxTradesAllowed = SysCord<T>.Runner.Config.Trade.TradeConfiguration.MaxPkmsPerTrade;

        // Check if batch mode is allowed and if the number of trades exceeds the limit
        if (maxTradesAllowed < 1 || trades.Count > maxTradesAllowed)
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention} Sólo puedes procesar hasta **{maxTradesAllowed}** trades a la vez. Por favor, reduzca el número de operaciones en su lote.", 5, Context.Message);
            _ = DeleteMessagesAfterDelayAsync(null, Context.Message, 2);
            return;
        }

        var batchTradeCode = Info.GetRandomTradeCode(userID);

        // Execute the trades in order of request, with delay
        for (int i = 0; i < trades.Count; i++)
        {
            var trade = trades[i];
            int batchTradeNumber = i + 1;
            // Execute
            await ProcessSingleTradeAsync(trade, batchTradeCode, true, batchTradeNumber, trades.Count);
            // Log to confirm trade order and pause
            Console.WriteLine($"Completed batch trade #{batchTradeNumber}: {trade}");
            // Add a delay of 3/4 of a second before processing the next batch trade number
            if (i < trades.Count - 1)
            {
                await Task.Delay(750); // 750 milliseconds = 0.75 seconds (Delay to process order)
            }
        }

        // Final cleanup
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    private static List<string> ParseBatchTradeContent(string content)
    {
        var delimiters = new[] { "---", "—-", "\n\n" }; // Includes both three hyphens and an em dash followed by a hyphen for phone users, and just a normal space in between.
        return content.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                            .Select(trade => trade.Trim())
                            .ToList();
    }

    [Command("batchtradezip")]
    [Alias("btz")]
    [Summary("Hace que el bot intercambie varios Pokémon desde el archivo .zip proporcionado, hasta un máximo de 6 intercambios.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeZipAsync()
    {
        var userID = Context.User.Id;
        var batchTradeCode = Info.GetRandomTradeCode(userID);
        await BatchTradeZipAsync(batchTradeCode).ConfigureAwait(false);
    }


    [Command("batchtradezip")]
    [Alias("btz")]
    [Summary("Hace que el bot intercambie varios Pokémon desde el archivo .zip proporcionado, hasta un máximo de 6 intercambios.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeZipAsync([Summary("Trade Code")] int code)
    {
        // First, check if batch trades are allowed
        if (!SysCord<T>.Runner.Config.Trade.TradeConfiguration.AllowBatchTrades)
        {
            _ = ReplyAndDeleteAsync($"<a:no:1206485104424128593> {Context.User.Mention} Los intercambios por lotes están actualmente deshabilitados.", 2);
            return;
        }

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

        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == default)
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, no se ha adjuntado ningún archivo. ¡Por favor intenta de nuevo!", 2);
            return;
        }

        if (!attachment.Filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, el formato de archivo no es válido. Por favor, proporciona un archivo en formato .zip.", 2);
            return;
        }

        var zipBytes = await new HttpClient().GetByteArrayAsync(attachment.Url);
        await using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entries = archive.Entries.ToList();

        const int maxTradesAllowed = 6; // for full team in the zip created

        // Check if batch mode is allowed and if the number of trades exceeds the limit
        if (maxTradesAllowed < 1 || entries.Count > maxTradesAllowed)
        {
            _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, solo puedes procesar hasta {maxTradesAllowed} intercambios a la vez. Reduce la cantidad de Pokémon en tu archivo .zip.", 5, Context.Message);
            return;
        }

        var batchTradeCode = Info.GetRandomTradeCode(userID);
        int batchTradeNumber = 1;

        foreach (var entry in entries)
        {
            await using var entryStream = entry.Open();
            var pkBytes = await TradeModule<T>.ReadAllBytesAsync(entryStream).ConfigureAwait(false);
            var pk = EntityFormat.GetFromBytes(pkBytes);

            if (pk is T)
            {
                await ProcessSingleTradeAsync((T)pk, batchTradeCode, true, batchTradeNumber, entries.Count);
                batchTradeNumber++;
            }
        }
        if (Context.Message is IUserMessage userMessage)
        {
            _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    private async Task ProcessSingleTradeAsync(T pk, int batchTradeCode, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var la = new LegalityAnalysis(pk);
                var spec = GameInfo.Strings.Species[pk.Species];

                if (!la.Valid)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> El {spec} en el archivo proporcionado no es legal.").ConfigureAwait(false);
                    return;
                }
                // Set correct MetDate for Mightiest Mark
                TradeExtensions<T>.CheckAndSetUnrivaledDate(pk);
                pk.ResetPartyStats();

                var userID = Context.User.Id;
                var code = Info.GetRandomTradeCode(userID);
                var lgcode = Info.GetRandomLGTradeCode();

                // Ad Name Check
                if (Info.Hub.Config.Trade.TradeConfiguration.EnableSpamCheck)
                {
                    if (TradeExtensions<T>.HasAdName(pk, out string ad))
                    {
                        await ReplyAndDeleteAsync("❌ Nombre de anuncio detectado en el nombre del Pokémon o en el nombre del entrenador, lo cual no está permitido.", 5);
                        return;
                    }
                }

                // Add the trade to the queue
                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(batchTradeCode, Context.User.Username, pk, sig, Context.User, isBatchTrade, batchTradeNumber, totalBatchTrades, lgcode: lgcode, tradeType: PokeTradeType.Batch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            }
        });

        // Return immediately to avoid blocking
        await Task.CompletedTask;
    }

    private async Task ProcessSingleTradeAsync(string tradeContent, int batchTradeCode, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades)
    {
        tradeContent = ReusableActions.StripCodeBlock(tradeContent);
        var set = new ShowdownSet(tradeContent);
        var ignoreAutoOT = tradeContent.Contains("OT:") || tradeContent.Contains("TID:") || tradeContent.Contains("SID:");
        var template = AutoLegalityWrapper.GetTemplate(set);

        if (set.InvalidLines.Count != 0)
        {
            var msg = $"No se puede analizar el conjunto showdown:\n{string.Join("\n", set.InvalidLines)}";
            await ReplyAsync(msg).ConfigureAwait(false);
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                bool setEdited = false;
                if (pkm is not T pk || !la.Valid || !string.IsNullOrEmpty(set.Form.ToString()))
                {
                    // Perform auto correct if it's on and send that shit through again
                    if (SysCord<T>.Runner.Config.Trade.AutoCorrectConfig.EnableAutoCorrect && !la.Valid)
                    {
                        var (finalShowdownSet, correctionMessages) = await AutoCorrectShowdown<T>.PerformAutoCorrect(tradeContent, pkm, la);
                        set = new ShowdownSet(finalShowdownSet);
                        template = AutoLegalityWrapper.GetTemplate(set);
                        pkm = sav.GetLegal(template, out result);
                        la = new LegalityAnalysis(pkm);
                        setEdited = true;
                        if (correctionMessages.Count > 0 && la.Valid)
                        {
                            var userName = Context.User.Mention;
                            var changesEmbed = new EmbedBuilder()
                                .WithTitle("Correcciones del set de Showdown")
                                .WithColor(Color.Orange)
                                .WithThumbnailUrl("https://raw.githubusercontent.com/bdawg1989/sprites/main/profoak.png")
                                .WithDescription(string.Join("\n", correctionMessages))
                                .AddField("Set de Showdown corregido:", $"```{finalShowdownSet}```")
                                .Build();
                            var correctionMessage = await ReplyAsync($"{userName}, tu conjunto de showdown era incorrecto o inválido y lo hemos corregido.\nAquí están las correcciones hechas:", embed: changesEmbed).ConfigureAwait(false);
                            _ = DeleteMessagesAfterDelayAsync(correctionMessage, Context.Message, 30);
                        }
                    }

                    if (pkm is not T correctedPk || !la.Valid)
                    {
                        var reason = result switch
                        {
                            "Timeout" => $"El conjunto {spec} tardó demasiado en generarse.",
                            "VersionMismatch" => "Solicitud rechazada: La versión de **PKHeX** y **Auto-Legality Mod** no coinciden.",
                            _ => $"No pude crear un {spec} a partir de ese conjunto.."
                        };

                        var errorMessage = $"<a:no:1206485104424128593> Oops! {reason}";
                        if (result == "Failed")
                            errorMessage += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";

                        var errorEmbed = new EmbedBuilder
                        {
                            Description = errorMessage,
                            Color = Color.Red,
                            ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                            ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                        };

                        errorEmbed.WithAuthor("Error al procesar el trade", "https://i.imgur.com/0R7Yvok.gif")
                             .WithFooter(footer =>
                             {
                                 footer.Text = $"{Context.User.Username} • {DateTime.UtcNow.ToString("hh:mm tt")}";
                                 footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                             });

                        await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                        return;
                    }

                    pk = correctedPk;
                }

                if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.SuggestRelearnMoves)
                {
                    if (pkm is PK9 pk9)
                    {
                        pk9.SetRecordFlagsAll();
                    }
                    else if (pkm is PK8 pk8)
                    {
                        pk8.SetRecordFlagsAll();
                    }
                    else if (pkm is PB8 pb8)
                    {
                        pb8.SetRecordFlagsAll();
                    }
                    else if (pkm is PB7 pb7)
                    {
                        // not applicable for PB7 (LGPE)
                    }
                    else if (pkm is PA8 pa8)
                    {
                        // not applicable for PA8 (Legends: Arceus)
                    }
                }

                if (pkm is PA8)
                {
                    pkm.HeldItem = (int)HeldItem.None; // Set to None for "Legends: Arceus" Pokémon
                }
                else if (pkm.HeldItem == 0 && !pkm.IsEgg)
                {
                    pkm.HeldItem = (int)SysCord<T>.Runner.Config.Trade.TradeConfiguration.DefaultHeldItem;
                }

                if (pkm is PB7)
                {
                    if (pkm.Species == (int)Species.Mew)
                    {
                        if (pkm.IsShiny)
                        {
                            await ReplyAsync("Mew **no** puede ser Shiny en LGPE. PoGo Mew no se transfiere y Pokeball Plus Mew tiene shiny lock.");
                            return;
                        }
                    }
                }
                // Set correct MetDate for Mightiest Mark
                TradeExtensions<T>.CheckAndSetUnrivaledDate(pkm);
                if (pkm.WasEgg)
                    pkm.EggMetDate = pkm.MetDate;
                pkm.ResetPartyStats();

                var userID = Context.User.Id;
                var code = Info.GetRandomTradeCode(userID);
                var lgcode = Info.GetRandomLGTradeCode();
                if (pkm is PB7)
                {
                    lgcode = TradeModule<T>.GenerateRandomPictocodes(3);
                }

                // Ad Name Check
                if (Info.Hub.Config.Trade.TradeConfiguration.EnableSpamCheck)
                {
                    if (TradeExtensions<T>.HasAdName(pk, out string ad))
                    {
                        await ReplyAndDeleteAsync("❌ Nombre de anuncio detectado en el nombre del Pokémon o en el nombre del entrenador, lo cual no está permitido.", 5);
                        return;
                    }
                }

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(batchTradeCode, Context.User.Username, pk, sig, Context.User, isBatchTrade, batchTradeNumber, totalBatchTrades, lgcode: lgcode, tradeType: PokeTradeType.Batch, ignoreAutoOT: ignoreAutoOT, setEdited: setEdited).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            }
        });

        // Return immediately to avoid blocking
        await Task.CompletedTask;
    }

    [Command("listevents")]
    [Alias("le")]
    [Summary("Enumera los archivos de eventos disponibles, filtrados por una letra o subcadena específica, y envía la lista a través de DM.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task ListEventsAsync([Remainder] string args = "")
    {
        const int itemsPerPage = 20; // Number of items per page
        var eventsFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.EventsFolder;
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        // Check if the events folder path is not set or empty
        if (string.IsNullOrEmpty(eventsFolderPath))
        {
            _ = ReplyAndDeleteAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.", 2, Context.Message);
            return;
        }

        // Parsing the arguments to separate filter and page number
        string filter = "";
        int page = 1;
        var parts = args.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > 0)
        {
            // Check if the last part is a number (page number)
            if (int.TryParse(parts.Last(), out int parsedPage))
            {
                page = parsedPage;
                filter = string.Join(" ", parts.Take(parts.Length - 1));
            }
            else
            {
                filter = string.Join(" ", parts);
            }
        }

        var allEventFiles = Directory.GetFiles(eventsFolderPath)
                                     .Select(Path.GetFileNameWithoutExtension)
                                     .OrderBy(file => file)
                                     .ToList();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var filteredEventFiles = allEventFiles
                                 .Where(file => string.IsNullOrWhiteSpace(filter) || file.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                 .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        IUserMessage replyMessage;

        // Check if there are no files matching the filter
        if (!filteredEventFiles.Any())
        {
            replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron eventos que coincidan con el filtro '{filter}'.");
            _ = DeleteMessagesAfterDelayAsync(replyMessage, Context.Message, 10);
        }
        else
        {
            var pageCount = (int)Math.Ceiling(filteredEventFiles.Count / (double)itemsPerPage);
            page = Math.Clamp(page, 1, pageCount); // Ensure page number is within valid range

            var pageItems = filteredEventFiles.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);

            var embed = new EmbedBuilder()
                .WithTitle($"📝 Eventos disponibles - Filtro: '{filter}'")
                .WithDescription($"Pagina {page} de {pageCount}")
                .WithColor(Color.Blue);

            foreach (var item in pageItems)
            {
                var index = allEventFiles.IndexOf(item) + 1; // Get the index from the original list
                embed.AddField($"{index}. {item}", $"Usa `{botPrefix}er {index}` en el canal correspondiente para solicitar este evento.");
            }

            if (Context.User is IUser user)
            {
                try
                {
                    var dmChannel = await user.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync(embed: embed.Build());
                    replyMessage = await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, Te envié un DM con la lista de eventos.");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    // This exception is thrown when the bot cannot send DMs to the user
                    replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("<a:Error:1223766391958671454> **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
            }

            _ = DeleteMessagesAfterDelayAsync(replyMessage, Context.Message, 10);
        }
    }

    [Command("eventrequest")]
    [Alias("er")]
    [Summary("Descarga archivos adjuntos de eventos de la carpeta de eventos especificada y los agrega a la cola de transacciones.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task EventRequestAsync(int index)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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
            var eventsFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.EventsFolder;
            var eventFiles = Directory.GetFiles(eventsFolderPath)
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            // Check if the events folder path is not set or empty
            if (string.IsNullOrEmpty(eventsFolderPath))
            {
                _ = ReplyAndDeleteAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.", 2, Context.Message);
                return;
            }

            if (index < 1 || index > eventFiles.Count)
            {
                _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Índice de eventos no válido. Utilice un número de evento válido mostrado en la lista que te envie al MD cuando usaste el comando `.le`.", 2, Context.Message);
                return;
            }

            var selectedFile = eventFiles[index - 1]; // Adjust for zero-based indexing
#pragma warning disable CS8604 // Possible null reference argument.
            var fileData = await File.ReadAllBytesAsync(Path.Combine(eventsFolderPath, selectedFile));
#pragma warning restore CS8604 // Possible null reference argument.
            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = GetRequest(download);
            if (pk == null)
            {
                _ = ReplyAndDeleteAsync("<a:warning:1206483664939126795> No se pudo convertir el archivo de eventos al tipo PKM requerido.", 2, Context.Message);
                return;
            }

            var code = Info.GetRandomTradeCode(userID);
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();

            await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention} Evento solicitado, agregado a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ = ReplyAndDeleteAsync($"<a:Error:1223766391958671454> Ocurrió un error: {ex.Message}", 2, Context.Message);
        }
        finally
        {
            if (Context.Message is IUserMessage userMessage)
            {
                _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
            }
        }
    }

    [Command("battlereadylist")]
    [Alias("brl")]
    [Summary("Enumera los archivos disponibles listos para la batalla, filtrados por una letra o subcadena específica, y envía la lista a través de DM.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task BattleReadyListAsync([Remainder] string args = "")
    {
        const int itemsPerPage = 20; // Number of items per page
        var battleReadyFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.BattleReadyPKMFolder;
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        // Check if the battleready folder path is not set or empty
        if (string.IsNullOrEmpty(battleReadyFolderPath))
        {
            _ = ReplyAndDeleteAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.", 2, Context.Message);
            return;
        }

        // Parsing the arguments to separate filter and page number
        string filter = "";
        int page = 1;
        var parts = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > 0)
        {
            // Check if the last part is a number (page number)
            if (int.TryParse(parts.Last(), out int parsedPage))
            {
                page = parsedPage;
                filter = string.Join(" ", parts.Take(parts.Length - 1));
            }
            else
            {
                filter = string.Join(" ", parts);
            }
        }

        var allBattleReadyFiles = Directory.GetFiles(battleReadyFolderPath)
                                           .Select(Path.GetFileNameWithoutExtension)
                                           .OrderBy(file => file)
                                           .ToList();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var filteredBattleReadyFiles = allBattleReadyFiles
                                       .Where(file => string.IsNullOrWhiteSpace(filter) || file.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                       .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        IUserMessage replyMessage;

        // Check if there are no files matching the filter
        if (!filteredBattleReadyFiles.Any())
        {
            replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> No se encontraron archivos listos para la batalla que coincidan con el filtro. '{filter}'.");
            _ = DeleteMessagesAfterDelayAsync(replyMessage, Context.Message, 10);
        }
        else
        {
            var pageCount = (int)Math.Ceiling(filteredBattleReadyFiles.Count / (double)itemsPerPage);
            page = Math.Clamp(page, 1, pageCount); // Ensure page number is within valid range

            var pageItems = filteredBattleReadyFiles.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);

            var embed = new EmbedBuilder()
                .WithTitle($"📝 Archivos listos para la batalla disponibles - Filtro: '{filter}'")
                .WithDescription($"Pagina {page} de {pageCount}")
                .WithColor(Color.Blue);

            foreach (var item in pageItems)
            {
                var index = allBattleReadyFiles.IndexOf(item) + 1; // Get the index from the original list
                embed.AddField($"{index}. {item}", $"Usa `{botPrefix}brr {index}` en el canal correspondiente para solicitar este archivo listo para batalla.");
            }

            if (Context.User is IUser user)
            {
                try
                {
                    var dmChannel = await user.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync(embed: embed.Build());
                    replyMessage = await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, Te envié un DM con la lista de archivos pokemon listos para batalla.");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    // This exception is thrown when the bot cannot send DMs to the user
                    replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("<a:Error:1223766391958671454> **Error**: No se puede enviar un MD. Por favor verifique su **Configuración de privacidad del servidor**.");
            }

            _ = DeleteMessagesAfterDelayAsync(replyMessage, Context.Message, 10);
        }
    }

    [Command("battlereadyrequest")]
    [Alias("brr", "br")]
    [Summary("Descarga archivos adjuntos listos para la batalla desde la carpeta especificada y los agrega a la cola de intercambios.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BattleReadyRequestAsync(int index)
    {
        var userID = Context.User.Id;

        // Check if the user is already in the queue
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
            var battleReadyFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.BattleReadyPKMFolder;
            var battleReadyFiles = Directory.GetFiles(battleReadyFolderPath)
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            // Check if the battleready folder path is not set or empty
            if (string.IsNullOrEmpty(battleReadyFolderPath))
            {
                _ = ReplyAndDeleteAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.", 2, Context.Message);
                return;
            }

            if (index < 1 || index > battleReadyFiles.Count)
            {
                _ = ReplyAndDeleteAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Índice de archivos listos para la batalla no válido. Utilice un número de archivo mostrado en la lista que te envie al MD cuando usaste el comando `.blr`.", 2, Context.Message);
                return;
            }

            var selectedFile = battleReadyFiles[index - 1];
#pragma warning disable CS8604 // Possible null reference argument.
            var fileData = await File.ReadAllBytesAsync(Path.Combine(battleReadyFolderPath, selectedFile));
#pragma warning restore CS8604 // Possible null reference argument.
            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = GetRequest(download);
            if (pk == null)
            {
                _ = ReplyAndDeleteAsync("<a:warning:1206483664939126795> No se pudo convertir el archivo listo para batalla al tipo PKM requerido.", 2, Context.Message);
                return;
            }

            var code = Info.GetRandomTradeCode(userID);
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();

            await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, solicitud de Pokemon listo para batalla agregada a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ = ReplyAndDeleteAsync($"<a:Error:1223766391958671454> Ocurrió un error: {ex.Message}", 2, Context.Message);
        }
        finally
        {
            if (Context.Message is IUserMessage userMessage)
            {
                _ = DeleteMessagesAfterDelayAsync(userMessage, null, 2);
            }
        }
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Hace que el bot intercambie al usuario mencionado el archivo adjunto.")]
    [RequireSudo]
    public async Task TradeAsyncAttachUser([Summary("Trade Code")] int code, [Remainder] string _)
    {
        if (Context.Message.MentionedUsers.Count > 1)
        {
            await ReplyAsync("<a:warning:1206483664939126795> Demasiadas menciones. Solo puedes agregar a la lista un usario a la vez.").ConfigureAwait(false);
            return;
        }

        if (Context.Message.MentionedUsers.Count == 0)
        {
            await ReplyAsync("<a:warning:1206483664939126795> Un usuario debe ser mencionado para hacer esto.").ConfigureAwait(false);
            return;
        }

        var usr = Context.Message.MentionedUsers.ElementAt(0);
        var sig = usr.GetFavor();
        await Task.Run(async () =>
        {
            await TradeAsyncAttachInternal(code, sig, usr).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Hace que el bot intercambie al usuario mencionado el archivo adjunto.")]
    [RequireSudo]
    public Task TradeAsyncAttachUser([Remainder] string _)
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return TradeAsyncAttachUser(code, _);
    }

    private async Task TradeAsyncAttachInternal(int code, RequestSignificance sig, SocketUser usr, bool ignoreAutoOT = false)
    {
        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == default)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, no se ha proporcionado ningún archivo adjunto. ¡Por favor, inténtalo de nuevo!").ConfigureAwait(false);
            return;
        }
        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, ¡el archivo adjunto proporcionado no es compatible con este módulo!").ConfigureAwait(false);
            return;
        }
        await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr, ignoreAutoOT: ignoreAutoOT).ConfigureAwait(false);
    }

    private async Task HideTradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr, bool ignoreAutoOT = false)
    {
        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == default)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, no se ha proporcionado ningún archivo adjunto. ¡Por favor, inténtalo de nuevo!").ConfigureAwait(false);
            return;
        }

        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            await ReplyAsync("<a:warning:1206483664939126795> ¡El archivo adjunto proporcionado no es compatible con este módulo!").ConfigureAwait(false);
            return;
        }
        await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr, isHiddenTrade: true, ignoreAutoOT: ignoreAutoOT).ConfigureAwait(false);
    }

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

    private async Task AddTradeToQueueAsync(int code, string trainerName, T? pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isHiddenTrade = false, bool isMysteryTrade = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, PokeTradeType tradeType = PokeTradeType.Specific, bool ignoreAutoOT = false, bool setEdited = false)
    {
        lgcode ??= TradeModule<T>.GenerateRandomPictocodes(3);
        if (pk is not null && !pk.CanBeTraded())
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
            await Task.Delay(6000).ConfigureAwait(false); // Delay for 6 seconds
            await reply.DeleteAsync().ConfigureAwait(false);
            return;
        }
        var la = new LegalityAnalysis(pk!);
        if (!la.Valid)
        {
            string legalityReport = la.Report(verbose: false);
            var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
            var embedBuilder = new EmbedBuilder(); // Crear el objeto EmbedBuilder
            embedBuilder.WithColor(Color.Red); // Opcional: establecer el color del embed

            if (pk?.IsEgg == true)
            {
                string speciesName = SpeciesName.GetSpeciesName(pk.Species, (int)LanguageID.English);
                embedBuilder.WithAuthor("Conjunto de showdown no válido!", customIconUrl);
                embedBuilder.WithDescription($"<a:no:1206485104424128593> {usr.Mention} El conjunto de showdown __no es válido__ para un huevo de **{speciesName}**.");
                embedBuilder.AddField("__**Error**__", $"Puede que __**{speciesName}**__ no se pueda obtener en un huevo o algún dato esté impidiendo el trade.", inline: true);
                embedBuilder.AddField("__**Solución**__", $"Revisa tu __información__ y vuelve a intentarlo.", inline: true);
                embedBuilder.AddField("Reporte:", $"\n```{la.Report()}```");
            }
            else
            {
                string speciesName = SpeciesName.GetSpeciesName(pk!.Species, (int)LanguageID.English);
                embedBuilder.WithAuthor("Archivo adjunto no valido!", customIconUrl);
                embedBuilder.WithDescription($"<a:no:1206485104424128593> {usr.Mention}, este **{speciesName}** no es nativo de este juego y no se puede intercambiar!\n### He aquí la razón:\n```{legalityReport}```\n```🔊Consejo:\n• Por favor verifica detenidamente la informacion en PKHeX e intentalo de nuevo!\n• Puedes utilizar el plugin de ALM para legalizar tus pokemons y ahorrarte estos problemas.```");
            }
            embedBuilder.WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png");
            embedBuilder.WithImageUrl("https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif");
            // Añadir el footer con icono y texto
            embedBuilder.WithFooter(footer =>
            {
                footer.WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());
                footer.WithText($"{Context.User.Username} | {DateTimeOffset.Now.ToString("hh:mm tt")}");
            });

            var reply = await ReplyAsync(embed: embedBuilder.Build()).ConfigureAwait(false); // Enviar el embed'
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            await Task.Delay(10000); // Esperar antes de borrar
            await reply.DeleteAsync().ConfigureAwait(false); // Borrar el mensaje
            return;
        }
        bool isNonNative = false;
        if (la.EncounterOriginal.Context != pk?.Context || pk?.GO == true)
        {
            isNonNative = true;
        }
        if (Info.Hub.Config.Legality.DisallowNonNatives && (la.EncounterOriginal.Context != pk?.Context || pk?.GO == true))
        {
            var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
            var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
            var customthumbnail = "https://i.imgur.com/DWLEXyu.png";
            string speciesName = SpeciesName.GetSpeciesName(pk!.Species, (int)LanguageID.English);
            // Allow the owner to prevent trading entities that require a HOME Tracker even if the file has one already.
            var embedBuilder = new EmbedBuilder()
                .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                .WithDescription($"<a:no:1206485104424128593> {usr.Mention}, este **{speciesName}** no es nativo de este juego y no se puede intercambiar!")
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
        if (Info.Hub.Config.Legality.DisallowTracked && pk is IHomeTrack { HasTracker: true })
        {
            var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
            var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
            var customthumbnail = "https://i.imgur.com/DWLEXyu.png";
            string speciesName = SpeciesName.GetSpeciesName(pk.Species, (int)LanguageID.English);
            // Allow the owner to prevent trading entities that already have a HOME Tracker.
            var embedBuilder = new EmbedBuilder()
                .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                .WithDescription($"<a:no:1206485104424128593> {usr.Mention}, este archivo de **{speciesName}** ya tiene un **HOME Tracker** y ni puede ser tradeado!")
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
            var clone = (T)pk!.Clone();
            clone.HandlingTrainerName = pk.OriginalTrainerName;
            clone.HandlingTrainerGender = pk.OriginalTrainerGender;
            if (clone is PK8 or PA8 or PB8 or PK9)
                ((dynamic)clone).HandlingTrainerLanguage = (byte)pk.Language;
            clone.CurrentHandler = 1;
            la = new LegalityAnalysis(clone);
            if (la.Valid) pk = clone;
        }
        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk!, PokeRoutineType.LinkTrade, tradeType, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryTrade, isMysteryEgg, lgcode, ignoreAutoOT: ignoreAutoOT, setEdited: setEdited, isNonNative: isNonNative).ConfigureAwait(false);
    }

    public static List<Pictocodes> GenerateRandomPictocodes(int count)
    {
        Random rnd = new();
        List<Pictocodes> randomPictocodes = [];
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

    private async Task ReplyAndDeleteAsync(string message, int delaySeconds, IMessage? messageToDelete = null)
    {
        try
        {
            var sentMessage = await ReplyAsync(message).ConfigureAwait(false);
            _ = DeleteMessagesAfterDelayAsync(sentMessage, messageToDelete, delaySeconds);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
        }
    }

    private async Task DeleteMessagesAfterDelayAsync(IMessage? sentMessage, IMessage? messageToDelete, int delaySeconds)
    {
        try
        {
            await Task.Delay(delaySeconds * 1000);

            if (sentMessage != null)
            {
                try
                {
                    await sentMessage.DeleteAsync();
                }
                catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    // Ignore Unknown Message exception
                }
            }

            if (messageToDelete != null)
            {
                try
                {
                    await messageToDelete.DeleteAsync();
                }
                catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
                {
                    // Ignore Unknown Message exception
                }
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
        }
    }
}
