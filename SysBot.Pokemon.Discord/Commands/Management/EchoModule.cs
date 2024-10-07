using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static SysBot.Pokemon.DiscordSettings;

namespace SysBot.Pokemon.Discord
{
    public static class EmbedColorConverter
    {
        public static Color ToDiscordColor(this EmbedColorOption colorOption)
        {
            return colorOption switch
            {
                EmbedColorOption.Blue => Color.Blue,
                EmbedColorOption.Green => Color.Green,
                EmbedColorOption.Red => Color.Red,
                EmbedColorOption.Gold => Color.Gold,
                EmbedColorOption.Purple => Color.Purple,
                EmbedColorOption.Teal => Color.Teal,
                EmbedColorOption.Orange => Color.Orange,
                EmbedColorOption.Magenta => Color.Magenta,
                EmbedColorOption.LightGrey => Color.LightGrey,
                EmbedColorOption.DarkGrey => Color.DarkGrey,
                _ => Color.Blue,  // Default to Blue if somehow an undefined enum value is used
            };
        }
    }

    public class EchoModule : ModuleBase<SocketCommandContext>
    {
        private static DiscordSettings? Settings { get; set; }

        private class EchoChannel(ulong channelId, string channelName, Action<string> action, Action<byte[], string, EmbedBuilder> raidAction)
        {
            public readonly ulong ChannelID = channelId;

            public readonly string ChannelName = channelName;

            public readonly Action<string> Action = action;

            public readonly Action<byte[], string, EmbedBuilder> RaidAction = raidAction;

            public string EmbedResult = string.Empty;
        }

        private class EncounterEchoChannel(ulong channelId, string channelName, Action<string, Embed> embedaction)
        {
            public readonly ulong ChannelID = channelId;

            public readonly string ChannelName = channelName;

            public readonly Action<string, Embed> EmbedAction = embedaction;

            public string EmbedResult = string.Empty;
        }

        private static readonly Dictionary<ulong, EchoChannel> Channels = [];

        private static readonly Dictionary<ulong, EncounterEchoChannel> EncounterChannels = [];

        private static readonly Dictionary<ulong, EchoChannel> AbuseChannels = [];

        public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
        {
            Settings = cfg;
            foreach (var ch in cfg.AnnouncementChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID);
            }
            foreach (var ch in cfg.AbuseLogChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddAbuseEchoChannel(c, ch.ID);
            }
        }

