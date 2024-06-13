using System;
using System.Collections.Generic;

namespace SysBot.Base;

public static class EchoUtil
{
    public static readonly List<Action<string>> Forwarders = [];

    public static void Echo(string message)
    {
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd(message);
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"Excepción: {ex} ocurrió al intentar hacer eco: {message} al reenviador: {fwd}", "Echo");
                LogUtil.LogSafe(ex, "Echo");
            }
        }
        LogUtil.LogInfo(message, "Echo");
    }
}
