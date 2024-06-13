using System.Collections.Generic;
using System.Text.Json;
using System.IO;

namespace SysBot.Pokemon;

public class TradeCodeStorage
{
    private const string FileName = "tradecodes.json";
    private Dictionary<ulong, TradeCodeDetails> _tradeCodeDetails;

    public class TradeCodeDetails
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string Code { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string? OT { get; set; }
        public int TID { get; set; }
        public int SID { get; set; }
        public int TradeCount { get; set; }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public TradeCodeStorage()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        LoadFromFile();
    }

    public int GetTradeCode(ulong trainerID)
    {
        LoadFromFile();

        if (_tradeCodeDetails.TryGetValue(trainerID, out var details))
        {
            int code = int.Parse(details.Code); // Convierte de string a int
            details.TradeCount++;
            SaveToFile();
            return code;
        }
        else
        {
            var code = GenerateRandomTradeCode();
            _tradeCodeDetails[trainerID] = new TradeCodeDetails { Code = code.ToString("D8"), TradeCount = 1 };
            SaveToFile();
            return code;
        }
    }

    public bool SetTradeCode(ulong trainerID, int tradeCode)
    {
        // Convierte el entero a string aquí
        string tradeCodeStr = tradeCode.ToString("D8"); // Formatea como un número de 8 dígitos

        if (_tradeCodeDetails.ContainsKey(trainerID))
        {
            return false;
        }

        _tradeCodeDetails[trainerID] = new TradeCodeDetails { Code = tradeCodeStr, TradeCount = 1 };
        SaveToFile();
        return true;
    }

    public bool UpdateTradeCode(ulong trainerID, int newTradeCode)
    {
        if (!_tradeCodeDetails.ContainsKey(trainerID))
        {
            return false;
        }

        // Convierte el entero a string aquí también
        string newTradeCodeStr = newTradeCode.ToString("D8"); // Asegura que tenga 8 dígitos

        _tradeCodeDetails[trainerID].Code = newTradeCodeStr;
        SaveToFile();
        return true;
    }

    private static int GenerateRandomTradeCode()
    {
        var settings = new TradeSettings();
        return settings.GetRandomTradeCode();
    }

    private void LoadFromFile()
    {
        if (File.Exists(FileName))
        {
            string json = File.ReadAllText(FileName);
#pragma warning disable CS8601 // Possible null reference assignment.
            _tradeCodeDetails = JsonSerializer.Deserialize<Dictionary<ulong, TradeCodeDetails>>(json, SerializerOptions);
#pragma warning restore CS8601 // Possible null reference assignment.
        }
        else
        {
            _tradeCodeDetails = new Dictionary<ulong, TradeCodeDetails>();
        }
    }

    public bool DeleteTradeCode(ulong trainerID)
    {
        LoadFromFile();

        if (_tradeCodeDetails.Remove(trainerID))
        {
            SaveToFile();
            return true;
        }
        return false;
    }

    private void SaveToFile()
    {
        string json = JsonSerializer.Serialize(_tradeCodeDetails, SerializerOptions);
        File.WriteAllText(FileName, json);
    }

    public int GetTradeCount(ulong trainerID)
    {
        LoadFromFile();

        if (_tradeCodeDetails.TryGetValue(trainerID, out var details))
        {
            return details.TradeCount;
        }
        return 0;
    }

    public TradeCodeDetails? GetTradeDetails(ulong trainerID)
    {
        LoadFromFile();

        if (_tradeCodeDetails.TryGetValue(trainerID, out var details))
        {
            return details;
        }
        return null;
    }

    public void UpdateTradeDetails(ulong trainerID, string ot, int tid, int sid)
    {
        LoadFromFile();

        if (_tradeCodeDetails.TryGetValue(trainerID, out var details))
        {
            details.OT = ot;
            details.TID = tid;
            details.SID = sid;
            SaveToFile();
        }
    }
}
