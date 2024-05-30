using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Requires an assigned role in order to accept commands. Can be used by sudo users if satisfied.
/// </summary>
public sealed class RequireRoleAccessAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return Task.FromResult(PreconditionResult.FromSuccess());

        // Check if this user is a Guild User, which is the only context where roles exist
        if (context.User is not SocketGuildUser gUser)
            return Task.FromResult(PreconditionResult.FromError($"<a:warning:1206483664939126795> {context.User.Mention} este comando solo puede ser usado dentro de un servidor y no en mensajes directos."));

        var roles = gUser.Roles;
        if (mgr.CanUseSudo(roles.Select(z => z.Name)))
            return Task.FromResult(PreconditionResult.FromSuccess());

        if (!mgr.GetHasRoleAccess(RoleName, roles.Select(z => z.Name)))
            return Task.FromResult(PreconditionResult.FromError($"<a:no:1206485104424128593> {context.User.Mention} No tienes el rol requerido para ejecutar este comando."));

        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
