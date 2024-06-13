using Discord;
using Discord.Commands;
using ImageSharp = SixLabors.ImageSharp;  // Alias for ImageSharp namespace
using SixLabors.ImageSharp.PixelFormats;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysBot.Pokemon.Discord;
using System;

namespace SysBot.Pokemon.Discord;

public class HelloModule : ModuleBase<SocketCommandContext>
{
    [Command("hello")]
    [Alias("hi")]
    [Summary("Saluda al bot y obtén una respuesta.")]
    public async Task PingAsync()
    {
        var avatarUrl = Context.User.GetAvatarUrl(size: 128) ?? Context.User.GetDefaultAvatarUrl();
        var color = await GetDominantColorAsync(avatarUrl);

        var str = SysCordSettings.Settings.HelloResponse;
        var msg = string.Format(str, Context.User.Mention);

        var embed = new EmbedBuilder()
            .WithTitle("¡Saludos!")
            .WithDescription(msg)
            .WithColor(color) // Establece el color del embed
            .WithCurrentTimestamp()
            .WithThumbnailUrl("https://i.imgur.com/BcMI5KC.png")
            .WithImageUrl("https://i.pinimg.com/originals/1a/0e/2f/1a0e2f953f778092b079dcf6f5800b5d.gif")
            .WithFooter(footer =>
            {
                footer.WithText(Context.User.Username);
                footer.WithIconUrl(avatarUrl);
            });
        embed.WithTimestamp(DateTime.Now);

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    private async Task<Color> GetDominantColorAsync(string imageUrl)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(imageUrl);
        using var stream = await response.Content.ReadAsStreamAsync();

        using var image = ImageSharp.Image.Load<Rgba32>(stream);
        var histogram = new Dictionary<Rgba32, int>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                if (histogram.ContainsKey(pixel))
                    histogram[pixel]++;
                else
                    histogram[pixel] = 1;
            }
        }

        var dominant = histogram.OrderByDescending(kvp => kvp.Value).First().Key;
        return new Color(dominant.R, dominant.G, dominant.B);
    }
}
