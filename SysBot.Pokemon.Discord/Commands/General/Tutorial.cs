using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System.Linq;
using SysBot.Pokemon.Discord;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TuBotDiscord.Modules;

public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    public class TutorialModule : ModuleBase<SocketCommandContext>
    {
        [Command("ayuda")]
        [Summary("Muestra como usar algunos comandos como el clone, fix, egg y demas.")]
        public async Task HelpAsync(string? command = null)
        {
            var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

            // Si el usuario pidió ayuda para un comando específico
            if (!string.IsNullOrEmpty(command))
            {
                var embedBuilder = new EmbedBuilder();
                var icon = "https://i.imgur.com/axXN5Sd.gif";

                ConfigureHelpEmbed(command.ToLower(), embedBuilder, icon, botPrefix);

                try
                {
                    // Enviar el mensaje por DM
                    var dmChannel = await Context.User.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync(embed: embedBuilder.Build());

                    // Eliminar el mensaje del usuario del canal
                    await Context.Message.DeleteAsync();

                    // Enviar confirmación en el canal
                    var confirmation = await ReplyAsync($"<a:yes:1206485105674166292> {Context.User.Mention}, la información de ayuda sobre el comando `{command}` ha sido enviada a tu MD. Por favor, revisa tus mensajes directos.");

                    // Borrar el mensaje de confirmación después de 5 segundos
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await confirmation.DeleteAsync();
                }
                catch
                {
                    // Si el usuario tiene los DMs bloqueados, notificar en el canal
                    await ReplyAsync($"❌ **{Context.User.Mention}, no puedo enviarte un mensaje privado. Asegúrate de tener los DMs habilitados.**");
                }

                return;
            }

            var builder = new EmbedBuilder()
                .WithTitle("Comandos disponibles")
                .WithDescription($"Selecciona un comando del menú desplegable para obtener más información.\n\n🔴 **Haz clic en el botón 'Cerrar' cuando hayas terminado.**")
                .AddField("» Menú Ayuda", $"Tenemos `12` categorías de las cuales puedes aprender cómo usar sus correspondientes funciones.\n\n**También puedes usar `{botPrefix}ayuda <comando>` para acceder directamente a un tema.**")
                .AddField("Opciones", $"- `{botPrefix}ayuda sr` ∷ Pedidos Especiales.\n- `{botPrefix}ayuda brl` ∷ Pokemons Entrenados.\n- `{botPrefix}ayuda le` ∷ Eventos\n- `{botPrefix}ayuda bt` ∷ Intercambio por Lotes.\n- `{botPrefix}ayuda clone` ∷ Clonar un Pokemon.\n- `{botPrefix}ayuda fix` ∷ Quitar Anuncios de Pokemon.s\n- `{botPrefix}ayuda ditto` ∷ Como pedir Dittos.\n- `{botPrefix}ayuda me` ∷ Como pedir Huevos Misteriosos.\n- `{botPrefix}ayuda egg` ∷ Como pedir Huevos de un Pokemons específico.\n- `{botPrefix}ayuda rt` ∷ Como generar Un equipo VGC random.\n- `{botPrefix}ayuda pp` ∷ Cómo generar un equipo a partir de un link PokePaste.\n- `{botPrefix}ayuda srp` ∷ Como pedir Regalos Misteriosos.")
                .WithColor(Discord.Color.Blue);

            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("📜 Selecciona un comando...") // Emoji in placeholder
                .WithCustomId("help_menu")
                .AddOption("Pedidos Especiales", "help_sr", "Información sobre pedidos especiales", new Emoji("📌"))
                .AddOption("Pokemons Entrenados", "help_brl", "Lista de pokémons entrenados", new Emoji("⚔️"))
                .AddOption("Eventos", "help_le", "Cómo solicitar eventos", new Emoji("🎉"))
                .AddOption("Intercambio por Lotes", "help_bt", "Cómo realizar intercambios por lotes", new Emoji("📦"))
                .AddOption("Clone", "help_clone", "Cómo clonar un Pokémon", new Emoji("🔁"))
                .AddOption("Fix", "help_fix", "Eliminar nombres no deseados de Pokémon", new Emoji("🛠️"))
                .AddOption("Ditto", "help_ditto", "Solicitar un Ditto con IVs específicos", new Emoji("✨"))
                .AddOption("Huevo Misterioso", "help_me", "Solicitar un huevo misterioso aleatorio", new Emoji("🥚"))
                .AddOption("Huevos", "help_egg", "Cómo solicitar huevos", new Emoji("🐣"))
                .AddOption("Equipo Random", "help_rt", "Generar un equipo aleatorio", new Emoji("🎲"))
                .AddOption("Equipo Completo", "help_pp", "Cómo obtener un equipo completo", new Emoji("🏆"))
                .AddOption("Regalos Misteriosos", "help_srp", "Solicitar regalos misteriosos", new Emoji("🎁"));

            var closeButton = new ButtonBuilder()
                .WithLabel("Cerrar")
                .WithStyle(ButtonStyle.Danger)
                .WithCustomId("close_help");

            var componentBuilder = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .WithButton(closeButton);

            var message = await ReplyAsync(embed: builder.Build(), components: componentBuilder.Build());
            await Context.Message.DeleteAsync();

            await HandleInteractions(message);
        }

        private async Task HandleInteractions(IUserMessage message)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), cancellationTokenSource.Token);

            while (true)
            {
                var interactionTask = WaitForInteractionResponseAsync(message, TimeSpan.FromMinutes(2));
                var completedTask = await Task.WhenAny(interactionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout occurred, remove the select menu and buttons
                    await message.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                    break;
                }

                var interaction = await interactionTask;
                if (interaction != null)
                {
                    // Reset the timeout
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource = new CancellationTokenSource();
                    timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), cancellationTokenSource.Token);

                    if (interaction.Data.CustomId == "close_help")
                    {
                        await interaction.Message.DeleteAsync();
                        return;
                    }

                    await interaction.DeferAsync(); // No ephemeral response

                    var command = interaction.Data.Values.FirstOrDefault()?.Substring(5) ?? string.Empty;
                    var icon = "https://i.imgur.com/axXN5Sd.gif";
                    var embedBuilder = new EmbedBuilder();

                    ConfigureHelpEmbed(command, embedBuilder, icon, SysCord<T>.Runner.Config.Discord.CommandPrefix);

                    // Edit the main embed instead of sending a new ephemeral message
                    await message.ModifyAsync(msg =>
                    {
                        msg.Embed = embedBuilder.Build();
                    });
                }
            }
        }

        private async Task<SocketMessageComponent?> WaitForInteractionResponseAsync(IUserMessage message, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessageComponent?>();
            var cancellationTokenSource = new CancellationTokenSource(timeout);

            Context.Client.InteractionCreated += OnInteractionCreated;

            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            finally
            {
                Context.Client.InteractionCreated -= OnInteractionCreated;
                cancellationTokenSource.Dispose();
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            async Task OnInteractionCreated(SocketInteraction interaction)
            {
                if (interaction is SocketMessageComponent componentInteraction &&
                    componentInteraction.Message.Id == message.Id)
                {
                    tcs.TrySetResult(componentInteraction);
                }
            }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        private void ConfigureHelpEmbed(string command, EmbedBuilder builder, string icon, string botPrefix)
        {
            // Set the thumbnail for all embeds
            builder.WithThumbnailUrl("https://i.imgur.com/lPU9wFp.png");

            switch (command.ToLower())
            {
                case "sr":
                    builder.WithAuthor("Pedidos Especiales", icon)
                           .WithDescription($"# Special Request\r\n\r\nEstas peticiones se hacen añadiéndote a ti mismo a la cola `SpecialRequest` usando `{botPrefix}sr` , donde muestras al bot un Pokémon ya sea con un **Objeto** específico o **Apodo** de una forma específica. Luego cambia a un Pokémon de descarte y completa el intercambio.\r\n\r\n**Nota**: *El Pokémon de descarte tiene que ser completamente diferente al Pokémon original.  Comprueba siempre si tu petición y el mon original son legales de antemano (por ejemplo, no intentes hacer shiny un Pokémon con shiny lock o cambiar el OT de un Pokémon de evento)*.\r\n\r\n### **__LimpiaOT/Apodo:__**\r\n- Dale al Pokémon este objeto: para este efecto.\r\n - **Poké Ball**: borra Apodo\r\n - **Great Ball**: borra OT (y lo cambia por tu propio nombre de entrenador)\r\n - **Ultra Ball**: borra OT y Apodo\r\n**Nota**: *Esto borrará los apodos al nombre del idioma original para tu especie de mon. Si necesitas un cambio de idioma, consulta más abajo.*\r\n\r\n### __**Cambios de idioma**__\r\n- Dale al Pokémon este objeto guardado: para este efecto.\r\n - **Protección X**: Japonés \r\n - **Crítico X**: Inglés \r\n - **Ataque X**: Alemán \r\n - **Defensa X**: Francés \r\n - **Velocidad X**: Español (los nombres son los mismos que eng)\r\n - **Precisión X**: Coreano \r\n - **Ataque especial X**: Chino T \r\n - **Defensa especial X**: Chino S\r\n**Nota**: *Esto también borrará los apodos.*\r\n\r\n### __**Estadisticas**__\r\n- Da al Pokémon este objeto: para este efecto.\r\n - **Cura total**: 6IV\r\n - **Pokémuñeco** 5IV 0 Velocidad\r\n - **Revivir**: 4IV 0 Velocidad 0 Ataque\r\n - **Agua fresca**: 5IV 0 Ataque\r\n - **Refresco**:  Nivel 100\r\n - **Limonada**: 6IV + nivel 100\r\n**Nota**: *Puedes cambiar la `Naturaleza` base de un Pokémon dándole el respectivo `Objeto Menta` y mostrándoselo al bot*.\r\n\r\n\r\n## **__Shiny__**\r\n- Dale al Pokémon este objeto: para este efecto.\r\n - **Antiquemar**: Shiny\r\n - **Despertar**: Shiny + 6IV\r\n - **Antiparalizador**: convierte un ppokemon shiny en no-shiny\r\n**Nota**: *Puedes hace shiny un huevo mostrándoselo al bot durante 3 - 5 segundos y cambiando a un pokemon de descarte.*\r\n\r\n### **__Tera__**\r\nPuedes cambiar el \"Tipo de Tera\" de un Pokémon mostrándoselo al bot un \"Objeto de Tera\" del tipo al que quieras cambiarlo. \r\n- Dale al Pokémon este objeto: para conseguir este efecto.\r\n - **Teralito agua**: Tipo Agua\r\n - **Teralito fuego**: Tipo Fuego\r\n - **Teralito eléctrico**: Tipo Eléctrico\r\n*etc*\r\n\r\n### __**PokeBall**__\r\nSimplemente apoda a cualquier pokemon en este formato: **?(ball_name)** por ejemplo, `?beastball` o `?beastba` si no caben todos. \r\n**Nota**: *No pidas cambios de ball ilegales. Por ejemplo, no intentes cambiar un Pokémon capturado en POGO por una bola amiga, ya que no están disponibles en POGO*.\r\n\r\n### __**Genero**__\r\nSimplemente apoda a cualquier Pokémon en este formato `!male` o `!female` para cambiar a ese género.\r\nNo pidas Pokémon sin género o sólo femenino/masculino para cambiar de género, el bot no hará cambios ilegales.\r\n\r\nBásicamente solo tienes que elegir que efecto quieres y darle el item o cambiarle el nick basándote en lo que dice la guía, luego usa el comando `.sr` y una vez que estés en el intercambio con el bot, ofrécele el Pokémon interesado, luego retíra la oferta e intercámbiale cualquier Pokémon que no te interese.");
                    break;
                case "brl":
                    builder.WithAuthor("Pokemons Entrenados", icon)
                           .WithDescription($"Nuestros Pokémons listos para batalla son __legendarios/míticos__ y otros pokemon populares usados para competición con **EV** entrenados. Estos tienen rastreadores de **HOME** válidos y pueden ir a **HOME**.\r\n\r\n__Comandos a utilizar__:\r\n\r\n- __**`{botPrefix}brl`**__ - Este comando te mostrará una lista de Pokémon entrenados.  Para filtrar, usa el comando + un nombre o número de página.\r\n      - __**`{botPrefix}brl calyrex`**__ - Para calyrex\r\n    - __**`{botPrefix}brl 2`**__ - para la página 2 de la lista completa.\r\n\r\nEntonces, verás el código para solicitar realmente el pokemon que quieres en la lista que te da el bot.");
                    break;
                case "clone":
                    builder.WithAuthor("Como Clonar un Pokemon", icon)
                           .WithDescription($"Para Clonar un Pokémon, simplemente ejecuta el comando __**`{botPrefix}clone`**__.  El bot te enviará un código de intercambio.  Cuando sea tu turno, el bot te enviará un mensaje y te dirá que está listo para intercambiar.  Enséñale primero el Pokémon que quieres clonar, te enviará un mensaje y te dirá que canceles el intercambio del Pokémon que le has enseñado y que elijas un nuevo Pokémon que quieras descartar a cambio del Pokémon clonado..");
                    break;
                case "ditto":
                    builder.WithAuthor("Ditto", icon)
                           .WithDescription($"Solicitar un Ditto para la cría con IVs, Idioma y Naturaleza especificados.\r\n\r\n__**`{botPrefix}ditto`**__/__**`{botPrefix}dt`**__ **<código opcional> <modificadores de estado> <idioma> <naturaleza>**\r\n\r\n**Nota**: Deben utilizarse todos los campos excepto el código opcional.\r\n\r\n### **Ejemplo**:\r\n__**`{botPrefix}ditto ATKSPE Japanese Modest`**__\r\n\r\nEl ejemplo anterior daría como resultado un 0ATK/0SPE Japanese Modest Ditto.\r\n\r\n__**Idiomas**__:\r\n```Japanese, English, French, Italian, German, Spanish, Korean, ChineseS, ChineseT.```\r\n\r\n__**Modificadores**__:\r\n```ATK for 0 Atk\r\nSPE for 0 Spe\r\nSPA for 0 SpA\r\nATKSPE for 0 Atk and 0 Spe\r\nATKSPESPA for 0 Atk, 0 Spe, 0 SpA```\r\n\r\n__**Naturaleza**__:\r\n```Se puede especificar cualquier naturaleza. El formato de entrada es <Nature>.```\r\n\r\n### Aqui mas Ejemplos:\n__**[German, 0 Atk, Adamant]**__:\r\n```{botPrefix}ditto ATK German Adamant```\r\n\r\n__**[French, 0 Spe, Hasty]**__:\r\n```{botPrefix}ditto SPE French Hasty```\r\n\r\n__**[Japanese, 0 Atk and 0 Spe, Modest]**__:\r\n```{botPrefix}ditto ATKSPE Japanese Modest```\r\n\r\n__**[Korean, 6IV, Timid]**__:\r\n```{botPrefix}ditto 6IV Korean Timid```");
                    break;
                case "fix":
                    builder.WithAuthor("Quitar Anuncios de Pokemons", icon)
                           .WithDescription($"Si recibiste un Pokémon que tiene el apodo de un sitio web, simplemente ejecuta el comando __**`{botPrefix}fix`**__ en ⁠canal correspondiente y arreglará el nombre por ti clonando y devolviéndote exactamente el mismo Pokémon, pero sin los anuncios.");
                    break;
                case "le":
                    builder.WithAuthor("Eventos", icon)
                           .WithDescription($"## Como pedir eventos\r\n\r\nPara obtener una lista de eventos, ejecute el comando __**`{botPrefix}le`**__ seguido del número de página o una letra específica, o ambos.\r\n\r\n### Ejemplo:\n     - Para filtrar la lista de eventos a todo lo que empiece por la letra D, utilice el comando __**`{botPrefix}le d`**__ \r\n           - Los 10 primeros eventos encontrados le serán enviados por __MD__ con el código correcto para solicitar ese evento.\n         - Si hay varias páginas de eventos que empiezan por d, puedes escribir __**`{botPrefix}le d 2`**__ para la página 2.\r\n\r\nUna vez que el bot te envíe la lista de eventos, verás el comando __**`{botPrefix}er`**__ y el número que te dirá que uses.\r\n\r\nVe al canal del bot (No al MD) y escribe el comando.");
                    break;
                case "bt":
                    builder.WithAuthor("Intercambio por lotes", icon)
                           .WithDescription($"Ahora puedes intercambiar por lotes varios Pokémons a la vez.  Para ello, utiliza la siguiente plantilla:\r\n\r\n### Importante:\nEl bot utilizará el mismo código de operación para cada trade por lotes.  El bot cerrará la operación después de cada operación exitosa, y buscará de nuevo usando el mismo código de operación para el siguiente trade.\r\n\r\n```{botPrefix}bt\r\n[Plantilla Showdown]\r\n---\r\n[Plantilla Showdown]\r\n---\r\n[Plantilla Showdown]```\r\n\r\n### __Asegúrese de separar cada trade con un__: **---**\r\n\r\n### He aquí un ejemplo:\r\n\r\n```{botPrefix}bt\r\nSolgaleo @ Ability Patch\r\nLevel: 100\r\nShiny: Yes\r\nEVs: 252 HP / 252 Atk / 6 Spe\r\nTera Type: Dark\r\n- Calm Mind\r\n- Close Combat\r\n- Cosmic Power\r\n- Heavy Slam\r\n---\r\nSpectrier @ Ability Patch\r\nLevel: 100\r\nEVs: 252 HP / 252 SpA / 6 Spe\r\nTera Type: Dark\r\n- Nasty Plot\r\n- Night Shade\r\n- Phantom Force\r\n- Shadow Ball\r\n---\r\nThundurus-Therian @ Ability Patch\r\nLevel: 100\r\nEVs: 6 Atk / 252 SpA / 252 Spe\r\nTera Type: Dark\r\n- Hammer Arm\r\n- Smart Strike\r\n- Taunt\r\n- Thunder Wave```");
                    break;
                case "me":
                    builder.WithAuthor("Huevo Misterioso", icon)
                           .WithDescription($"## Como pedir Huevos Misteriosos\r\nUtiliza el comando __`{botPrefix}Me`__ para intercambiar un huevo misterioso shiny aleatorio.\r\n\r\nLos Huevos Misteriosos siempre serán\r\n- __**Shinies**__\r\n- Tendrán __**IVs perfectos**__\r\n-  Tendrán __**Habilidad oculta**__");
                    break;
                case "egg":
                    builder.WithAuthor("Huevos", icon)
                           .WithDescription($"## Como pedir Huevos\r\nUtiliza el comando __`{botPrefix}Egg`__ seguido de tu set de showdown.\r\n\r\n### Ejemplo: \r\n__**`{botPrefix}Egg Charmander`**__\r\nShiny: Yes");
                    break;
                case "rt":
                    builder.WithAuthor("Equipo Random", icon)
                           .WithDescription($"# Generar un equipo aleatorio VGC\r\n\r\n## Comandos:\r\n- `{botPrefix}randomteam` o `{botPrefix}rt`\r\n\r\n## Descripción:\r\nGenera un equipo VGC aleatorio a partir de la hoja de cálculo VGCPastes. El bot creará un embed con información detallada sobre el equipo, incluyendo la Descripción del Equipo, Nombre del Entrenador, Fecha Compartida, y una visualización condicional del Código de Alquiler-si está disponible.\r\n\r\n## Características del embed:\r\n- **Descripción del equipo**: Ofrece una visión general del tema o estrategia del equipo.\r\n- **Nombre del formador**: Indica quién creó el equipo.\r\n- **Fecha de compartición**: Muestra la fecha de compartición del equipo.\r\n- **Código de alquiler**: Proporciona un código de acceso directo en el juego, mostrado sólo si está disponible.")
                           .WithImageUrl("https://i.imgur.com/jUBAz0a.png");
                    break;
                case "pp":
                    builder.WithAuthor("Equipo Completo a partir de PokePaste", icon)
                           .WithDescription($"# Generar equipos completos a partir de URLs PokePaste\r\n\r\n## Comando:\r\n- `{botPrefix}pp` o `{botPrefix}Pokepaste`\r\n\r\n## Descripción:\r\nPermite a los usuarios generar equipos completos Pokémon VGC directamente desde URLs de PokePaste. Esta función agiliza el proceso de compartir y utilizar equipos.\r\n\r\n## Modo de uso:\r\nEscribe el comando seguido de la URL de PokePaste que contiene el equipo que deseas utilizar. Por ejemplo\r\n```\r\n.pp <URL de PokePaste>\r\n```\r\n\r\nEste comando simplifica el uso compartido de equipos dentro de su comunidad o para la exploración personal de nuevos equipos.\r\n");
                    break;
                case "srp":
                    builder.WithAuthor("Pedir Regalos Misteriosos", icon)
                           .WithDescription($"# Guía de comandos Pokemon de Petición Especial\r\n\r\n## **🔍 Cómo funciona**\r\n\r\nEl usuario obtendrá una lista de eventos válidos para cada juego escribiendo `{botPrefix}srp <juego> <páginaX>`. Sustituye `<juego>` por el juego del que quieras obtener información. \r\n\r\n- Para Sword/Shield, escribe: `{botPrefix}srp swsh` para obtener una lista de Eventos Misteriosos de SwSh.\r\n- Para Escarlata/Violeta, escribe `{botPrefix}srp gen9` para ver los eventos misteriosos de Scarlet/Violet.\r\n- Para la página 2, escriba `{botPrefix}srp gen9 page2`\r\n\r\n**Juegos disponibles\r\n`{botPrefix}srp gen9` - Escarlata/Violeta\r\n`{botPrefix}srp bdsp` - Diamante brillante/Perla brillante\r\n`{botPrefix}srp swsh` - Espada/Escudo\r\n`{botPrefix}srp pla` - Leyendas: Arceus\r\n`{botPrefix}srp gen7` - Sol y Luna - Ultra Sol y Ultra Luna\r\n`{botPrefix}srp gen6` - Pokémon X e Y\r\n`{botPrefix}srp gen5` - Negro/Blanco - Negro2/Blanco2\r\n`{botPrefix}srp gen4` - Diamante y Perla - Platino\r\n`{botPrefix}srp gen3` - Rubí/Safiro/Esmeralda\r\n\r\nEl bot te enviará una lista de 25 eventos por página para que elijas, y te dará un código para que lo introduzcas en el canal de comercio.\r\n\r\nEl código será el siguiente `srp gen9 10` para el Evento índice 10.\r\n\r\n**Solicitudes entre juegos**\r\n\r\nTambién puedes solicitar eventos de otros juegos, y el bot te lo legalizará para ese juego en concreto.\r\n\r\nPor ejemplo, si quieres un evento de SwSh, pero para Scarlet/Violet, mirarás la lista de eventos para SwSh con `srp swsh` e introducirás el código en un bot de comercio de Scarlet/Violet para que haga ese evento de SwSh para ti.\r\n\r\n**Características principales\r\n\r\n- 📖 Fácil de usar con comandos simples.\r\n- 🌐 Compatibilidad entre juegos\r\n- 📥 Generación de wondercards automática y legal.\r\n- 🤖 No requiere configuración adicional para los propietarios de bots");
                    break;
                default:
                    builder.WithAuthor("Comando no encontrado", icon)
                          .WithDescription($"No se encontró información sobre el comando: `{command}`.");
                    break;
            }
        }
    }
}
