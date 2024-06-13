using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;

namespace SysBot.Pokemon.Twitch
{
    public static class TwitchCommandsHelper<T> where T : PKM, new()
    {
        // Helper functions for commands
        public static bool AddToWaitingList(string setstring, string display, string username, ulong mUserId, bool sub, out string msg)
        {
            if (!TwitchBot<T>.Info.GetCanQueue())
            {
                msg = "❌ Lo siento, actualmente no acepto solicitudes de cola!";
                return false;
            }

            var set = ShowdownUtil.ConvertToShowdown(setstring);
            if (set == null)
            {
                msg = $"⚠️ Omitiendo intercambio, @{username}: apodo vacío proporcionado para la especie.";
                return false;
            }
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (template.Species < 1)
            {
                msg = $"⚠️ Saltándose el intercambio, @{username}: lea lo que se supone que debe escribir como argumento del comando.";
                return false;
            }

            if (set.InvalidLines.Count != 0)
            {
                msg = $"⚠️ Omitiendo intercambio, @{username}: No se puede analizar el conjunto de enfrentamiento:\n{string.Join("\n", set.InvalidLines)}";
                return false;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                PKM pkm = sav.GetLegal(template, out var result);

                var nickname = pkm.Nickname.ToLower();
                if (nickname == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    AbstractTrade<T>.EggTrade(pkm, template);

                if (pkm.Species == 132 && (nickname.Contains("atk") || nickname.Contains("spa") || nickname.Contains("spe") || nickname.Contains("6iv")))
                    AbstractTrade<T>.DittoTrade(pkm);

                if (!pkm.CanBeTraded())
                {
                    msg = $"⚠️ Omitiendo intercambio, @{username}: Contenido de Pokémon bloqueado para el intercambio!";
                    return false;
                }

                if (pkm is T pk)
                {
                    var valid = new LegalityAnalysis(pkm).Valid;
                    if (valid)
                    {
                        var tq = new TwitchQueue<T>(pk, new PokeTradeTrainerInfo(display, mUserId), username, sub);
                        TwitchBot<T>.QueuePool.RemoveAll(z => z.UserName == username); // remove old requests if any
                        TwitchBot<T>.QueuePool.Add(tq);
                        msg = $"✅ @{username} ➜ ¡Añadido a la lista de espera! ¡Susurrame tu código comercial! ¡Si eres demasiado lento, tu solicitud de la lista de espera se eliminará!";
                        return true;
                    }
                }

                var reason = result == "Timeout" ? "El conjunto tardó demasiado en generarse." : "No se puede legalizar el Pokémon.";
                msg = $"⚠️ Omitiendo intercambio, @{username}: {reason}";
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TwitchCommandsHelper<T>));
                msg = $"⚠️ Omitiendo intercambio, @{username}: Ocurrió un problema inesperado.";
            }
            return false;
        }

        public static string ClearTrade(string user)
        {
            var result = TwitchBot<T>.Info.ClearTrade(user);
            return GetClearTradeMessage(result);
        }

        public static string ClearTrade(ulong userID)
        {
            var result = TwitchBot<T>.Info.ClearTrade(userID);
            return GetClearTradeMessage(result);
        }

        public static string GetCode(ulong parse)
        {
            var detail = TwitchBot<T>.Info.GetDetail(parse);
            return detail == null
                ? "⚠️ Lo sentimos, actualmente no estás en la cola."
                : $"✅ Su código comercial es {detail.Trade.Code:0000 0000}";
        }

        private static string GetClearTradeMessage(QueueResultRemove result)
        {
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "⚠️ ¡Parece que estás siendo procesado actualmente! No se te eliminó de la cola.",
                QueueResultRemove.CurrentlyProcessingRemoved => "⚠️ ¡Parece que estás siendo procesado actualmente! Eliminado de la cola.",
                QueueResultRemove.Removed => "✅ Te eliminé de la cola.",
                _ => "⚠️ Lo sentimos, actualmente no estás en la cola.",
            };
        }
    }
}
