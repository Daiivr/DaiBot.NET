using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

namespace SysBot.Pokemon;

public class TradeCodeStorage
{
    private const string FileName = "tradecodes.json";
    private Dictionary<ulong, int> _tradeCodes;

    public TradeCodeStorage()
    {
        _tradeCodes = LoadFromFile();
    }

    public int GetTradeCode(ulong trainerID)
    {
        // Load the trade codes from the JSON file
        _tradeCodes = LoadFromFile();

        if (_tradeCodes.TryGetValue(trainerID, out int code))
            return code;

        code = GenerateRandomTradeCode();
        _tradeCodes[trainerID] = code;
        SaveToFile();
        return code;
    }

    public bool SetTradeCode(ulong trainerID, int tradeCode)
    {
        // Verifica si el usuario ya tiene un código almacenado y, en caso afirmativo, no actualiza el código.
        if (_tradeCodes.ContainsKey(trainerID))
        {
            return false; // Retorna falso para indicar que el código no se actualizó.
        }

        // Si el usuario no tiene un código almacenado, actualiza el código.
        _tradeCodes[trainerID] = tradeCode;
        SaveToFile(); // Guarda los cambios en el archivo.
        return true; // Retorna verdadero para indicar que el código se actualizó correctamente.
    }

    public bool UpdateTradeCode(ulong trainerID, int newTradeCode)
    {
        // Verifica si el usuario tiene un código almacenado y, en caso afirmativo, actualiza el código.
        if (_tradeCodes.ContainsKey(trainerID))
        {
            _tradeCodes[trainerID] = newTradeCode;
            SaveToFile(); // Guarda los cambios en el archivo.
            return true; // Retorna verdadero para indicar que el código se actualizó correctamente.
        }

        return false; // Retorna falso para indicar que el código no se actualizó porque no existía previamente.
    }

    private static int GenerateRandomTradeCode()
    {
        var settings = new TradeSettings();
        return settings.GetRandomTradeCode();
    }

    private static Dictionary<ulong, int> LoadFromFile()
    {
        if (File.Exists(FileName))
        {
            string json = File.ReadAllText(FileName);
            return JsonConvert.DeserializeObject<Dictionary<ulong, int>>(json);
        }
        return [];
    }

    public bool DeleteTradeCode(ulong trainerID)
    {
        // Load the trade codes from the JSON file
        _tradeCodes = LoadFromFile();

        if (_tradeCodes.Remove(trainerID))
        {
            SaveToFile();
            return true;
        }
        return false;
    }

    private void SaveToFile()
    {
        string json = JsonConvert.SerializeObject(_tradeCodes);
        File.WriteAllText(FileName, json);
    }
}
