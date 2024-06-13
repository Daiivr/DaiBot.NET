using AnimatedGif;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public class OwnerModule<T> : SudoModule<T> where T : PKM, new()
{
    [Command("listguilds")]
    [Alias("lg", "servers", "listservers")]
    [Summary("Enumera todos los servers de los que forma parte el bot.")]
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
            .WithColor((DiscordColor)Color.Blue);

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
    [Summary("Agrega una ID de servidor a la lista negra de servidores del bot.")]
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
        await ReplyAsync($"<a:yes:1206485105674166292> He dejado el servidor '{server.Name}' y lo agregue a la lista negra.");
    }

    [Command("unblacklistserver")]
    [Alias("ubls")]
    [Summary("Elimina una ID de servidor de la lista negra de servidores del bot.")]
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

    [Command("addSudo")]
    [Summary("Agrega el usuario mencionado al sudo global")]
    [RequireOwner]
    public async Task SudoUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.GlobalSudoList.AddIfNew(objects);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("removeSudo")]
    [Summary("Elimina el usuario mencionado del sudo global")]
    [RequireOwner]
    public async Task RemoveSudoUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.GlobalSudoList.RemoveAll(z => objects.Any(o => o.ID == z.ID));
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("addChannel")]
    [Summary("Agrega un canal a la lista de canales que aceptan comandos.")]
    [RequireOwner]
    public async Task AddChannel()
    {
        var obj = GetReference(Context.Message.Channel);
        SysCordSettings.Settings.ChannelWhitelist.AddIfNew([obj]);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("syncChannels")]
    [Alias("sch", "syncchannels")]
    [Summary("Copia todos los canales de la Lista blanca de canales al Canal de anuncios.")]
    [RequireOwner]
    public async Task SyncChannels()
    {
        var whitelist = SysCordSettings.Settings.ChannelWhitelist.List;
        var announcementList = SysCordSettings.Settings.AnnouncementChannels.List;

        bool changesMade = false;

        foreach (var channel in whitelist)
        {
            if (!announcementList.Any(x => x.ID == channel.ID))
            {
                announcementList.Add(channel);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            await ReplyAsync("<a:yes:1206485105674166292> La lista blanca de canales se ha sincronizado correctamente con los canales de anuncios.").ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync("<a:warning:1206483664939126795> Todos los canales de la lista blanca ya están en los canales de anuncios, no se realizaron cambios.").ConfigureAwait(false);
        }
    }

    [Command("removeChannel")]
    [Summary("Elimina un canal de la lista de canales que aceptan comandos.")]
    [RequireOwner]
    public async Task RemoveChannel()
    {
        var obj = GetReference(Context.Message.Channel);
        SysCordSettings.Settings.ChannelWhitelist.RemoveAll(z => z.ID == obj.ID);
        await ReplyAsync("<a:yes:1206485105674166292> Listo.").ConfigureAwait(false);
    }

    [Command("leave")]
    [Alias("bye")]
    [Summary("Abandona el servidor actual.")]
    [RequireOwner]
    public async Task Leave()
    {
        await ReplyAsync("Goodbye.").ConfigureAwait(false);
        await Context.Guild.LeaveAsync().ConfigureAwait(false);
    }

    [Command("leaveguild")]
    [Alias("lg")]
    [Summary("Abandona el servidor según la identificación proporcionada.")]
    [RequireOwner]
    public async Task LeaveGuild(string userInput)
    {
        if (!ulong.TryParse(userInput, out ulong id))
        {
            await ReplyAsync("<a:warning:1206483664939126795> Proporcione una identificación válida de servidor!").ConfigureAwait(false);
            return;
        }

        var guild = Context.Client.Guilds.FirstOrDefault(x => x.Id == id);
        if (guild is null)
        {
            await ReplyAsync($"<a:yes:1206485105674166292> La entrada proporcionada ({{userInput}}) no es un ID de server válido o el bot no está en el servidor especificado.").ConfigureAwait(false);
            return;
        }

        await ReplyAsync($"Leaving {guild}.").ConfigureAwait(false);
        await guild.LeaveAsync().ConfigureAwait(false);
    }

    [Command("leaveall")]
    [Summary("Deja todos los servidores en los que se encuentra actualmente el bot.")]
    [RequireOwner]
    public async Task LeaveAll()
    {
        await ReplyAsync("<a:yes:1206485105674166292> Abandonando todos los servidores.").ConfigureAwait(false);
        foreach (var guild in Context.Client.Guilds)
        {
            await guild.LeaveAsync().ConfigureAwait(false);
        }
    }

    [Command("repeek")]
    [Alias("peek")]
    [Summary("Toma y envia una captura de pantalla desde el Switch actualmente configurada.")]
    [RequireSudo]
    public async Task RePeek(string address)
    {
        var source = new CancellationTokenSource();
        var token = source.Token;

        var bot = SysCord<T>.Runner.GetBot(address);
        if (bot == null)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No se encontró ningún bot con la dirección IP: ({address}).").ConfigureAwait(false);
            return;
        }

        _ = Array.Empty<byte>();
        byte[]? bytes;
        try
        {
            bytes = await bot.Bot.Connection.PixelPeek(token).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex)
        {
            await ReplyAsync($"<a:Error:1223766391958671454> Error al recuperar píxeles: {ex.Message}");
            return;
        }

        if (bytes.Length == 0)
        {
            await ReplyAsync("<a:warning:1206483664939126795> No se recibieron datos de captura de pantalla.");
            return;
        }

        using MemoryStream ms = new(bytes);
        const string img = "cap.jpg";
        var embed = new EmbedBuilder { ImageUrl = $"attachment://{img}", Color = (DiscordColor?)Color.Purple }
            .WithFooter(new EmbedFooterBuilder { Text = $"Aquí está tu captura de pantalla." });

        await Context.Channel.SendFileAsync(ms, img, embed: embed.Build());
    }

    [Command("video")]
    [Alias("video")]
    [Summary("Toma y envia un GIF desde el Switch actualmente configurado.")]
    [RequireSudo]
    public async Task RePeekGIF()
    {
        await Context.Channel.SendMessageAsync("Processing GIF request...").ConfigureAwait(false);

        // Offload processing to a separate task so we dont hold up gateway tasks
        _ = Task.Run(async () =>
        {
            try
            {
                string ip = OwnerModule<T>.GetBotIPFromJsonConfig();
                var source = new CancellationTokenSource();
                var token = source.Token;
                var bot = SysCord<T>.Runner.GetBot(ip);
                if (bot == null)
                {
                    await ReplyAsync($"<a:warning:1206483664939126795> No se encontró ningún bot con la dirección IP: ({ip}).").ConfigureAwait(false);
                    return;
                }
                const int screenshotCount = 10;
                var screenshotInterval = TimeSpan.FromSeconds(0.1 / 10);
#pragma warning disable CA1416 // Validate platform compatibility
                var gifFrames = new List<System.Drawing.Image>();
#pragma warning restore CA1416 // Validate platform compatibility
                for (int i = 0; i < screenshotCount; i++)
                {
                    byte[] bytes;
                    try
                    {
                        bytes = await bot.Bot.Connection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync($"<a:Error:1223766391958671454> Error al recuperar píxeles: {ex.Message}").ConfigureAwait(false);
                        return;
                    }
                    if (bytes.Length == 0)
                    {
                        await ReplyAsync("<a:warning:1206483664939126795> No se recibieron datos de captura de pantalla.").ConfigureAwait(false);
                        return;
                    }
                    using (var ms = new MemoryStream(bytes))
                    {
#pragma warning disable CA1416 // Validate platform compatibility
                        using var bitmap = new Bitmap(ms);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                        var frame = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                        gifFrames.Add(frame);
#pragma warning restore CA1416 // Validate platform compatibility
                    }
                    await Task.Delay(screenshotInterval).ConfigureAwait(false);
                }
                using (var ms = new MemoryStream())
                {
                    using (var gif = new AnimatedGifCreator(ms, 200))
                    {
                        foreach (var frame in gifFrames)
                        {
                            gif.AddFrame(frame);
#pragma warning disable CA1416 // Validate platform compatibility
                            frame.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
                        }
                    }
                    ms.Position = 0;
                    const string gifFileName = "screenshot.gif";
                    var embed = new EmbedBuilder { ImageUrl = $"attachment://{gifFileName}", Color = (DiscordColor?)Color.Red }
                        .WithFooter(new EmbedFooterBuilder { Text = "Here's your GIF." });
                    await Context.Channel.SendFileAsync(ms, gifFileName, embed: embed.Build()).ConfigureAwait(false);
                }
                foreach (var frame in gifFrames)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    frame.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
                }
#pragma warning disable CA1416 // Validate platform compatibility
                gifFrames.Clear();
#pragma warning restore CA1416 // Validate platform compatibility
            }
            catch (Exception ex)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Error al procesar GIF: {ex.Message}").ConfigureAwait(false);
            }
        });
    }

    private static string GetBotIPFromJsonConfig()
    {
        try
        {
            var jsonData = File.ReadAllText(TradeBot.ConfigPath);
            var config = JObject.Parse(jsonData);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var ip = config["Bots"][0]["Connection"]["IP"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            return ip;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"<a:Error:1223766391958671454> Error al leer el archivo de configuración: {ex.Message}");
            return "192.168.1.1";
        }
    }

    [Command("kill")]
    [Alias("shutdown")]
    [Summary("Hace que todo el proceso termine solo!")]
    [RequireOwner]
    public async Task ExitProgram()
    {
        await Context.Channel.EchoAndReply("<a:yes:1206485105674166292> Cerrando... ¡adiós! **Los servicios de bots se están desconectando.**").ConfigureAwait(false);
        Environment.Exit(0);
    }

    [Command("dm")]
    [Summary("Envía un mensaje directo a un usuario específico.")]
    [RequireOwner]
    public async Task DMUserAsync(SocketUser user, [Remainder] string message)
    {
        var attachments = Context.Message.Attachments;
        var hasAttachments = attachments.Count != 0;
        List<string> imageUrls = new List<string>();
        List<string> nonImageAttachmentUrls = new List<string>();

        // Collect image and non-image attachments separately
        foreach (var attachment in attachments)
        {
            if (attachment.Filename.EndsWith(".png") || attachment.Filename.EndsWith(".jpg") || attachment.Filename.EndsWith(".jpeg") || attachment.Filename.EndsWith(".gif"))
            {
                if (imageUrls.Count < 3) // Collect up to 3 image URLs
                {
                    imageUrls.Add(attachment.Url);
                }
            }
            else
            {
                nonImageAttachmentUrls.Add(attachment.Url);
            }
        }

        var embed = new EmbedBuilder
        {
            Title = "📢 Mensaje privado del propietario del bot",
            Description = $"### Mensaje:\n{message}",
            Color = (DiscordColor?)Color.Gold,
            Timestamp = DateTimeOffset.Now,
            ThumbnailUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/pikamail.png"
        };

        // Set the first image as the main embed image if available
        if (imageUrls.Any())
        {
            embed.ImageUrl = imageUrls[0];
        }

        // Add up to two more images as fields with clickable links
        for (int i = 1; i < imageUrls.Count; i++)
        {
            embed.AddField($"Imagen adicional {i}", $"[Ver imagen]({imageUrls[i]})");
        }

        // Add non-image attachments as download links
        foreach (var url in nonImageAttachmentUrls)
        {
            embed.AddField("Enlace de descarga", url);
        }

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();

            await dmChannel.SendMessageAsync(embed: embed.Build());

            var confirmationMessage = await ReplyAsync($"<a:yes:1206485105674166292> Mensaje enviado exitosamente a **{user.Username}**.");
            await Context.Message.DeleteAsync();
            await Task.Delay(TimeSpan.FromSeconds(10));
            await confirmationMessage.DeleteAsync();
        }
        catch (Exception ex)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> No se pudo enviar el mensaje a **{user.Username}**. Error: {ex.Message}");
        }
    }

    [Command("say")]
    [Summary("Envía un mensaje a un canal específico.")]
    [RequireSudo]
    public async Task SayAsync([Remainder] string message)
    {
        var attachments = Context.Message.Attachments;
        var hasAttachments = attachments.Count != 0;

        var indexOfChannelMentionStart = message.LastIndexOf('<');
        var indexOfChannelMentionEnd = message.LastIndexOf('>');
        if (indexOfChannelMentionStart == -1 || indexOfChannelMentionEnd == -1)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> {Context.User.Mention}, por favor mencione un canal correctamente usando #channel.");
            return;
        }

        var channelMention = message.Substring(indexOfChannelMentionStart, indexOfChannelMentionEnd - indexOfChannelMentionStart + 1);
        var actualMessage = message.Substring(0, indexOfChannelMentionStart).TrimEnd();

        var channel = Context.Guild.Channels.FirstOrDefault(c => $"<#{c.Id}>" == channelMention);

        if (channel == null)
        {
            await ReplyAsync("<a:no:1206485104424128593> Canal no encontrado.");
            return;
        }

        if (channel is not IMessageChannel messageChannel)
        {
            await ReplyAsync("<a:warning:1206483664939126795> El canal mencionado no es un canal de texto.");
            return;
        }

        // If there are attachments, send them to the channel
        if (hasAttachments)
        {
            foreach (var attachment in attachments)
            {
                using var httpClient = new HttpClient();
                var stream = await httpClient.GetStreamAsync(attachment.Url);
                var file = new FileAttachment(stream, attachment.Filename);
                await messageChannel.SendFileAsync(file, actualMessage);
            }
        }
        else
        {
            await messageChannel.SendMessageAsync(actualMessage);
        }

        // Send confirmation message to the user
        await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, mensaje publicado exitosamente en {channelMention}.");
    }

    private RemoteControlAccess GetReference(IUser channel) => new()
    {
        ID = channel.Id,
        Name = channel.Username,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };
}
