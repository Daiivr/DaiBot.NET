using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class LegalizerModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("convert"), Alias("showdown")]
        [Summary("Intenta convertir el conjunto Showdown al formato de plantilla Regen.")]
        [Priority(1)]
        public async Task ConvertShowdown([Summary("Generation/Format")] byte gen, [Remainder][Summary("Showdown Set")] string content)
        {
            var deleteMessageTask = DeleteCommandMessageAsync(Context.Message, 2000);
            var convertTask = Task.Run(() => Context.Channel.ReplyWithLegalizedSetAsync(content, gen));
            await Task.WhenAll(deleteMessageTask, convertTask).ConfigureAwait(false);
        }

        [Command("convert"), Alias("showdown")]
        [Summary("Intenta convertir el conjunto Showdown al formato de plantilla Regen.")]
        [Priority(0)]
        public async Task ConvertShowdown([Remainder][Summary("Showdown Set")] string content)
        {
            var deleteMessageTask = DeleteCommandMessageAsync(Context.Message, 2000);
            var convertTask = Task.Run(() => Context.Channel.ReplyWithLegalizedSetAsync<T>(content));
            await Task.WhenAll(deleteMessageTask, convertTask).ConfigureAwait(false);
        }

        [Command("legalize"), Alias("alm")]
        [Summary("Intenta legalizar los datos pkm adjuntos y generar la salida como plantilla Regen.")]
        public async Task LegalizeAsync()
        {
            var deleteMessageTask = DeleteCommandMessageAsync(Context.Message, 2000);
            var legalizationTasks = Context.Message.Attachments.Select(att =>
                Task.Run(() => Context.Channel.ReplyWithLegalizedSetAsync(att))
            ).ToArray();

            await Task.WhenAll(deleteMessageTask, Task.WhenAll(legalizationTasks)).ConfigureAwait(false);
        }

        private async Task DeleteCommandMessageAsync(IUserMessage message, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds).ConfigureAwait(false);
            await message.DeleteAsync().ConfigureAwait(false);
        }
    }
}
