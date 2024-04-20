using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class HelpModule(CommandService Service) : ModuleBase<SocketCommandContext>
{
    [Command("help")]
    [Summary("Lists available commands.")]
    public async Task HelpAsync()
    {
        var builder = new EmbedBuilder
        {
            Color = new Color(114, 137, 218),
            Description = "📝 Estos son los comandos que puedes usar:",
        };

        var mgr = SysCordSettings.Manager;
        var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
        var owner = app.Owner.Id;
        var uid = Context.User.Id;

        int fieldsCount = 0; // Variable para realizar un seguimiento del número de campos agregados

        foreach (var module in Service.Modules)
        {
            string? description = null;
            HashSet<string> mentioned = new HashSet<string>(); // Corregir la inicialización de HashSet

            foreach (var cmd in module.Commands)
            {
                var name = cmd.Name;
                if (mentioned.Contains(name))
                    continue;
                if (cmd.Attributes.Any(z => z is RequireOwnerAttribute) && owner != uid)
                    continue;
                if (cmd.Attributes.Any(z => z is RequireSudoAttribute) && !mgr.CanUseSudo(uid))
                    continue;

                mentioned.Add(name);
                var result = await cmd.CheckPreconditionsAsync(Context).ConfigureAwait(false);
                if (result.IsSuccess)
                    description += $"{cmd.Aliases[0]}\n";
            }
            if (string.IsNullOrWhiteSpace(description))
                continue;

            var moduleName = module.Name;
            var gen = moduleName.IndexOf('`');
            if (gen != -1)
                moduleName = moduleName[..gen];

            builder.AddField(x =>
            {
                x.Name = moduleName;
                x.Value = description;
                x.IsInline = true;
            });

            fieldsCount++; // Incrementar el contador de campos agregados

            // Si el número de campos agregados alcanza 25, enviar el EmbedBuilder actual y crear uno nuevo
            if (fieldsCount >= 25)
            {
                await ReplyAsync(embed: builder.Build()).ConfigureAwait(false);
                builder = new EmbedBuilder
                {
                    Color = new Color(114, 137, 218),
                    Title = "Continuación de la lista de comandos:", // Agregar título al nuevo EmbedBuilder
                };
                fieldsCount = 0; // Restablecer el contador de campos
            }
        }

        // Enviar el último EmbedBuilder si hay campos restantes
        if (fieldsCount > 0)
        {
            await ReplyAsync(embed: builder.Build()).ConfigureAwait(false);
        }
    }


    [Command("help")]
    [Summary("Lists information about a specific command.")]
    public async Task HelpAsync([Summary("The command you want help for")] string command)
    {
        var result = Service.Search(Context, command);

        if (!result.IsSuccess)
        {
            await ReplyAsync($"<a:warning:1206483664939126795> Lo siento, no pude encontrar un comando como: **{command}**.").ConfigureAwait(false);
            return;
        }

        var builder = new EmbedBuilder
        {
            Color = new Color(114, 137, 218),
            Description = $"He aquí algunos comandos como: **{command}**:",
        };

        foreach (var match in result.Commands)
        {
            var cmd = match.Command;

            builder.AddField(x =>
            {
                x.Name = string.Join(", ", cmd.Aliases);
                x.Value = GetCommandSummary(cmd);
                x.IsInline = false;
            });
        }

        await ReplyAsync("¡La ayuda ha llegado!", false, builder.Build()).ConfigureAwait(false);
    }

    private static string GetCommandSummary(CommandInfo cmd)
    {
        return $"Summary: {cmd.Summary}\nParameters: {GetParameterSummary(cmd.Parameters)}";
    }

    private static string GetParameterSummary(IReadOnlyList<ParameterInfo> p)
    {
        if (p.Count == 0)
            return "None";
        return $"{p.Count}\n- " + string.Join("\n- ", p.Select(GetParameterSummary));
    }

    private static string GetParameterSummary(ParameterInfo z)
    {
        var result = z.Name;
        if (!string.IsNullOrWhiteSpace(z.Summary))
            result += $" ({z.Summary})";
        return result;
    }
}