        [Command("AddAbuseEchoChannel")]
        [Alias("aaec")]
        [Summary("Hace que el bot publique registros de abuso en el canal.")]
        [RequireSudo]
        public async Task AddAbuseEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (AbuseChannels.TryGetValue(cid, out _))
            {
                await ReplyAsync("⚠️ Ya se están registrando abusos en este canal.").ConfigureAwait(false);
                return;
            }
            AddAbuseEchoChannel(c, cid);
            SysCordSettings.Settings.AbuseLogChannels.AddIfNew([GetReference(Context.Channel)]);
            await ReplyAsync("✅ ¡Se agregó la salida del registro de abuso a este canal!").ConfigureAwait(false);
        }
        private static void AddAbuseEchoChannel(ISocketMessageChannel c, ulong cid)
        {
            async void l(string msg) => await SendMessageWithRetry(c, msg).ConfigureAwait(false);
            EchoUtil.AbuseForwarders.Add(l);
            var entry = new EchoChannel(cid, c.Name, l, null);
            AbuseChannels.Add(cid, entry);
        }
        public static bool IsAbuseEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return AbuseChannels.TryGetValue(cid, out _);
        }
        [Command("RemoveAbuseEchoChannel")]
        [Alias("raec")]
        [Summary("Elimina el registro de abuso del canal.")]
        [RequireSudo]
        public async Task RemoveAbuseEchoAsync()
        {
            var id = Context.Channel.Id;
            if (!AbuseChannels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("⚠️ No se estan registrando abusos en este canal.").ConfigureAwait(false);
                return;
            }
            AbuseChannels.Remove(id);
            SysCordSettings.Settings.AbuseLogChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"✅ Se eliminó el registro de abuso del canal: {Context.Channel.Name}").ConfigureAwait(false);
        }
        [Command("ListAbuseEchoChannels")]
        [Alias("laec")]
        [Summary("Enumera todos los canales donde está habilitado el registro de abuso.")]
        [RequireSudo]
        public async Task ListAbuseEchoChannelsAsync()
        {
            if (AbuseChannels.Count == 0)
            {
                await ReplyAsync("⚠️ Actualmente no hay canales configurados para el registro de abuso.").ConfigureAwait(false);
                return;
            }
            var response = "📑 El registro de abuso está habilitado en los siguientes canales:\n";
            foreach (var channel in AbuseChannels.Values)
            {
                response += $"- {channel.ChannelName} (ID: {channel.ChannelID})\n";
            }

            await ReplyAsync(response).ConfigureAwait(false);
        }

        [Command("Announce", RunMode = RunMode.Async)]
        [Alias("announce")]
        [Summary("Envía un anuncio a todos los canales Echo agregados por el comando aec.")]
        [RequireOwner]
        public async Task AnnounceAsync([Remainder] string announcement)
        {
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var formattedTimestamp = $"<t:{unixTimestamp}:F>";
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var embedColor = Settings.AnnouncementSettings.RandomAnnouncementColor ? GetRandomColor() : Settings.AnnouncementSettings.AnnouncementEmbedColor.ToDiscordColor();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            // Default thumbnail URL
            var thumbnailUrl = Settings.AnnouncementSettings.RandomAnnouncementThumbnail ? GetRandomThumbnail() : GetSelectedThumbnail();

            // Checking for message attachments (images/other files)
            string? imageUrl = null;
            string? attachmentUrl = null;
            if (Context.Message.Attachments.Any())
            {
                var attachment = Context.Message.Attachments.First();
                if (attachment.Width.HasValue) // This indicates it's an image
                {
                    imageUrl = attachment.Url;
                }
                else
                {
                    attachmentUrl = attachment.Url;
                }
            }

            var embedDescription = $"## {announcement}\n\n**Enviado: {formattedTimestamp}**";

            var embed = new EmbedBuilder
            {
                Color = embedColor,
                Description = embedDescription,
                ThumbnailUrl = thumbnailUrl
            }
            .WithTitle("<a:Megaphone:1218248132954030141>  Anuncio importante!");

            // If an image URL is available, use it instead of the thumbnail
            if (!string.IsNullOrEmpty(imageUrl))
            {
                embed.WithImageUrl(imageUrl);
            }
            else
            {
                embed.WithThumbnailUrl(thumbnailUrl);
            }

            // If there's an attachment URL, add it as a field
            if (!string.IsNullOrEmpty(attachmentUrl))
            {
                embed.AddField("Descargar Adjunto", attachmentUrl);
            }

            var embedBuilt = embed.Build();

            var client = Context.Client;
            foreach (var channelEntry in Channels)
            {
                var channelId = channelEntry.Key;
                if (client.GetChannel(channelId) is not ISocketMessageChannel channel)
                {
                    LogUtil.LogError($"<a:warning:1206483664939126795> No se pudo encontrar o acceder al canal {channelId}", nameof(AnnounceAsync));
                    continue;
                }

                try
                {
                    await channel.SendMessageAsync(embed: embedBuilt).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"<a:warning:1206483664939126795> No se pudo enviar el anuncio al canal {channel.Name}: {ex.Message}", nameof(AnnounceAsync));
                }
            }
            var confirmationMessage = await ReplyAsync("<a:yes:1206485105674166292> Anuncio enviado a todos los canales Echo.").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await confirmationMessage.DeleteAsync().ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }


        private static Color GetRandomColor()
        {
            var random = new Random();
            var colors = Enum.GetValues(typeof(EmbedColorOption)).Cast<EmbedColorOption>().ToList();
            return colors[random.Next(colors.Count)].ToDiscordColor();
        }

        private static string GetRandomThumbnail()
        {
            var thumbnailOptions = new List<string>
        {
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/gengarmegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/pikachumegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/umbreonmegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/sylveonmegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/charmandermegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/jigglypuffmegaphone.png",
        "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/flareonmegaphone.png",
        };
            var random = new Random();
            return thumbnailOptions[random.Next(thumbnailOptions.Count)];
        }

        private static string GetSelectedThumbnail()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (!string.IsNullOrEmpty(Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl))
            {
                return Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl;
            }
            else
            {
                return GetUrlFromThumbnailOption(Settings.AnnouncementSettings.AnnouncementThumbnailOption);
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        private static string GetUrlFromThumbnailOption(ThumbnailOption option)
        {
            return option switch
            {
                ThumbnailOption.Gengar => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/gengarmegaphone.png",
                ThumbnailOption.Pikachu => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/pikachumegaphone.png",
                ThumbnailOption.Umbreon => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/umbreonmegaphone.png",
                ThumbnailOption.Sylveon => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/sylveonmegaphone.png",
                ThumbnailOption.Charmander => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/charmandermegaphone.png",
                ThumbnailOption.Jigglypuff => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/jigglypuffmegaphone.png",
                ThumbnailOption.Flareon => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/flareonmegaphone.png",
                _ => "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/gengarmegaphone.png",
            };
        }

        [Command("addEmbedChannel")]
        [Alias("aec")]
        [Summary("Hace que el bot publique embeds de incursiones en el canal.")]
        [RequireSudo]
        public async Task AddEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("Ya estoy notificando aquí.").ConfigureAwait(false);
                return;
            }

            AddEchoChannel(c, cid);

            SysCordSettings.Settings.AnnouncementChannels.AddIfNew([GetReference(Context.Channel)]);
            await ReplyAsync("<a:yes:1206485105674166292> ¡Se agregaron los Embed de anuncios a este canal!").ConfigureAwait(false);
        }

        private static async Task<bool> SendMessageWithRetry(ISocketMessageChannel c, string message, int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await c.SendMessageAsync(message).ConfigureAwait(false);
                    return true; // Successfully sent the message, exit the loop.
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"<a:warning:1206483664939126795> No se pudo enviar el mensaje al canal '{c.Name}' (Attempt {retryCount + 1}): {ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false); // Wait for 5 seconds before retrying.
                }
            }
            return false; // Reached max number of retries without success.
        }

        private static async Task<bool> RaidEmbedAsync(ISocketMessageChannel c, byte[] bytes, string fileName, EmbedBuilder embed, int maxRetries = 2)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    if (bytes is not null && bytes.Length > 0)
                    {
                        await c.SendFileAsync(new MemoryStream(bytes), fileName, "", false, embed: embed.Build()).ConfigureAwait(false);
                    }
                    else
                    {
                        await c.SendMessageAsync("", false, embed.Build()).ConfigureAwait(false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"<a:warning:1206483664939126795> No se pudo enviar el embed al canal '{c.Name}' (Attempt {retryCount + 1}): {ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    if (retryCount < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false); // Wait for a second before retrying.
                }
            }
            return false;
        }

        private static void AddEchoChannel(ISocketMessageChannel c, ulong cid)
        {
            async void l(string msg) => await SendMessageWithRetry(c, msg).ConfigureAwait(false);
            async void rb(byte[] bytes, string fileName, EmbedBuilder embed) => await RaidEmbedAsync(c, bytes, fileName, embed).ConfigureAwait(false);

            EchoUtil.Forwarders.Add(l);
            var entry = new EchoChannel(cid, c.Name, l, rb);
            Channels.Add(cid, entry);
        }

        public static bool IsEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return Channels.TryGetValue(cid, out _);
        }

        public static bool IsEmbedEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return EncounterChannels.TryGetValue(cid, out _);
        }

        [Command("echoInfo")]
        [Summary("Dump la configuración del mensaje especial (Echo).")]
        [RequireSudo]
        public async Task DumpEchoInfoAsync()
        {
            foreach (var c in Channels)
                await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
        }

        [Command("echoClear")]
        [Alias("rec")]
        [Summary("Borra la configuración de eco de mensajes especiales en ese canal específico.")]
        [RequireSudo]
        public async Task ClearEchosAsync()
        {
            var id = Context.Channel.Id;
            if (!Channels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("<a:warning:1206483664939126795> No hay eco en este canal.").ConfigureAwait(false);
                return;
            }
            EchoUtil.Forwarders.Remove(echo.Action);
            Channels.Remove(Context.Channel.Id);
            SysCordSettings.Settings.AnnouncementChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"<a:yes:1206485105674166292> Ecos eliminados del canal: {Context.Channel.Name}").ConfigureAwait(false);
        }

        [Command("echoClearAll")]
        [Alias("raec")]
        [Summary("Borra todas las configuraciones del canal Echo de mensajes especiales.")]
        [RequireSudo]
        public async Task ClearEchosAllAsync()
        {
            foreach (var l in Channels)
            {
                var entry = l.Value;
                await ReplyAsync($"<a:yes:1206485105674166292> Eco borrado de {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
                EchoUtil.Forwarders.Remove(entry.Action);
            }
            EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
            Channels.Clear();
            SysCordSettings.Settings.AnnouncementChannels.Clear();
            await ReplyAsync("<a:yes:1206485105674166292> ¡Ecos eliminados de todos los canales!").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Añadido por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };
    }
}
