using System;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RaidAdminPanel
{
    internal static class RaidAdminClientActions
    {
        public static bool Execute(string commandType, out string message)
        {
            PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] ENTER: commandType='{commandType}'");

            switch (commandType)
            {
                case "ForceExtractSurvived":
                case "ForceExtractAll":
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] Branch: {commandType}");
                    var result = TryForceExtract(out message, commandType == "ForceExtractAll");
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] {commandType} returned: {result}, message='{message}'");
                    return result;

                case "RequestInventorySnapshot":
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] Branch: RequestInventorySnapshot");
                    result = TryInventorySnapshot(out message);
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] RequestInventorySnapshot returned: {result}, message='{message}'");
                    return result;

                case "PingClient":
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Execute] Branch: PingClient -> pong");
                    message = "pong";
                    return true;

                default:
                    PluginCore.Log.LogWarning($"[RaidAdminPanel.Execute] Unknown command: '{commandType}'");
                    message = $"Unknown command: {commandType}";
                    return false;
            }
        }

        /// <summary>
        /// Принудительный extract: Fika F8-aware + safe bot dispose + emergency method_15.
        /// </summary>
        private static bool TryForceExtract(out string message, bool isMassExtract = false)
        {
            PluginCore.Log.LogInfo("[RaidAdminPanel.TryForceExtract] ENTER");

            if (!FikaExtractHelper.TryBuildPlan(out var plan, out message))
            {
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL plan: {message}");
                return false;
            }

            if (plan.IsExtractedWaiting)
            {
                PluginCore.Log.LogInfo("[RaidAdminPanel.TryForceExtract] Player on F8/extract waiting screen");
            }

            // Шаг 1: стандартный Stop (с safe dispose ботов для Fika)
            if (FikaExtractHelper.TryStandardStop(plan, out message))
            {
                ShowExtractNotification(isMassExtract);
                message = BuildSuccessMessage("Stop", plan, message);
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] SUCCESS: {message}");
                return true;
            }

            PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] Stop failed, trying emergency: {message}");

            // Шаг 2: emergency — method_15 (сохранение + выход в меню), даже если Stop упал посередине
            if (FikaExtractHelper.TryEmergencyExtract(plan, out var emergencyMsg))
            {
                ShowExtractNotification(isMassExtract, emergency: true);
                message = BuildSuccessMessage("Emergency", plan, emergencyMsg);
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] EMERGENCY SUCCESS: {message}");
                return true;
            }

            message = $"Stop: {message}; Emergency: {emergencyMsg}";
            PluginCore.Log.LogError($"[RaidAdminPanel.TryForceExtract] ALL PATHS FAILED: {message}");
            return false;
        }

        private static void ShowExtractNotification(bool isMassExtract, bool emergency = false)
        {
            var prefix = emergency ? "Аварийный выход (админка)" : "Сервер отправил";
            var text = isMassExtract ? $"{prefix}: массовая высадка" : $"{prefix} вас на выход";
            NotificationManagerClass.DisplayMessageNotification(text);
        }

        private static string BuildSuccessMessage(string path, FikaExtractHelper.ExtractPlan plan, string detail)
        {
            var waiting = plan.IsExtractedWaiting ? " [F8-wait]" : string.Empty;
            return $"{path} extract{waiting} @ {plan.ExitName} ({detail})";
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
