using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class LegalityCheckModule : ModuleBase<SocketCommandContext>
{
    [Command("lc"), Alias("check", "validate", "verify")]
    [Summary("Verifies the attachment for legality.")]
    public async Task LegalityCheck()
    {
        var attachments = Context.Message.Attachments;
        foreach (var att in attachments)
            await LegalityCheck(att, false).ConfigureAwait(false);
    }

    [Command("lcv"), Alias("verbose")]
    [Summary("Verifies the attachment for legality with a verbose output.")]
    public async Task LegalityCheckVerbose()
    {
        var attachments = Context.Message.Attachments;
        foreach (var att in attachments)
            await LegalityCheck(att, true).ConfigureAwait(false);
    }

    private async Task LegalityCheck(IAttachment att, bool verbose)
    {
        var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
        if (!download.Success)
        {
            await ReplyAsync(download.ErrorMessage).ConfigureAwait(false);
            return;
        }

        var pkm = download.Data!;
        var la = new LegalityAnalysis(pkm);
        var builder = new EmbedBuilder
        {
            Color = la.Valid ? Color.Green : Color.Red,
            Description = $"Informe de legalidad para: {download.SanitizedFileName}:",
        };

        builder.AddField(x =>
        {
            x.Name = la.Valid ? "<a:yes:1206485105674166292> Válido" : "<a:no:1206485104424128593> Inválido";
            x.Value = la.Report(verbose);
            x.IsInline = false;
        });

        await ReplyAsync("Aquí está el informe de legalidad!", false, builder.Build()).ConfigureAwait(false);
    }
}
