using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Z3;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysBot.Pokemon.ConsoleApp;

public static class Program
{
    private const string ConfigPath = "config.json";

    private static void ExitNoConfig()
    {
        var bot = new PokeBotState { Connection = new SwitchConnectionConfig { IP = "192.168.0.1", Port = 6000 }, InitialRoutine = PokeRoutineType.FlexTrade };
        var cfg = new ProgramConfig { Bots = [bot] };
        var created = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);
        File.WriteAllText(ConfigPath, created);
        Console.WriteLine("Creé un nuevo archivo de configuración ya que no se encontró ninguno en la ruta del programa. Por favor configúrelo y reinicie el programa.");
        Console.WriteLine("Se sugiere configurar este archivo de configuración usando el proyecto GUI si es posible, ya que le ayudará a asignar valores correctamente.");
        Console.WriteLine("Presiona cualquier tecla para salir.");
        Console.ReadKey();
    }

    private static void Main(string[] args)
    {
        Console.WriteLine("Empezando...");
        if (args.Length > 1)
            Console.WriteLine("Este programa no admite argumentos de línea de comando.");

        if (!File.Exists(ConfigPath))
        {
            ExitNoConfig();
            return;
        }

        try
        {
            var lines = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();
            PokeTradeBotSWSH.SeedChecker = new Z3SeedSearchHandler<PK8>();
            BotContainer.RunBots(cfg);
        }
        catch (Exception)
        {
            Console.WriteLine("No se pueden iniciar bots con el archivo de configuración guardado. Copie su configuración del proyecto Win Forms o elimínela y vuelva a configurarla.");
            Console.ReadKey();
        }
    }
}

[JsonSerializable(typeof(ProgramConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class ProgramConfigContext : JsonSerializerContext;

public static class BotContainer
{
    public static void RunBots(ProgramConfig prog)
    {
        IPokeBotRunner env = GetRunner(prog);
        foreach (var bot in prog.Bots)
        {
            bot.Initialize();
            if (!AddBot(env, bot, prog.Mode))
                Console.WriteLine($"No se pudo agregar el bot: {bot}");
        }

        LogUtil.Forwarders.Add(ConsoleForwarder.Instance);
        env.StartAll();
        Console.WriteLine($"Se iniciaron todos los bots (Recuento: {prog.Bots.Length}.");
        Console.WriteLine("Presione cualquier tecla para detener la ejecución y salir. ¡Siéntete libre de minimizar esta ventana!");
        Console.ReadKey();
        env.StopAll();
    }

    private static bool AddBot(IPokeBotRunner env, PokeBotState cfg, ProgramMode mode)
    {
        if (!cfg.IsValid())
        {
            Console.WriteLine($"La configuración de {cfg} no es válida.");
            return false;
        }

        PokeRoutineExecutorBase newBot;
        try
        {
            newBot = env.CreateBotFromConfig(cfg);
        }
        catch
        {
            Console.WriteLine($"El modo actual ({mode}) no admite este tipo de bot ({cfg.CurrentRoutineType}).");
            return false;
        }
        try
        {
            env.Add(newBot);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }

        Console.WriteLine($"Agregado: {cfg}: {cfg.InitialRoutine}");
        return true;
    }

    private static IPokeBotRunner GetRunner(ProgramConfig prog) => prog.Mode switch
    {
        ProgramMode.SWSH => new PokeBotRunnerImpl<PK8>(new PokeTradeHub<PK8>(prog.Hub), new BotFactory8SWSH(), prog),
        ProgramMode.BDSP => new PokeBotRunnerImpl<PB8>(new PokeTradeHub<PB8>(prog.Hub), new BotFactory8BS(), prog),
        ProgramMode.LA => new PokeBotRunnerImpl<PA8>(new PokeTradeHub<PA8>(prog.Hub), new BotFactory8LA(), prog),
        ProgramMode.SV => new PokeBotRunnerImpl<PK9>(new PokeTradeHub<PK9>(prog.Hub), new BotFactory9SV(), prog),
        ProgramMode.LGPE => new PokeBotRunnerImpl<PB7>(new PokeTradeHub<PB7>(prog.Hub), new BotFactory7LGPE(), prog),
        _ => throw new IndexOutOfRangeException("Modo no compatible."),
    };
}
