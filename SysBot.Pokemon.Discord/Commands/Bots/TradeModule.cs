using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SysBot.Pokemon.TradeSettings.TradeSettingsCategory;

namespace SysBot.Pokemon.Discord;

[Summary("Queues new Link Code trades")]
public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    private static readonly char[] separator = [' '];
    private static readonly char[] separatorArray = [' '];
    private static readonly char[] separatorArray0 = [' '];

    [Command("listguilds")]
    [Alias("lg", "servers", "listservers")]
    [Summary("Lists all guilds the bot is part of.")]
    [RequireSudo]
    public async Task ListGuilds(int page = 1)
    {
        const int guildsPerPage = 25; // Discord limit for fields in an embed
        int guildCount = Context.Client.Guilds.Count;
        int totalPages = (int)Math.Ceiling(guildCount / (double)guildsPerPage);
        page = Math.Max(1, Math.Min(page, totalPages));

        var guilds = Context.Client.Guilds
            .Skip((page - 1) * guildsPerPage)
            .Take(guildsPerPage);

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"Lista de Servidores - Pagina {page}/{totalPages}")
            .WithDescription("📝 Aquí están los servidores en los que estoy actualmente:")
            .WithColor(Color.Blue);

        foreach (var guild in guilds)
        {
            embedBuilder.AddField(guild.Name, $"ID: {guild.Id}", inline: true);
        }
        var dmChannel = await Context.User.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync(embed: embedBuilder.Build());

        await ReplyAsync($"{Context.User.Mention}, Te envié un DM con la lista de servidores. (Pagina {page}).");

        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000);
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    [Command("blacklistserver")]
    [Alias("bls")]
    [Summary("Adds a server ID to the bot's server blacklist.")]
    [RequireOwner]
    public async Task BlacklistServer(ulong serverId)
    {
        var settings = SysCord<T>.Runner.Hub.Config.Discord;

        if (settings.ServerBlacklist.Contains(serverId))
        {
            await ReplyAsync("<a:warning:1206483664939126795> Este servidor ya está en la lista negra.");
            return;
        }

        var server = Context.Client.GetGuild(serverId);
        if (server == null)
        {
            await ReplyAsync("<a:warning:1206483664939126795> No se puede encontrar un servidor con el ID proporcionado. Asegúrese de que el bot sea miembro del servidor que desea incluir en la lista negra.");
            return;
        }

        var newServerAccess = new RemoteControlAccess { ID = serverId, Name = server.Name, Comment = "Servidor en lista negra" };

        settings.ServerBlacklist.AddIfNew([newServerAccess]);

        await server.LeaveAsync();
        await ReplyAsync($"<a:yes:1206485105674166292> Deje el servidor '{server.Name}' y lo agregue a la lista negra.");
    }

    [Command("unblacklistserver")]
    [Alias("ubls")]
    [Summary("Removes a server ID from the bot's server blacklist.")]
    [RequireOwner]
    public async Task UnblacklistServer(ulong serverId)
    {
        var settings = SysCord<T>.Runner.Hub.Config.Discord;

        if (!settings.ServerBlacklist.Contains(serverId))
        {
            await ReplyAsync("<a:warning:1206483664939126795> Este servidor no está actualmente en la lista negra.");
            return;
        }

        var wasRemoved = settings.ServerBlacklist.RemoveAll(x => x.ID == serverId) > 0;

        if (wasRemoved)
        {
            await ReplyAsync($"<a:yes:1206485105674166292> El servidor con ID {serverId} ha sido eliminado de la lista negra.");
        }
        else
        {
            await ReplyAsync("<a:warning:1206483664939126795> Se produjo un error al intentar eliminar el servidor de la lista negra. Verifique la ID del servidor e inténtelo nuevamente.");
        }
    }

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT()
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

        var code = Info.GetRandomTradeCode();
        var trainerName = Context.User.Username;
        var lgcode = Info.GetRandomLGTradeCode();
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT, Context.User, false, 1, 1, false, lgcode).ConfigureAwait(false);
    }

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT([Summary("Trade Code")] int code)
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

        var trainerName = Context.User.Username;
        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT, Context.User, false, 1, 1, false, lgcode).ConfigureAwait(false);
    }

    [Command("fixOTList")]
    [Alias("fl", "fq")]
    [Summary("Prints the users in the FixOT queue.")]
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
    [Summary("Makes the bot trade you a Ditto with a requested stat spread and language.")]
    public async Task DittoTrade([Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
    {
        var code = Info.GetRandomTradeCode();
        await DittoTrade(code, keyword, language, nature).ConfigureAwait(false);
    }

    [Command("dittoTrade")]
    [Alias("dt", "ditto")]
    [Summary("Makes the bot trade you a Ditto with a requested stat spread and language.")]
    public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("Una combinación de \"ATK/SPA/SPE\" or \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
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

        keyword = keyword.ToLower().Trim();
        if (Enum.TryParse(language, true, out LanguageID lang))
            language = lang.ToString();
        else
        {
            await Context.Message.ReplyAsync($"<a:warning:1206483664939126795> No pude reconocer el idioma solicitado: {language}.").ConfigureAwait(false);
            return;
        }

        nature = nature.Trim()[..1].ToUpper() + nature.Trim()[1..].ToLower();
        var set = new ShowdownSet($"{keyword}(Ditto)\nLanguage: {language}\nNature: {nature}");
        var template = AutoLegalityWrapper.GetTemplate(set);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        var pkm = sav.GetLegal(template, out var result);
        AbstractTrade<T>.DittoTrade((T)pkm);

        var la = new LegalityAnalysis(pkm);

        if (pkm is not T pk || !la.Valid)
        {
            var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
            var imsg = $"<a:warning:1206483664939126795> Oops! {reason} Aquí está mi mejor intento para ese **Ditto**!";
            await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
            return;
        }

        pk.ResetPartyStats();
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
    }

    [Command("itemTrade")]
    [Alias("it", "item")]
    [Summary("Makes the bot trade you a Pokémon holding the requested item, or Ditto if stat spread keyword is provided.")]
    public async Task ItemTrade([Remainder] string item)
    {
        var code = Info.GetRandomTradeCode();
        await ItemTrade(code, item).ConfigureAwait(false);
    }

    [Command("itemTrade")]
    [Alias("it", "item")]
    [Summary("Makes the bot trade you a Pokémon holding the requested item.")]
    public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
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

        Species species = Info.Hub.Config.Trade.TradeConfiguration.ItemTradeSpecies == Species.None ? Species.Diglett : Info.Hub.Config.Trade.TradeConfiguration.ItemTradeSpecies;
        var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
        var template = AutoLegalityWrapper.GetTemplate(set);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        var pkm = sav.GetLegal(template, out var result);
        pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
        if (pkm.HeldItem == 0)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, el item que has solicitado no ha sido reconocido.").ConfigureAwait(false);
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
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
    }

    [Command("tradeList")]
    [Alias("tl")]
    [Summary("Prints the users in the trade queues.")]
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
    [Summary("Trades an egg generated from the provided Pokémon name.")]
    public async Task TradeEgg([Remainder] string egg)
    {
        var code = Info.GetRandomTradeCode();
        await TradeEggAsync(code, egg).ConfigureAwait(false);
    }

    [Command("egg")]
    [Alias("Egg")]
    [Summary("Trades an egg generated from the provided Pokémon name.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeEggAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
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

        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);

        try
        {
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;

            if (pkm is not T pk)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Oops! {Context.User.Mention}, No pude crear un huevo con el pokemon solicitado.").ConfigureAwait(false);
                return;
            }

            // Use the EggTrade method without setting the nickname
            pk.IsNicknamed = false; // Make sure we don't set a nickname
            AbstractTrade<T>.EggTrade(pk, template);

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Se produjo un error al procesar la solicitud.").ConfigureAwait(false);
        }
    }

    [Command("mysteryegg")]
    [Alias("me")]
    [Summary("Trades a random mystery egg with perfect stats and shiny appearance.")]
    public async Task TradeMysteryEggAsync()
    {
        var code = Info.GetRandomTradeCode();
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
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var speciesList = BreedableSpeciesGenerator.GetBreedableSpeciesForSV();
            var randomIndex = new Random().Next(speciesList.Count);
            ushort speciesId = speciesList[randomIndex];
            var context = new EntityContext();
            var IsEgg = new EncounterEgg(speciesId, 0, 1, 9, GameVersion.SV, context);
            var pk = IsEgg.ConvertToPKM(sav);
            TradeModule<T>.SetPerfectIVsAndShiny(pk);

            if (pk is not T pkT)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Oops! {Context.User.Mention}, no pude crear el huevo misterioso.").ConfigureAwait(false);
                return;
            }

            AbstractTrade<T>.EggTrade(pkT, null);

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pkT, sig, Context.User, isMysteryEgg: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, se produjo un error al procesar la solicitud.").ConfigureAwait(false);
        }
    }

    private static void SetPerfectIVsAndShiny(PKM pk)
    {
        // Set IVs to perfect
        pk.IVs = [31, 31, 31, 31, 31, 31];
        // Set as shiny
        pk.SetShiny();
        // Set hidden ability
        pk.RefreshAbility(2);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you the provided Pokémon file.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsyncAttach([Summary("Trade Code")] int code)
    {
        var sig = Context.User.GetFavor();
        return TradeAsyncAttach(code, sig, Context.User);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
        var lgcode = Info.GetRandomLGTradeCode();
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
        var set = new ShowdownSet(content);
        var template = AutoLegalityWrapper.GetTemplate(set);
        int formArgument = ExtractFormArgument(content);
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

        try
        {
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.SuggestRelearnMoves)
            {
                if (pkm is ITechRecord tr)
                {
                    tr.SetRecordFlagsAll();
                }
            }
            // Check if the Pokémon is from "Legends: Arceus"
            bool isLegendsArceus = pkm.Version == GameVersion.PLA;

            if (!isLegendsArceus && pkm.HeldItem == 0 && !pkm.IsEgg)
            {
                pkm.HeldItem = (int)SysCord<T>.Runner.Config.Trade.TradeConfiguration.DefaultHeldItem;
            }
            else if (isLegendsArceus)
            {
                pkm.HeldItem = (int)HeldItem.None; // Set to None for "Legends: Arceus" Pokémon
            }
            if (pkm is PB7)
            {
                if (pkm.Species == (int)Species.Mew)
                {
                    if (pkm.IsShiny)
                    {
                        await ReplyAsync($"<a:warning:1206483664939126795> Lo siento {Context.User.Mention}, Mew **no** puede ser Shiny en LGPE. PoGo Mew no se transfiere y Pokeball Plus Mew tiene shiny lock.");
                        return;
                    }
                }
            }
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? $"Este **{spec}** tomó demasiado tiempo en generarse." : result == "VersionMismatch" ? "Solicitud denegada: Las versiones de **PKHeX** y **Auto-Legality Mod** no coinciden." : $"{Context.User.Mention} No se puede crear un **{spec}** con los datos proporcionados.";
                var errorMessage = $"<a:no:1206485104424128593> Oops! {reason}";
                if (result == "Failed")
                    errorMessage += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";

                var embed = new EmbedBuilder
                {
                    Description = errorMessage,
                    Color = Color.Red,
                    ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                    ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
                };

                embed.WithAuthor("Error en la Legalidad del Conjunto", "https://img.freepik.com/free-icon/warning_318-478601.jpg")
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
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, isBatchTrade: false, batchTradeNumber: 1, totalBatchTrades: 1, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            var msg = $"<a:warning:1206483664939126795> ¡Oops! Ocurrió un problema inesperado con este conjunto de showdown:\n```{string.Join("\n", set.GetSetLines())}```";

            await Task.Delay(2000);
            await Context.Message.DeleteAsync();
        }
        _ = Task.Delay(2000).ContinueWith(async _ => await Context.Message.DeleteAsync());
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsync(code, content);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("Makes the bot trade you the attached file.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeAsyncAttach()
    {
        var code = Info.GetRandomTradeCode();
        var sig = Context.User.GetFavor();

        await TradeAsyncAttach(code, sig, Context.User);
    }

    private static int ExtractFormArgument(string content)
    {
        var match = Regex.Match(content, @"\.FormArgument=(\d+)");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return 0;
    }

    [Command("batchTrade")]
    [Alias("bt")]
    [Summary("Makes the bot trade multiple Pokémon from the provided list, up to a maximum of 3 trades.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeAsync([Summary("Lista de conjuntos de showdowns separados por '---'")][Remainder] string content)
    {
        // First, check if batch trades are allowed
        if (!SysCord<T>.Runner.Config.Trade.TradeConfiguration.AllowBatchTrades)
        {
            await ReplyAsync("<a:warning:1206483664939126795> Los intercambios por lotes están actualmente deshabilitados.").ConfigureAwait(false);
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

        var trades = TradeModule<T>.ParseBatchTradeContent(content);
        var maxTradesAllowed = SysCord<T>.Runner.Config.Trade.TradeConfiguration.MaxPkmsPerTrade;

        // Check if batch mode is allowed and if the number of trades exceeds the limit
        if (maxTradesAllowed < 1 || trades.Count > maxTradesAllowed)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} Sólo puedes procesar hasta **{maxTradesAllowed}** trades a la vez. Por favor, reduzca el número de operaciones en su lote").ConfigureAwait(false);

            await Task.Delay(5000);
            await Context.Message.DeleteAsync();
            return;
        }
        // Check if the number of trades exceeds the limit
        if (trades.Count > maxTradesAllowed)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Sólo puede procesar hasta {maxTradesAllowed} trades a la vez. Por favor, reduzca el número de operaciones en su lote.").ConfigureAwait(false);

            await Task.Delay(2000);
            await Context.Message.DeleteAsync();
            return;
        }

        var batchTradeCode = Info.GetRandomTradeCode();
        int batchTradeNumber = 1;
        _ = Task.Delay(2000).ContinueWith(async _ => await Context.Message.DeleteAsync());

        foreach (var trade in trades)
        {
            await ProcessSingleTradeAsync(trade, batchTradeCode, true, batchTradeNumber, trades.Count); // Pass the total number of trades here
            batchTradeNumber++;
        }
    }

    private static List<string> ParseBatchTradeContent(string content)
    {
        var delimiters = new[] { "---", "—-" }; // Includes both three hyphens and an em dash followed by a hyphen
        var trades = content.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                            .Select(trade => trade.Trim())
                            .ToList();
        return trades;
    }

    [Command("batchtradezip")]
    [Alias("btz")]
    [Summary("Makes the bot trade multiple Pokémon from the provided .zip file, up to a maximum of 6 trades.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeZipAsync()
    {
        var batchTradeCode = Info.GetRandomTradeCode();
        await BatchTradeZipAsync(batchTradeCode).ConfigureAwait(false);
    }

    [Command("batchtradezip")]
    [Alias("btz")]
    [Summary("Makes the bot trade multiple Pokémon from the provided .zip file, up to a maximum of 6 trades.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BatchTradeZipAsync([Summary("Trade Code")] int code)
    {
        // First, check if batch trades are allowed
        if (!SysCord<T>.Runner.Config.Trade.TradeConfiguration.AllowBatchTrades)
        {
            await ReplyAsync($"<a:no:1206485104424128593> {Context.User.Mention} Los intercambios por lotes están actualmente deshabilitados.").ConfigureAwait(false);
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
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se proporciona ningún archivo adjunto!").ConfigureAwait(false);
            return;
        }

        if (!attachment.Filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} Formato de archivo inválido. Proporcione un archivo `.zip.`").ConfigureAwait(false);
            return;
        }

        var zipBytes = await new HttpClient().GetByteArrayAsync(attachment.Url);
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = archive.Entries.ToList();
        var maxTradesAllowed = 6; // for full team in the zip created

        // Check if batch mode is allowed and if the number of trades exceeds the limit
        if (maxTradesAllowed < 1 || entries.Count > maxTradesAllowed)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} Sólo puedes procesar hasta {maxTradesAllowed} operaciones a la vez. Por favor, reduce el número de Pokémon en tu archivo `.zip`.").ConfigureAwait(false);

            await Task.Delay(5000);
            await Context.Message.DeleteAsync();
            return;
        }

        int batchTradeNumber = 1;
        _ = Task.Delay(2000).ContinueWith(async _ => await Context.Message.DeleteAsync());

        foreach (var entry in entries)
        {
            using var entryStream = entry.Open();
            var pkBytes = await TradeModule<T>.ReadAllBytesAsync(entryStream).ConfigureAwait(false);
            var pk = EntityFormat.GetFromBytes(pkBytes);

            if (pk is T)
            {
                await ProcessSingleTradeAsync((T)pk, code, true, batchTradeNumber, entries.Count);
                batchTradeNumber++;
            }
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    private async Task ProcessSingleTradeAsync(T pk, int batchTradeCode, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades)
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

            pk.ResetPartyStats();

            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();

            // Add the trade to the queue
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(batchTradeCode, Context.User.Username, pk, sig, Context.User, isBatchTrade, batchTradeNumber, totalBatchTrades, lgcode: lgcode, tradeType: PokeTradeType.Batch).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
        }
    }

    private async Task ProcessSingleTradeAsync(string tradeContent, int batchTradeCode, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades)
    {
        // Strip any code block formatting and parse the Showdown set
        tradeContent = ReusableActions.StripCodeBlock(tradeContent);
        var set = new ShowdownSet(tradeContent);

        // Get the template for the Pokémon
        var template = AutoLegalityWrapper.GetTemplate(set);

        // Handle invalid lines (if any)
        if (set.InvalidLines.Count != 0)
        {
            var msg = $"No se puede analizar el conjunto showdown:\n{string.Join("\n", set.InvalidLines)}";
            await ReplyAsync(msg).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get the trainer information and generate the Pokémon
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.SuggestRelearnMoves)
            {
                if (pkm is ITechRecord tr)
                    tr.SetRecordFlagsAll();
            }
            // Perform legality analysis
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;

            if (pkm is not T pk || !la.Valid)
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

            pk.ResetPartyStats();

            // Use a predefined or random trade code
            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();

            // Add the trade to the queue
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(batchTradeCode, Context.User.Username, pk, sig, Context.User, isBatchTrade, batchTradeNumber, totalBatchTrades, lgcode: lgcode, tradeType: PokeTradeType.Batch).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
        }
    }

    [Command("specialrequestpokemon")]
    [Alias("srp")]
    [Summary("Lists available wondercard events from the specified generation or game and sends the list via DM.")]
    public async Task ListSpecialEventsAsync(string generationOrGame, [Remainder] string args = "")
    {
        const int itemsPerPage = 25; // discord limit
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        int page = 1;
        var parts = args.Split(separatorArray0, StringSplitOptions.RemoveEmptyEntries);

        var pagePart = parts.FirstOrDefault(p => p.StartsWith("page", StringComparison.OrdinalIgnoreCase));
        if (pagePart != null)
        {
            if (int.TryParse(pagePart.AsSpan(4), out int pageNumber))
            {
                page = pageNumber;
            }
        }
        else if (parts.Length > 0 && int.TryParse(parts.Last(), out int parsedPage))
        {
            page = parsedPage;
        }

        MysteryGift[] eventData;

        switch (generationOrGame.ToLowerInvariant())
        {
            case "3":
            case "gen3":
                eventData = EncounterEvent.MGDB_G3;
                break;
            case "4":
            case "gen4":
                eventData = EncounterEvent.MGDB_G4;
                break;
            case "5":
            case "gen5":
                eventData = EncounterEvent.MGDB_G5;
                break;
            case "6":
            case "gen6":
                eventData = EncounterEvent.MGDB_G6;
                break;
            case "7":
            case "gen7":
                eventData = EncounterEvent.MGDB_G7;
                break;
            case "gg":
            case "lgpe":
                eventData = EncounterEvent.MGDB_G7GG;
                break;
            case "swsh":
                eventData = EncounterEvent.MGDB_G8;
                break;
            case "pla":
            case "la":
                eventData = EncounterEvent.MGDB_G8A;
                break;
            case "bdsp":
                eventData = EncounterEvent.MGDB_G8B;
                break;
            case "9":
            case "gen9":
                eventData = EncounterEvent.MGDB_G9;
                break;
            default:
                await ReplyAsync($"<a:warning:1206483664939126795> Generación o juego no válido: {generationOrGame}");
                return;
        }

        // get and filter event data
        var allEvents = eventData
            .Where(gift => gift.IsEntity && !gift.IsItem)
            .Select(gift =>
            {
                string speciesName = GameInfo.Strings.Species[gift.Species];
                string levelInfo = $"(Lv. {gift.Level})";
                string formName = ShowdownParsing.GetStringFromForm(gift.Form, GameInfo.Strings, gift.Species, gift.Context);
                formName = !string.IsNullOrEmpty(formName) ? $"-{formName}" : formName;
                return new
                {
                    CardNumber = gift.CardID,
                    EventInfo = $"{gift.CardHeader} - {speciesName}{formName} {levelInfo}"
                };
            })
            .ToList();

        var groupedEvents = allEvents
            .Select((evt, index) => new { Index = index + 1, evt.CardNumber, evt.EventInfo })
            .ToList();

        IUserMessage replyMessage;

        if (groupedEvents.Count == 0)
        {
            replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron eventos para: {generationOrGame}.");
        }
        else
        {
            var pageCount = (int)Math.Ceiling(groupedEvents.Count / (double)itemsPerPage);
            page = Math.Clamp(page, 1, pageCount); // make sure page number is in range

            var pageItems = groupedEvents.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);

            var embed = new EmbedBuilder()
                .WithTitle($"📝 Eventos Disponibles - {generationOrGame.ToUpperInvariant()}")
                .WithDescription($"Pagina {page} de {pageCount}")
                .WithColor(Color.Blue);

            foreach (var item in pageItems)
            {
                embed.AddField($"{item.Index}. {item.EventInfo}", $"Usa `{botPrefix}srp {generationOrGame} {item.Index}` en el canal correspondiente para solicitar este evento.");
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
                    replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("<a:warning:1206483664939126795> **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de Privacidad del Servidor**.");
            }
        }

        await Task.Delay(10_000);
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
        await replyMessage.DeleteAsync().ConfigureAwait(false);
    }

    [Command("specialrequestpokemon")]
    [Alias("srp")]
    [Summary("Downloads wondercard event attachments from the specified generation and adds to trade queue.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task SpecialEventRequestAsync(string generationOrGame, int index)
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
            MysteryGift[] eventData;

            switch (generationOrGame.ToLowerInvariant())
            {
                case "3":
                case "gen3":
                    eventData = EncounterEvent.MGDB_G3;
                    break;
                case "4":
                case "gen4":
                    eventData = EncounterEvent.MGDB_G4;
                    break;
                case "5":
                case "gen5":
                    eventData = EncounterEvent.MGDB_G5;
                    break;
                case "6":
                case "gen6":
                    eventData = EncounterEvent.MGDB_G6;
                    break;
                case "7":
                case "gen7":
                    eventData = EncounterEvent.MGDB_G7;
                    break;
                case "gg":
                case "lgpe":
                    eventData = EncounterEvent.MGDB_G7GG;
                    break;
                case "swsh":
                    eventData = EncounterEvent.MGDB_G8;
                    break;
                case "pla":
                case "la":
                    eventData = EncounterEvent.MGDB_G8A;
                    break;
                case "bdsp":
                    eventData = EncounterEvent.MGDB_G8B;
                    break;
                case "9":
                case "gen9":
                    eventData = EncounterEvent.MGDB_G9;
                    break;
                default:
                    await ReplyAsync($"<a:warning:1206483664939126795> Generación o juego no válido: {generationOrGame}");
                    return;
            }

            var entityEvents = eventData
                .Where(gift => gift.IsEntity && !gift.IsItem)
                .ToArray();

            if (index < 1 || index > entityEvents.Length)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Índice de eventos no válido. Utilice un número de evento válido del comando `{SysCord<T>.Runner.Config.Discord.CommandPrefix}srp {generationOrGame}`");
                return;
            }

            var selectedEvent = entityEvents[index - 1];

            var download = new Download<PKM>
            {
                Data = selectedEvent.ConvertToPKM(new SimpleTrainerInfo(), EncounterCriteria.Unrestricted),
                Success = true
            };

            if (download.Data is null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> No se pudieron convertir los datos de Wondercard al tipo PKM requerido.");
                return;
            }

            var pk = GetRequest(download);

            if (pk is null)
            {
                await ReplyAsync("<a:warning:1206483664939126795> Los datos de Wondercard proporcionados no son compatibles con este módulo!");
                return;
            }

            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();
            await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention} Solicitud de evento especial agregada a la cola.");
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"<a:yes:1206485105674166292> Ocurrió un error: {ex.Message}");
        }
    }

    [Command("listevents")]
    [Alias("le")]
    [Summary("Lists available event files, filtered by a specific letter or substring, and sends the list via DM.")]
    public async Task ListEventsAsync([Remainder] string args = "")
    {
        const int itemsPerPage = 20; // Number of items per page
        var eventsFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.EventsFolder;
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        // Check if the events folder path is not set or empty
        if (string.IsNullOrEmpty(eventsFolderPath))
        {
            await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.");
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

        var filteredEventFiles = allEventFiles
                                 .Where(file => string.IsNullOrWhiteSpace(filter) || file.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                 .ToList();

        IUserMessage replyMessage;

        // Check if there are no files matching the filter
        if (!filteredEventFiles.Any())
        {
            replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention} No se encontraron eventos que coincidan con el filtro '{filter}'.");
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
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, Te envié un DM con la lista de eventos.");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    // This exception is thrown when the bot cannot send DMs to the user
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("<a:warning:1206483664939126795> **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
            }
        }

        await Task.Delay(10_000);
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
        await replyMessage.DeleteAsync().ConfigureAwait(false);
    }

    [Command("eventrequest")]
    [Alias("er")]
    [Summary("Downloads event attachments from the specified EventsFolder and adds to trade queue.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task EventRequestAsync(int index)
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
            var eventsFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.EventsFolder;
            var eventFiles = Directory.GetFiles(eventsFolderPath)
                                      .Select(Path.GetFileName)
                                      .OrderBy(x => x)
                                      .ToList();

            // Check if the events folder path is not set or empty
            if (string.IsNullOrEmpty(eventsFolderPath))
            {
                await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.");
                return;
            }

            if (index < 1 || index > eventFiles.Count)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Índice de eventos no válido. Utilice un número de evento válido mostrado en la lista que te envie al MD cuando usaste el comando `.le`.").ConfigureAwait(false);
                return;
            }

            var selectedFile = eventFiles[index - 1]; // Adjust for zero-based indexing
            var fileData = await File.ReadAllBytesAsync(Path.Combine(eventsFolderPath, selectedFile));

            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = GetRequest(download);
            if (pk == null)
            {
                await ReplyAsync("<a:warning:1206483664939126795> No se pudo convertir el archivo de eventos al tipo PKM requerido.").ConfigureAwait(false);
                return;
            }

            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();
            await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention} Evento solicitado, agregado a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Ocurrió un error: {ex.Message}").ConfigureAwait(false);
        }
    }

    [Command("battlereadylist")]
    [Alias("brl")]
    [Summary("Lists available battle-ready files, filtered by a specific letter or substring, and sends the list via DM.")]
    public async Task BattleReadyListAsync([Remainder] string args = "")
    {
        const int itemsPerPage = 20; // Number of items per page
        var battleReadyFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.BattleReadyPKMFolder;
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        // Check if the battleready folder path is not set or empty
        if (string.IsNullOrEmpty(battleReadyFolderPath))
        {
            await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.");
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

        var filteredBattleReadyFiles = allBattleReadyFiles
                                       .Where(file => string.IsNullOrWhiteSpace(filter) || file.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                       .ToList();

        IUserMessage replyMessage;

        // Check if there are no files matching the filter
        if (!filteredBattleReadyFiles.Any())
        {
            replyMessage = await ReplyAsync($"<a:warning:1206483664939126795> No se encontraron archivos listos para la batalla que coincidan con el filtro. '{filter}'.");
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
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, Te envié un DM con la lista de archivos pokemon listos para batalla.");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    // This exception is thrown when the bot cannot send DMs to the user
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("<a:warning:1206483664939126795> **Error**: No se puede enviar un MD. Por favor verifique su **Configuración de privacidad del servidor**.");
            }
        }

        await Task.Delay(10_000);
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
        await replyMessage.DeleteAsync().ConfigureAwait(false);
    }

    [Command("battlereadyrequest")]
    [Alias("brr", "br")]
    [Summary("Downloads battle-ready attachments from the specified folder and adds to trade queue.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTradePlus))]
    public async Task BattleReadyRequestAsync(int index)
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
            var battleReadyFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.BattleReadyPKMFolder;
            var battleReadyFiles = Directory.GetFiles(battleReadyFolderPath)
                                            .Select(Path.GetFileName)
                                            .OrderBy(x => x)
                                            .ToList();

            // Check if the battleready folder path is not set or empty
            if (string.IsNullOrEmpty(battleReadyFolderPath))
            {
                await ReplyAsync($"<a:no:1206485104424128593> Lo siento {Context.User.Mention}, Este bot no tiene esta función configurada.");
                return;
            }

            if (index < 1 || index > battleReadyFiles.Count)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, Índice de archivos listos para la batalla no válido. Utilice un número de archivo mostrado en la lista que te envie al MD cuando usaste el comando `.blr`.").ConfigureAwait(false);
                return;
            }

            var selectedFile = battleReadyFiles[index - 1];
            var fileData = await File.ReadAllBytesAsync(Path.Combine(battleReadyFolderPath, selectedFile));

            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = GetRequest(download);
            if (pk == null)
            {
                await ReplyAsync("<a:warning:1206483664939126795> No se pudo convertir el archivo listo para batalla al tipo PKM requerido.").ConfigureAwait(false);
                return;
            }

            var code = Info.GetRandomTradeCode();
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = Context.User.GetFavor();
            await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, solicitud de Pokemon listo para batalla agregada a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Ocurrió un error: {ex.Message}").ConfigureAwait(false);
        }
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Makes the bot trade the mentioned user the attached file.")]
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
        await TradeAsyncAttach(code, sig, usr).ConfigureAwait(false);
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("Makes the bot trade the mentioned user the attached file.")]
    [RequireSudo]
    public Task TradeAsyncAttachUser([Remainder] string _)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttachUser(code, _);
    }

    private async Task TradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr)
    {
        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == default)
        {
            await ReplyAsync("<a:warning:1206483664939126795> No se proporcionó ningún archivo adjunto!").ConfigureAwait(false);
            return;
        }

        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            await ReplyAsync("<a:warning:1206483664939126795> ¡El archivo adjunto proporcionado no es compatible con este módulo!").ConfigureAwait(false);
            return;
        }

        await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
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

    private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isMysteryEgg = false, List<Pictocodes> lgcode = null, PokeTradeType tradeType = PokeTradeType.Specific)
    {
        lgcode ??= TradeModule<T>.GenerateRandomPictocodes(3);
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
        if (!la.Valid)
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

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, tradeType, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, isMysteryEgg, lgcode).ConfigureAwait(false);
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
