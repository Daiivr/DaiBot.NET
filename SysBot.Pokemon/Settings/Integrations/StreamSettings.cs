using PKHeX.Core;
using SysBot.Base;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public class StreamSettings
{
    private const string Operation = nameof(Operation);

    private static readonly byte[] BlackPixel = // 1x1 black pixel
    [
        0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
    ];

    public static Action<PKM, string>? CreateSpriteFile { get; set; }

    [Category(Operation), Description("Formato para mostrar las Operaciones Completadas. {0} = Conteo")]
    public string CompletedTradesFormat { get; set; } = "Operaciones completadas: {0}";

    [Category(Operation), Description("Copia el archivo de bloqueo comercial si existe; de ​​lo contrario, se copia una imagen de marcador de posición.")]
    public bool CopyImageFile { get; set; } = true;

    [Category(Operation), Description("Generar activos continuos; el apagado impedirá la generación de activos.")]
    public bool CreateAssets { get; set; }

    [Category(Operation), Description("Cree un archivo que indique el recuento de operaciones completadas cuando se inicia una nueva operación.")]
    public bool CreateCompletedTrades { get; set; } = true;

    [Category(Operation), Description("Crear un archivo que enumere la cantidad estimada de tiempo que un usuario tendrá que esperar si se unió a la cola.")]
    public bool CreateEstimatedTime { get; set; } = true;

    [Category(Operation), Description("Genere una lista de las personas que se encuentran actualmente en cubierta.")]
    public bool CreateOnDeck { get; set; } = true;

    [Category(Operation), Description("Generar una lista de personas actualmente en cubierta #2.")]
    public bool CreateOnDeck2 { get; set; } = true;

    [Category(Operation), Description("Genere detalles de inicio de operaciones, indicando con quién está operando el robot.")]
    public bool CreateTradeStart { get; set; } = true;

    [Category(Operation), Description("Genere detalles de inicio de comercio, indicando qué está comercializando el bot.")]
    public bool CreateTradeStartSprite { get; set; } = true;

    [Category(Operation), Description("Genere una lista de personas con las cuales actualmente se intercambian.")]
    public bool CreateUserList { get; set; } = true;

    [Category(Operation), Description("Cree un archivo que indique el recuento de usuarios en la cola.")]
    public bool CreateUsersInQueue { get; set; } = true;

    [Category(Operation), Description("Cree un archivo que enumere la cantidad de tiempo que ha esperado el usuario retirado de la cola más recientemente.")]
    public bool CreateWaitedTime { get; set; } = true;

    [Category(Operation), Description("Formato para mostrar la marca de tiempo de espera estimado.")]
    public string EstimatedFulfillmentFormat { get; set; } = @"hh\:mm\:ss";

    // Estimated Time
    [Category(Operation), Description("Formato para mostrar el tiempo de espera estimado.")]
    public string EstimatedTimeFormat { get; set; } = "Estimated time: {0:F1} minutes";

    [Category(Operation), Description("Formato para mostrar la lista de usuarios disponibles. {0} = ID, {3} = Usuario")]
    public string OnDeckFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("Formato para mostrar la lista de usuarios número 2 disponibles. {0} = ID, {3} = Usuario")]
    public string OnDeckFormat2 { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("Separador para dividir la lista de usuarios en cubierta.")]
    public string OnDeckSeparator { get; set; } = "\n";

    [Category(Operation), Description("Separador para dividir la lista de usuarios número 2 en cubierta.")]
    public string OnDeckSeparator2 { get; set; } = "\n";

    [Category(Operation), Description("Número de usuarios disponibles para omitir en la parte superior. Si desea ocultar las personas que se están procesando, configúrelo según su número de consolas.")]
    public int OnDeckSkip { get; set; }

    [Category(Operation), Description("Número de usuarios n.° 2 en cubierta que se deben omitir en la parte superior. Si desea ocultar las personas que se están procesando, configúrelo según su número de consolas.")]
    public int OnDeckSkip2 { get; set; }

    // On Deck
    [Category(Operation), Description("Número de usuarios a mostrar en la lista en cubierta.")]
    public int OnDeckTake { get; set; } = 5;

    // On Deck 2
    [Category(Operation), Description("Número de usuarios que se mostrarán en la lista número 2 disponible.")]
    public int OnDeckTake2 { get; set; } = 5;

    // TradeCodeBlock
    [Category(Operation), Description("Nombre del archivo fuente de la imagen que se copiará cuando se ingrese un código comercial. Si se deja vacío, se creará una imagen de marcador de posición.")]
    public string TradeBlockFile { get; set; } = string.Empty;

    [Category(Operation), Description("Nombre del archivo de destino de la imagen de bloqueo del Código de Enlace. {0} se reemplaza con la dirección IP local.")]
    public string TradeBlockFormat { get; set; } = "block_{0}.png";

    [Category(Operation), Description("Formato para mostrar los detalles de Now Trading. {0} = ID, {1} = Usuario")]
    public string TrainerTradeStart { get; set; } = "(ID {0}) {1}";

    [Category(Operation), Description("Formato para mostrar la lista de usuarios. {0} = ID, {3} = Usuario")]
    public string UserListFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("Separador para dividir la lista de usuarios.")]
    public string UserListSeparator { get; set; } = ", ";

    [Category(Operation), Description("Número de usuarios a saltar en la parte superior. Si desea ocultar las personas que se están procesando, configúrelo según su número de consolas.")]
    public int UserListSkip { get; set; }

    // User List
    [Category(Operation), Description("Número de usuarios a mostrar en la lista.")]
    public int UserListTake { get; set; } = -1;

    // Users in Queue
    [Category(Operation), Description("Formato para mostrar los usuarios en cola. {0} = Contar")]
    public string UsersInQueueFormat { get; set; } = "Users in Queue: {0}";

    // Waited Time
    [Category(Operation), Description("Formato para mostrar el tiempo de espera del usuario retirado de la cola más recientemente.")]
    public string WaitedTimeFormat { get; set; } = @"hh\:mm\:ss";

    public void EndEnterCode(PokeRoutineExecutorBase b)
    {
        try
        {
            var file = GetBlockFileName(b);
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public void IdleAssets(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly))
            {
                if (file.Contains(b.Connection.Name))
                    File.Delete(file);
            }

            if (CreateWaitedTime)
                File.WriteAllText("waited.txt", "00:00:00");
            if (CreateEstimatedTime)
            {
                File.WriteAllText("estimatedTime.txt", "Estimated time: 0 minutes");
                File.WriteAllText("estimatedTimestamp.txt", "");
            }
            if (CreateOnDeck)
                File.WriteAllText("ondeck.txt", "Waiting...");
            if (CreateOnDeck2)
                File.WriteAllText("ondeck2.txt", "Queue is empty!");
            if (CreateUserList)
                File.WriteAllText("users.txt", "None");
            if (CreateUsersInQueue)
                File.WriteAllText("queuecount.txt", "Users in Queue: 0");
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public void StartEnterCode(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            var file = GetBlockFileName(b);
            if (CopyImageFile && File.Exists(TradeBlockFile))
                File.Copy(TradeBlockFile, file);
            else
                File.WriteAllBytes(file, BlackPixel);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    // Completed Trades
    public void StartTrade<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail, PokeTradeHub<T> hub) where T : PKM, new()
    {
        if (!CreateAssets)
            return;

        try
        {
            if (CreateTradeStart)
                GenerateBotConnection(b, detail);
            if (CreateWaitedTime)
                GenerateWaitedTime(detail.Time);
            if (CreateEstimatedTime)
                GenerateEstimatedTime(hub);
            if (CreateUsersInQueue)
                GenerateUsersInQueue(hub.Queues.Info.Count);
            if (CreateOnDeck)
                GenerateOnDeck(hub);
            if (CreateOnDeck2)
                GenerateOnDeck2(hub);
            if (CreateUserList)
                GenerateUserList(hub);
            if (CreateCompletedTrades)
                GenerateCompletedTrades(hub);
            if (CreateTradeStartSprite)
                GenerateBotSprite(b, detail);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public override string ToString() => "Configuración de transmisión";

    private static void GenerateBotSprite<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var func = CreateSpriteFile;
        if (func == null)
            return;
        var file = b.Connection.Name;
        var pk = detail.TradeData;
        func.Invoke(pk, $"sprite_{file}.png");
    }

    private void GenerateBotConnection<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var file = b.Connection.Name;
        var name = string.Format(TrainerTradeStart, detail.ID, detail.Trainer.TrainerName, (Species)detail.TradeData.Species);
        File.WriteAllText($"{file}.txt", name);
    }

    private void GenerateCompletedTrades<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var msg = string.Format(CompletedTradesFormat, hub.Config.Trade.CountStatsSettings.CompletedTrades);
        File.WriteAllText("completed.txt", msg);
    }

    private void GenerateEstimatedTime<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var count = hub.Queues.Info.Count;
        var estimate = hub.Config.Queues.EstimateDelay(count, hub.Bots.Count);

        // Minutes
        var wait = string.Format(EstimatedTimeFormat, estimate);
        File.WriteAllText("estimatedTime.txt", wait);

        // Expected to be fulfilled at this time
        var now = DateTime.Now;
        var difference = now.AddMinutes(estimate);
        var date = difference.ToString(EstimatedFulfillmentFormat);
        File.WriteAllText("estimatedTimestamp.txt", date);
    }

    private void GenerateOnDeck<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat);
        ondeck = ondeck.Skip(OnDeckSkip).Take(OnDeckTake); // filter down
        File.WriteAllText("ondeck.txt", string.Join(OnDeckSeparator, ondeck));
    }

    private void GenerateOnDeck2<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat2);
        ondeck = ondeck.Skip(OnDeckSkip2).Take(OnDeckTake2); // filter down
        File.WriteAllText("ondeck2.txt", string.Join(OnDeckSeparator2, ondeck));
    }

    private void GenerateUserList<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var users = hub.Queues.Info.GetUserList(UserListFormat);
        users = users.Skip(UserListSkip);
        if (UserListTake > 0)
            users = users.Take(UserListTake); // filter down
        File.WriteAllText("users.txt", string.Join(UserListSeparator, users));
    }

    private void GenerateUsersInQueue(int count)
    {
        var value = string.Format(UsersInQueueFormat, count);
        File.WriteAllText("queuecount.txt", value);
    }

    private void GenerateWaitedTime(DateTime time)
    {
        var now = DateTime.Now;
        var difference = now - time;
        var value = difference.ToString(WaitedTimeFormat);
        File.WriteAllText("waited.txt", value);
    }

    private string GetBlockFileName(PokeRoutineExecutorBase b) => string.Format(TradeBlockFormat, b.Connection.Name);
}
