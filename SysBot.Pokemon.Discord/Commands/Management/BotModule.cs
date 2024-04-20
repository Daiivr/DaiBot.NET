using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class BotModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("botStatus")]
        [Summary("Obtiene el estado de los bots.")]
        [RequireSudo]
        public async Task GetStatusAsync()
        {
            var me = SysCord<T>.Runner;
            var bots = me.Bots.Select(z => z.Bot).OfType<PokeRoutineExecutorBase>().ToArray();
            if (bots.Length == 0)
            {
                await ReplyAsync("<a:warning:1206483664939126795> No hay bots configurados.").ConfigureAwait(false);
                return;
            }

            var summaries = bots.Select(GetDetailedSummary);
            var lines = string.Join(Environment.NewLine, summaries);
            await ReplyAsync(Format.Code(lines)).ConfigureAwait(false);
        }

        private static string GetBotIPFromJsonConfig()
        {
            try
            {
                // Read the file and parse the JSON
                var jsonData = File.ReadAllText(TradeBot.ConfigPath);
                var config = JObject.Parse(jsonData);

                // Access the IP address from the first bot in the Bots array
                var ip = config["Bots"][0]["Connection"]["IP"].ToString();
                return ip;
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during reading or parsing the file
                Console.WriteLine($"Error al leer el archivo de configuración: {ex.Message}");
                return "192.168.1.1"; // Default IP if error occurs
            }
        }

        private static string GetDetailedSummary(PokeRoutineExecutorBase z)
        {
            return $"- {z.Connection.Name} | {z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}";
        }

        [Command("botStart")]
        [Summary("Inicia el bot que se está ejecutando actualmente.")]
        [RequireSudo]
        public async Task StartBotAsync([Summary("IP address of the bot")] string ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Start();
            await ReplyAsync($"<a:yes:1206485105674166292> El bot ha recibido la orden de Iniciar.").ConfigureAwait(false);
        }

        [Command("botStop")]
        [Summary("Detiene el bot que se está ejecutando actualmente.")]
        [RequireSudo]
        public async Task StopBotAsync([Summary("IP address of the bot")] string ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Stop();
            await ReplyAsync($"<a:yes:1206485105674166292> El bot ha recibido la orden de Parar.").ConfigureAwait(false);
        }

        [Command("botIdle")]
        [Alias("botPause")]
        [Summary("Ordena al bot que se está ejecutando actualmente que permanezca inactivo.")]
        [RequireSudo]
        public async Task IdleBotAsync([Summary("IP address of the bot")] string ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Pause();
            await ReplyAsync($"<a:yes:1206485105674166292>El bot ha sido comandado a Idle.").ConfigureAwait(false);
        }

        [Command("botChange")]
        [Summary("Cambia la rutina del bot actualmente en ejecución (intercambios).")]
        [RequireSudo]
        public async Task ChangeTaskAsync([Summary("Routine enum name")] PokeRoutineType task, [Summary("IP address of the bot")] string ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Bot.Config.Initialize(task);
            await ReplyAsync($"<a:yes:1206485105674166292> El bot ha recibido la orden de realizar **{task}** como su próxima tarea.").ConfigureAwait(false);
        }

        [Command("botRestart")]
        [Summary("Reinicia los bots que se están ejecutando actualmente.")]
        [RequireSudo]
        public async Task RestartBotAsync([Summary("IP address of the bot")] string ip = null)
        {
            if (ip == null)
                ip = BotModule<T>.GetBotIPFromJsonConfig();

            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"<a:warning:1206483664939126795> Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            c.Reset();
            bot.Start();
            await ReplyAsync($"<a:yes:1206485105674166292> El bot ha recibido la orden de Reiniciarse.").ConfigureAwait(false);
        }
    }
}
