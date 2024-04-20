using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

// ReSharper disable once UnusedType.Global
public class BatchEditingModule : ModuleBase<SocketCommandContext>
{
    [Command("batchInfo"), Alias("bei")]
    [Summary("Intenta obtener información sobre la propiedad solicitada.")]
    public async Task GetBatchInfo(string propertyName)
    {
        var result = BatchEditing.GetPropertyType(propertyName);
        if (string.IsNullOrWhiteSpace(result))
            await ReplyAsync($"<a:warning:1206483664939126795> No pude encuentra información sobre: {propertyName}.").ConfigureAwait(false);
        else
            await ReplyAsync($"{propertyName}: {result}").ConfigureAwait(false);
    }

    [Command("batchValidate"), Alias("bev")]
    [Summary("Intenta obtener información sobre la propiedad solicitada.")]
    public async Task ValidateBatchInfo(string instructions)
    {
        bool valid = IsValidInstructionSet(instructions, out var invalid);

        if (!valid)
        {
            var msg = invalid.Select(z => $"{z.PropertyName}, {z.PropertyValue}");
            await ReplyAsync($"<a:warning:1206483664939126795> Líneas no válidas detectadas:\r\n{Format.Code(string.Join(Environment.NewLine, msg))}")
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync($"{invalid.Count} la(s) línea(s) no es(son) válida(s).").ConfigureAwait(false);
        }
    }

    private static bool IsValidInstructionSet(ReadOnlySpan<char> split, out List<StringInstruction> invalid)
    {
        invalid = [];
        var set = new StringInstructionSet(split);
        foreach (var s in set.Filters.Concat(set.Instructions))
        {
            var type = BatchEditing.GetPropertyType(s.PropertyName);
            if (type == null)
                invalid.Add(s);
        }

        return invalid.Count == 0;
    }
}
