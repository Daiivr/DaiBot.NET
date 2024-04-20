using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Pokemon;
using SysBot.Pokemon.Discord;
using System;
using System.Threading.Tasks;

namespace TuBotDiscord.Modules;

public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    public class TutorialModule : ModuleBase<SocketCommandContext>
    {
        [Command("ayuda")]
        public async Task HelpAsync(string command = null)
        {
            var icon = "https://i.imgur.com/axXN5Sd.gif";
            var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
            var builder = new EmbedBuilder();
            // Obtener la URL del avatar del usuario y la hora actual
            var avatarUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl(); // Utiliza el avatar predeterminado si no tiene uno personalizado
            var currentTime = DateTime.Now.ToString("h:mm tt"); // Formato de 12 horas con AM/PM

            if (string.IsNullOrEmpty(command))
            {
                builder.WithTitle("Comandos disponibles")
                       .WithDescription("Usa `!ayuda <comando>` para obtener más información sobre un comando específico.")
                       .AddField("!ayuda sr", "Información sobre los **Pedidos Especiales**")
                       .AddField("!ayuda brl", "Información sobre los pedidos de **Pokemons Entrenados**")
                       .AddField("!ayuda le", "Información sobre los pedidos de **Pokemons de Eventos**")
                       .AddField("!ayuda bt", "Información sobre los pedidos de **Pokemons por Lotes**")
                       .AddField("!ayuda clone", "Información sobre el comando **Clone**")
                       .AddField("!ayuda fix", "Información sobre el comando **Fix**")
                       .AddField("!ayuda ditto", "Información sobre como pedir **Dittos**")
                       .AddField("!ayuda me", "Información sobre como pedir **Huevos Misteriosos**")
                       .AddField("!ayuda egg", "Información sobre como pedir **Huevos de un Pokemons específico**")
                       // Agrega el resto de los comandos aquí
                       .WithColor(Discord.Color.Blue);
            }
            else
            {
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
                               .WithDescription($"Ahora puedes intercambiar por lotes varios Pokémons a la vez.  Para ello, utiliza la siguiente plantilla:\r\n\r\n### Importante:\nEl bot utilizará el mismo código de operación para cada trade por lotes.  El bot cerrará la operación después de cada operación exitosa, y buscará de nuevo usando el mismo código de operación para el siguiente trade.\r\n\r\n```MimikyuPlsbt\r\n[Plantilla Showdown]\r\n---\r\n[Plantilla Showdown]\r\n---\r\n[Plantilla Showdown]```\r\n\r\n### __Asegúrese de separar cada trade con un__: **---**\r\n\r\n### He aquí un ejemplo:\r\n\r\n```{botPrefix}bt\r\nSolgaleo @ Ability Patch\r\nLevel: 100\r\nShiny: Yes\r\nEVs: 252 HP / 252 Atk / 6 Spe\r\nTera Type: Dark\r\n- Calm Mind\r\n- Close Combat\r\n- Cosmic Power\r\n- Heavy Slam\r\n---\r\nSpectrier @ Ability Patch\r\nLevel: 100\r\nEVs: 252 HP / 252 SpA / 6 Spe\r\nTera Type: Dark\r\n- Nasty Plot\r\n- Night Shade\r\n- Phantom Force\r\n- Shadow Ball\r\n---\r\nThundurus-Therian @ Ability Patch\r\nLevel: 100\r\nEVs: 6 Atk / 252 SpA / 252 Spe\r\nTera Type: Dark\r\n- Hammer Arm\r\n- Smart Strike\r\n- Taunt\r\n- Thunder Wave```");
                        break;
                    case "me":
                        builder.WithAuthor("Huevo Misterioso", icon)
                               .WithDescription($"## Como pedir Huevos Misteriosos\r\nUtiliza el comando __`{botPrefix}Me`__ para intercambiar un huevo misterioso shiny aleatorio.\r\n\r\nLos Huevos Misteriosos siempre serán\r\n- __**Shinies**__\r\n- Tendrán __**IVs perfectos**__\r\n-  Tendrán __**Habilidad oculta**__");
                        break;
                    case "egg":
                        builder.WithAuthor("Huevos", icon)
                               .WithDescription("## Como pedir Huevos\r\nUtiliza el comando __`MimikyuPlsEgg`__ seguido de tu set de showdown.\r\n\r\n### Ejemplo: \r\n__**`MimikyuPlsEgg Charmander`**__\r\nShiny: Yes");
                        break;
                    // Agrega casos para cada comando
                    default:
                        builder.WithAuthor("Comando no encontrado", icon)
                               .WithDescription($"No se encontró información sobre `{command}`.");
                        break;
                }

                builder.WithColor(Discord.Color.Red);
            }
            // Utiliza DeleteAsync para borrarlo.
            await Context.Message.DeleteAsync();
            builder.WithFooter(footer =>
            {
                footer.WithIconUrl(avatarUrl);
                footer.WithText($"{Context.User.Username} • {currentTime}");
            });
            builder.WithThumbnailUrl("https://i.imgur.com/lPU9wFp.png");

            await ReplyAsync(embed: builder.Build());
        }
    }
}
