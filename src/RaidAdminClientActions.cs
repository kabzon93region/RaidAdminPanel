using System;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RaidAdminPanel
{
    internal static class RaidAdminClientActions
    {
        public static bool Execute(string commandType, out string message)
        {
            switch (commandType)
            {
                case "ForceExtractSurvived":
                    return TryForceExtract(out message);
                case "RequestInventorySnapshot":
                    return TryInventorySnapshot(out message);
                case "PingClient":
                    message = "pong";
                    return true;
                default:
                    message = $"Unknown command: {commandType}";
                    return false;
            }
        }

        /// <summary>
        /// Как Fika console command extract — штатная высадка с сохранением лута через match/local/end.
        /// </summary>
        private static bool TryForceExtract(out string message)
        {
            var coopGame = ResolveCoopGame();
            if (coopGame == null)
            {
                message = "Не в Fika CoopGame";
                return false;
            }

            var statusProp = coopGame.GetType().GetProperty("Status");
            var status = statusProp?.GetValue(coopGame);
            if (status == null || status.ToString() != "Started")
            {
                message = $"Рейд не Started ({status})";
                return false;
            }

            var world = Singleton<GameWorld>.Instance;
            var mainPlayer = world?.MainPlayer;
            if (mainPlayer == null)
            {
                message = "MainPlayer не найден";
                return false;
            }

            if (!mainPlayer.HealthController.IsAlive)
            {
                message = "Игрок мёртв";
                return false;
            }

            var extract = coopGame.GetType().GetMethod("Extract", BindingFlags.Instance | BindingFlags.Public);
            if (extract == null)
            {
                message = "Extract method not found";
                return false;
            }

            extract.Invoke(coopGame, new object[] { mainPlayer, null });
            message = "Extract запущен (survived)";
            PluginCore.Log.LogInfo("[RaidAdminPanel] admin force extract executed");
            return true;
        }

        private static object ResolveCoopGame()
        {
            var fikaAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Fika.Core");
            if (fikaAsm == null)
            {
                return null;
            }

            var iface = fikaAsm.GetType("Fika.Core.Main.GameMode.IFikaGame");
            if (iface == null)
            {
                return null;
            }

            var singletonType = typeof(Singleton<>).MakeGenericType(iface);
            var instanceProp = singletonType.GetProperty("Instance");
            var game = instanceProp?.GetValue(null);
            if (game == null)
            {
                return null;
            }

            var coopType = fikaAsm.GetType("Fika.Core.Main.GameMode.CoopGame");
            return coopType != null && coopType.IsInstanceOfType(game) ? game : null;
        }

        private static bool TryInventorySnapshot(out string message)
        {
            var world = Singleton<GameWorld>.Instance;
            var mainPlayer = world?.MainPlayer;
            if (mainPlayer == null)
            {
                message = "Не в рейде";
                return false;
            }

            var items = mainPlayer.Inventory?.AllRealPlayerItems;
            var itemCount = items == null ? 0 : items.Count();
            var location = world?.LocationId ?? "unknown";

            var payload =
                $"{{\"note\":\"client snapshot\",\"raidLocation\":\"{location}\",\"itemCount\":{itemCount}}}";
            try
            {
                SPT.Common.Http.RequestHandler.PostJson("/raidadminpanel/client/inventory-snapshot", payload);
                message = $"Снимок отправлен (items={itemCount})";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }
    }
}
