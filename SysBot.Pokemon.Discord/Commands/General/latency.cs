using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class LatencyModule : ModuleBase<SocketCommandContext>
    {
        private static readonly DateTime StartTime = DateTime.UtcNow; // Corrected Start Time at Bot Launch

        [Command("latency")]
        [Alias("latencia")]
        [Summary("Muestra la latencia, tiempo de actividad, uso de CPU y memoria del bot.")]
        [RequireOwner]
        public async Task LatenciaAsync()
        {
            var botUser = Context.Client.CurrentUser; // Bot info
            var process = Process.GetCurrentProcess(); // Current process

            var stopwatch = Stopwatch.StartNew();
            var message = await ReplyAsync("🏓 Midiendo latencia...").ConfigureAwait(false);
            stopwatch.Stop();

            var latencia = Context.Client.Latency; // WebSocket latency
            var tiempoRespuesta = stopwatch.ElapsedMilliseconds; // Actual response time
            var uptimeTimestamp = ((DateTimeOffset)StartTime).ToUnixTimeSeconds(); // Convert StartTime to Unix timestamp
            var memoria = process.WorkingSet64 / 1024 / 1024; // RAM usage in MB
            var cpuUsage = GetCpuUsage(); // CPU usage %
            var servidores = Context.Client.Guilds.Count; // Number of servers
            var usuarios = Context.Client.Guilds.Sum(g => g.MemberCount); // Total users

            var embed = new EmbedBuilder()
                .WithTitle(botUser.Username) // Bot name as title
                .WithThumbnailUrl(botUser.GetAvatarUrl() ?? botUser.GetDefaultAvatarUrl()) // Bot avatar
                .WithColor(Color.Blue) // Embed color
                .AddField("🏓 ¡Pong!", "Resultados de la prueba de latencia:")
                .AddField("🕰 Latencia WebSocket", $"{latencia}ms", true)
                .AddField("⏱ Tiempo de Respuesta", $"{tiempoRespuesta}ms", true)
                .AddField("🔄 Tiempo en línea", $"<t:{uptimeTimestamp}:R>", true) // Live updating uptime
                .AddField("💾 Uso de Memoria", $"{memoria} MB", true)
                .AddField("⚙️ Uso de CPU", $"{cpuUsage:F2}%", true)
                .AddField("🌍 Servidores", $"{servidores}", true)
                .AddField("👥 Usuarios", $"{usuarios}", true)
                .WithFooter($"Solicitado por {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await message.ModifyAsync(m =>
            {
                m.Content = "";
                m.Embed = embed;
            }).ConfigureAwait(false);
        }

        private static double GetCpuUsage()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return (process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount) / (DateTime.UtcNow - StartTime).TotalMilliseconds * 100;
            }
        }
    }
}
