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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SysBot.Pokemon.TradeSettings;

namespace SysBot.Pokemon.Discord;

[Summary("Queues new Link Code trades")]
public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("listguilds")]
    [Alias("lg", "servers", "listservers")]
    [Summary("Lists all guilds the bot is part of.")]
    [RequireSudo]
    public async Task ListGuilds(int page = 1)
    {
        const int guildsPerPage = 25; // Discord limit for fields in an embed
        int guildCount = Context.Client.Guilds.Count;
        int totalPages = (int)Math.Ceiling(guildCount / (double)guildsPerPage);
        page = Math.Max(1, Math.Min(page, totalPages)); // Ensure page is within range

        var guilds = Context.Client.Guilds
            .Skip((page - 1) * guildsPerPage)
            .Take(guildsPerPage);

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"Lista de Servidores - Pagina {page}/{totalPages}")
            .WithDescription("📝 Aquí están los servidores en los que estoy actualmente.:")
            .WithColor(Color.Blue); // Choose an appropriate color

        foreach (var guild in guilds)
        {
            embedBuilder.AddField(guild.Name, $"ID: {guild.Id}", inline: true);
        }

        // Send the embed via DM
        var dmChannel = await Context.User.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync(embed: embedBuilder.Build());

        // Optionally, confirm to the user in the channel that the DM has been sent
        await ReplyAsync($"{Context.User.Mention}, Te envié un DM con la lista de gremios. (Pagina {page}).");

        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000); // Wait for 2 seconds
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
            await ReplyAsync("⚠️ Este servidor ya está en la lista negra..");
            return;
        }

        var server = Context.Client.GetGuild(serverId);
        if (server == null)
        {
            await ReplyAsync("⚠️ No se puede encontrar un servidor con el ID proporcionado. Asegúrese de que el bot sea miembro del servidor que desea incluir en la lista negra.");
            return;
        }

        var newServerAccess = new RemoteControlAccess { ID = serverId, Name = server.Name, Comment = "Servidor en lista negra" };

        settings.ServerBlacklist.AddIfNew(new[] { newServerAccess });

        await server.LeaveAsync();
        await ReplyAsync($"✔ Deje el servidor '{server.Name}' y lo agregue a la lista negra.");
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
            await ReplyAsync("⚠️ Este servidor no está actualmente en la lista negra.");
            return;
        }

        var wasRemoved = settings.ServerBlacklist.RemoveAll(x => x.ID == serverId) > 0;

        if (wasRemoved)
        {
            await ReplyAsync($"✔ El servidor con ID {serverId} ha sido eliminado de la lista negra.");
        }
        else
        {
            await ReplyAsync("⚠️ Se produjo un error al intentar eliminar el servidor de la lista negra. Verifique la ID del servidor e inténtelo nuevamente.");
        }
    }

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT()
    {
        var code = Info.GetRandomTradeCode();
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        // Delete the command message after 2 seconds
        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000); // Wait for 2 seconds
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    [Command("fixOT")]
    [Alias("fix", "f")]
    [Summary("Fixes OT and Nickname of a Pokémon you show via Link Trade if an advert is detected.")]
    [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
    public async Task FixAdOT([Summary("Trade Code")] int code)
    {
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        // Delete the command message after 2 seconds
        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000); // Wait for 2 seconds
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
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
        // Delete the command message after 2 seconds
        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000); // Wait for 2 seconds
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    [Command("dittoTrade")]
    [Alias("dt", "ditto")]
    [Summary("Makes the bot trade you a Ditto with a requested stat spread and language.")]
    public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
    {
        keyword = keyword.ToLower().Trim();
        if (Enum.TryParse(language, true, out LanguageID lang))
            language = lang.ToString();
        else
        {
            await Context.Message.ReplyAsync($"⚠️ No pude reconocer el idioma solicitado: {language}.").ConfigureAwait(false);
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
            var imsg = $"⚠️ Oops! {reason} Aquí está mi mejor intento para ese **Ditto**!";
            await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
            return;
        }

        pk.ResetPartyStats();
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        // Delete the command message after 2 seconds
        if (Context.Message is IUserMessage userMessage)
        {
            await Task.Delay(2000); // Wait for 2 seconds
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
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
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task TradeEggAsync([Summary("Showdown Set")][Remainder] string content)
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            var currentTime = DateTime.UtcNow;
            var formattedTime = currentTime.ToString("hh:mm tt");

            var queueEmbed = new EmbedBuilder
            {
                Description = $"<:red:1206057066146308128> {Context.User.Mention}, ya tienes una operación existente en la cola. Espere hasta que se procese.",
                Color = Color.Red,
                ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif");

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
                await ReplyAsync($"⚠️ Oops! {Context.User.Mention}, No pude crear un huevo con el pokemon solicitado.").ConfigureAwait(false);
                return;
            }

            // Use the EggTrade method without setting the nickname
            pk.IsNicknamed = false; // Make sure we don't set a nickname
            AbstractTrade<T>.EggTrade(pk, template);

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);

            // Delete the command message after 2 seconds
            if (Context.Message is IUserMessage userMessage)
            {
                await Task.Delay(2000); // Wait for 2 seconds
                await userMessage.DeleteAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            await ReplyAsync($"⚠️ {Context.User.Mention}, Se produjo un error al procesar la solicitud.").ConfigureAwait(false);
        }
    }

    [Command("mysteryegg")]
    [Alias("me")]
    [Summary("Trades a random mystery egg with perfect stats and shiny appearance.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
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
                Description = $"<:red:1206057066146308128> {Context.User.Mention}, ya tienes una operación existente en la cola. Espere hasta que se procese.",
                Color = Color.Red,
                ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif");

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
            var randomIndex = new Random().Next(speciesList.Count); // Standard way to pick a random index
            ushort speciesId = speciesList[randomIndex];

            var context = new EntityContext(); 
            var eggEncounter = new EncounterEgg(speciesId, 0, 1, 9, GameVersion.SV, context);

            var pk = eggEncounter.ConvertToPKM(sav);

            SetPerfectIVsAndShiny(pk); // Method to set IVs and shiny status

            if (pk is not T pkT)
            {
                await ReplyAsync("⚠️ Oops! {Context.User.Mention}, no pude crear el huevo misterioso.").ConfigureAwait(false);
                return;
            }

            AbstractTrade<T>.EggTrade(pkT, null); // Adjust as needed

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pkT, sig, Context.User, isMysteryEgg: true).ConfigureAwait(false);

            // Delete the command message after 2 seconds
            if (Context.Message is IUserMessage userMessage)
            {
                await Task.Delay(2000); // Wait for 2 seconds
                await userMessage.DeleteAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            await ReplyAsync($"⚠️ {Context.User.Mention}, se produjo un error al procesar la solicitud.").ConfigureAwait(false);
        }

    }

    private void SetPerfectIVsAndShiny(PKM pk)
    {
        // Set IVs to perfect
        pk.IVs = new int[] { 31, 31, 31, 31, 31, 31 };

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
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            var currentTime = DateTime.UtcNow;
            var formattedTime = currentTime.ToString("hh:mm tt");

            var queueEmbed = new EmbedBuilder
            {
                Description = $"<:red:1206057066146308128> {Context.User.Mention}, ya tienes una operación existente en la cola. Espere hasta que se procese.",
                Color = Color.Red,
                ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif");

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
                Description = $"No se puede analizar el conjunto showdown:\n{invalidLines}",
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
            // Delete the user's message after sending the reply
            await Task.Delay(2000); // Delay for 2 seconds
            await Context.Message.DeleteAsync(); // Delete the message
            return;
        }

        try
        {
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);

            // Check if the Pokémon is from "Legends: Arceus"
            bool isLegendsArceus = pkm.Version == (int)GameVersion.PLA;

            // Set default held item only if it's not from "Legends: Arceus"
            if (!isLegendsArceus && pkm.HeldItem == 0 && !pkm.IsEgg)
            {
                pkm.HeldItem = (int)SysCord<T>.Runner.Config.Trade.TradeConfiguration.DefaultHeldItem;
            }
            else if (isLegendsArceus)
            {
                pkm.HeldItem = (int)TradeSettingsCategory.HeldItem.None; // Set to None for "Legends: Arceus" Pokémon
            }

            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? $"Este **{spec}** tomó demasiado tiempo en generarse." : result == "VersionMismatch" ? "Solicitud denegada: Las versiones de **PKHeX** y **Auto-Legality Mod** no coinciden." : $"{Context.User.Mention} No se puede crear un **{spec}** con los datos proporcionados.";
                var errorMessage = $"<:red:1206057066146308128> Oops! {reason}";
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
                // Delete the user's message after sending the reply
                await Task.Delay(2000); // Delay for 2 seconds
                await Context.Message.DeleteAsync(); // Delete the message
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, isBatchTrade: false, batchTradeNumber: 1, totalBatchTrades: 1, formArgument: formArgument).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            // Delete the user's message if there's an exception
            await Task.Delay(2000); // Delay for 2 seconds
            await Context.Message.DeleteAsync(); // Delete the message
        }

        // Schedule the deletion of the user's command message after 2 seconds
        _ = Task.Delay(2000).ContinueWith(async _ => await Context.Message.DeleteAsync());
    }

    private static int ExtractFormArgument(string content)
    {
        var match = Regex.Match(content, @"\.FormArgument=(\d+)");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return 0; // Default or fallback value
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

        // Call TradeAsyncAttach with the generated code
        await TradeAsyncAttach(code);

        // Delay for 2 seconds
        await Task.Delay(2000);

        // Delete the user's command message
        if (Context.Message is IUserMessage userMessage)
        {
            await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }

    [Command("batchTrade")]
    [Alias("bt")]
    [Summary("Makes the bot trade multiple Pokémon from the provided list, up to a maximum of 3 trades.")]
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task BatchTradeAsync([Summary("Lista de conjuntos de showdowns separados por '---'")][Remainder] string content)
    {
        // First, check if batch trades are allowed
        if (!SysCord<T>.Runner.Config.Trade.TradeConfiguration.AllowBatchTrades)
        {
            await ReplyAsync("⚠️ Los intercambios por lotes están actualmente deshabilitados.").ConfigureAwait(false);
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
                Description = $"<:red:1206057066146308128> {Context.User.Mention}, ya tienes una operación existente en la cola. Espere hasta que se procese.",
                Color = Color.Red,
                ImageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            queueEmbed.WithAuthor("Error al intentar agregarte a la lista", "https://i.imgur.com/0R7Yvok.gif")
                 .WithFooter(footer =>
                 {
                     footer.Text = $"{Context.User.Username} • {formattedTime}";
                     footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                 });

            await ReplyAsync(embed: queueEmbed.Build()).ConfigureAwait(false);
            return;
        }

        var trades = ParseBatchTradeContent(content);
        var maxTradesAllowed = SysCord<T>.Runner.Config.Trade.TradeConfiguration.MaxPkmsPerTrade;

        // Check if batch mode is allowed and if the number of trades exceeds the limit
        if (maxTradesAllowed < 1 || trades.Count > maxTradesAllowed)
        {
            await ReplyAsync($"⚠️ {Context.User.Mention} Sólo puedes procesar hasta **{maxTradesAllowed}** trades a la vez. Por favor, reduzca el número de operaciones en su lote").ConfigureAwait(false);
            // Delete the user's message after sending the reply
            await Task.Delay(5000); // Delay for 5 seconds
            await Context.Message.DeleteAsync(); // Delete the message
            return;
        }
        // Check if the number of trades exceeds the limit
        if (trades.Count > 3)
        {
            await ReplyAsync("⚠️ Sólo puede procesar hasta 3 trades a la vez. Por favor, reduzca el número de operaciones en su lote.").ConfigureAwait(false);
            // Delete the user's message after sending the reply
            await Task.Delay(2000); // Delay for 2 seconds
            await Context.Message.DeleteAsync(); // Delete the message
            return;
        }

        var batchTradeCode = Info.GetRandomTradeCode();
        int batchTradeNumber = 1;

        // Delete the user's command message after 2 seconds
        _ = Task.Delay(2000).ContinueWith(async _ => await Context.Message.DeleteAsync());

        foreach (var trade in trades)
        {
            await ProcessSingleTradeAsync(trade, batchTradeCode, true, batchTradeNumber, trades.Count); // Pass the total number of trades here
            batchTradeNumber++;
        }
    }

    private List<string> ParseBatchTradeContent(string content)
    {
        var delimiters = new[] { "---", "—-" }; // Includes both three hyphens and an em dash followed by a hyphen
        var trades = content.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                            .Select(trade => trade.Trim()) // Trims any whitespace from each trade string
                            .ToList();
        return trades;
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
            var currentTime = DateTime.UtcNow;
            var formattedTime = currentTime.ToString("hh:mm tt");

            var invalidLinesEmbed = new EmbedBuilder
            {
                Description = $"No se puede analizar el conjunto showdown:\n{string.Join("\n", set.InvalidLines)}",
                Color = Color.Red,
                ImageUrl = "https://i.imgur.com/Y64hLzW.gif",
                ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png"
            };

            invalidLinesEmbed.WithAuthor("Conjunto de enfrentamiento no válido", "https://i.imgur.com/0R7Yvok.gif")
                 .WithFooter(footer =>
                 {
                     footer.Text = $"{Context.User.Username} • {formattedTime}";
                     footer.IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl();
                 });

            await ReplyAsync(embed: invalidLinesEmbed.Build()).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get the trainer information and generate the Pokémon
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);

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

                var errorMessage = $"<:red:1206057066146308128> Oops! {reason}";
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

            // Add the trade to the queue
            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(batchTradeCode, Context.User.Username, pk, sig, Context.User, isBatchTrade, batchTradeNumber, totalBatchTrades).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
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
            replyMessage = await ReplyAsync($"⚠️ {Context.User.Mention} No se encontraron eventos que coincidan con el filtro '{filter}'.");
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
                replyMessage = await ReplyAsync("⚠️ **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
            }
        }

        // Auto-deletion of messages
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
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task EventRequestAsync(int index)
    {
        try
        {
            var eventsFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.EventsFolder;
            var eventFiles = Directory.GetFiles(eventsFolderPath)
                                      .Select(Path.GetFileName)
                                      .OrderBy(x => x)
                                      .ToList();

            if (index < 1 || index > eventFiles.Count)
            {
                await ReplyAsync($"⚠️ {Context.User.Mention}, Índice de eventos no válido. Utilice un número de evento válido mostrado en la lista que te envie al MD cuando usaste el comando `.le`.").ConfigureAwait(false);
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
                await ReplyAsync("⚠️ No se pudo convertir el archivo de eventos al tipo PKM requerido.").ConfigureAwait(false);
                return;
            }

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await ReplyAsync($"✓ {Context.User.Mention} Evento solicitado, agregado a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"⚠️ Ocurrió un error: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            if (Context.Message is IUserMessage userMessage)
            {
                await userMessage.DeleteAsync().ConfigureAwait(false);
            }
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
            replyMessage = await ReplyAsync($"⚠️ No se encontraron archivos listos para la batalla que coincidan con el filtro. '{filter}'.");
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
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, Te envié un DM con la lista de archivos pokemon listos para batalla..");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    // This exception is thrown when the bot cannot send DMs to the user
                    replyMessage = await ReplyAsync($"{Context.User.Mention}, No puedo enviarte un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
                }
            }
            else
            {
                replyMessage = await ReplyAsync("⚠️ **Error**: No se puede enviar un DM. Por favor verifique su **Configuración de privacidad del servidor**.");
            }
        }

        // Auto-deletion of messages
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
    [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
    public async Task BattleReadyRequestAsync(int index)
    {
        try
        {
            var battleReadyFolderPath = SysCord<T>.Runner.Config.Trade.RequestFolderSettings.BattleReadyPKMFolder;
            var battleReadyFiles = Directory.GetFiles(battleReadyFolderPath)
                                            .Select(Path.GetFileName)
                                            .OrderBy(x => x)
                                            .ToList();

            if (index < 1 || index > battleReadyFiles.Count)
            {
                await ReplyAsync($"⚠️ {Context.User.Mention}, Índice de archivos listos para la batalla no válido. Utilice un número dearchivo mostrado en la lista que te envie al MD cuando usaste el comando `.blr`.").ConfigureAwait(false);
                return;
            }

            var selectedFile = battleReadyFiles[index - 1]; // Adjust for zero-based indexing
            var fileData = await File.ReadAllBytesAsync(Path.Combine(battleReadyFolderPath, selectedFile));

            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = GetRequest(download);
            if (pk == null)
            {
                await ReplyAsync("⚠️ No se pudo convertir el archivo listo para batalla al tipo PKM requerido.").ConfigureAwait(false);
                return;
            }

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await ReplyAsync($"✓ {Context.User.Mention}, solicitud de Pokemon listo para batalla agregada a la cola.").ConfigureAwait(false);
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"⚠️ Ocurrió un error: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            if (Context.Message is IUserMessage userMessage)
            {
                await userMessage.DeleteAsync().ConfigureAwait(false);
            }
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
            await ReplyAsync("⚠️ Demasiadas menciones. Solo puedes agregar a la lista un usario a la vez.").ConfigureAwait(false);
            return;
        }

        if (Context.Message.MentionedUsers.Count == 0)
        {
            await ReplyAsync("⚠️ Un usuario debe ser mencionado para hacer esto.").ConfigureAwait(false);
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
            await ReplyAsync("⚠️ No se proporcionó ningún archivo adjunto!").ConfigureAwait(false);
            return;
        }

        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            await ReplyAsync("⚠️ ¡El archivo adjunto proporcionado no es compatible con este módulo!").ConfigureAwait(false);
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

    private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, int formArgument = 0, bool isMysteryEgg = false)
    {
        if (!pk.CanBeTraded())
        {
            var errorMessage = $"✘ {usr.Mention} revisa el conjunto enviado, algun dato esta bloqueando el intercambio.\n\n```📝Soluciones:\n• Revisa detenidamente cada detalle del conjunto y vuelve a intentarlo!```";
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

            string responseMessage = pk.IsEgg ? $"{usr.Mention} El conjunto de showdown no válido para este huevo. Por favor revisa tu información y vuelve a intentarlo." :
                $"✘ {usr.Mention} el archivo **{typeof(T).Name}** no es __legal__ y no puede ser tradeado.\n### He aquí la razón:\n```{legalityReport}```\n```🔊Consejo:\n• Por favor verifica detenidamente la informacion en PKHeX e intentalo de nuevo!\n• Puedes utilizar el plugin de ALM para legalizar tus pokemons y ahorrarte estos problemas.```";

            if (homeLegalityCfg.DisallowNonNatives && (la.EncounterOriginal.Context != pk.Context || pk.GO))
            {
                // Allow the owner to prevent trading entities that require a HOME Tracker even if the file has one already.
                var embedBuilder = new EmbedBuilder()
                    .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                    .WithDescription($"✘ {usr.Mention}, este archivo Pokemon **{typeof(T).Name}** no cuenta con un **HOME TRACKER** y no puede ser tradeado!")
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
                // Allow the owner to prevent trading entities that already have a HOME Tracker.
                var embedBuilder = new EmbedBuilder()
                    .WithAuthor("Error al intentar agregarte a la cola.", customIconUrl)
                    .WithDescription($"✘ {usr.Mention}, este archivo Pokemon **{typeof(T).Name}** ya tiene un **HOME TRACKER** y no puede ser tradeado!")
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
            await Task.Delay(10000); // Delay for 20 seconds
            await reply.DeleteAsync().ConfigureAwait(false);
            return;
        }

        // Pass all the necessary flags to the QueueHelper's AddToQueueAsync method
        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, formArgument, isMysteryEgg).ConfigureAwait(false);
    }

}
