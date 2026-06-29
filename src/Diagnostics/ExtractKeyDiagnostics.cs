using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;

namespace RaidAdminPanel.Diagnostics
{
    internal static class ExtractKeyDiagnostics
    {
        private static PropertyInfo _quitStateProp;
        private static FieldInfo _requestQuitField;
        private static FieldInfo _isClientField;
        private static PropertyInfo _extractKeyProp;
        private static float _lastBlockedLogTime;

        internal static void LogProcessQuittingAttempt(object coopHandler)
        {
            if (coopHandler == null || !TryProbe(coopHandler.GetType()))
            {
                return;
            }

            if (!IsExtractKeyDown())
            {
                return;
            }

            var quitState = ReadEnum(_quitStateProp?.GetValue(coopHandler));
            var requestQuit = ReadBool(_requestQuitField?.GetValue(coopHandler));
            var isClient = ReadBool(_isClientField?.GetValue(coopHandler));

            var world = Singleton<GameWorld>.Instance;
            var mainPlayer = world?.MainPlayer;
            var alive = mainPlayer?.HealthController?.IsAlive ?? false;
            var exitLocation = ResolveExitLocation(coopHandler);
            var extractedWaiting = FikaExtractHelper.TryBuildPlan(out var plan, out _)
                && plan.IsExtractedWaiting;

            var now = UnityEngine.Time.unscaledTime;
            var blocked = quitState == "None" || requestQuit;
            if (blocked && now - _lastBlockedLogTime < 2f)
            {
                return;
            }

            if (blocked)
            {
                _lastBlockedLogTime = now;
            }

            PluginCore.Log.LogInfo(
                $"[RaidAdminPanel.Extract] F8 key down quitState={quitState} requestQuit={requestQuit} " +
                $"isClient={isClient} alive={alive} extractedWaiting={extractedWaiting} exitLocation='{exitLocation}'");

            if (quitState == "None")
            {
                PluginCore.Log.LogWarning(
                    "[RaidAdminPanel.Extract] F8 blocked: QuitState=None (not at extract / not in end-raid state).");
            }
        }

        private static bool TryProbe(Type coopHandlerType)
        {
            if (_quitStateProp != null)
            {
                return true;
            }

            _quitStateProp = coopHandlerType.GetProperty("QuitState", BindingFlags.Instance | BindingFlags.Public);
            _requestQuitField = coopHandlerType.GetField("_requestQuitGame", BindingFlags.Instance | BindingFlags.NonPublic);
            _isClientField = coopHandlerType.GetField("_isClient", BindingFlags.Instance | BindingFlags.NonPublic);

            var fikaPluginType = AccessTools.TypeByName("Fika.Core.FikaPlugin");
            var settingsProp = fikaPluginType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            var instance = settingsProp?.GetValue(null);
            var settings = instance?.GetType().GetProperty("Settings")?.GetValue(instance);
            _extractKeyProp = settings?.GetType().GetProperty("ExtractKey");

            return _quitStateProp != null;
        }

        private static bool IsExtractKeyDown()
        {
            try
            {
                var fikaPluginType = AccessTools.TypeByName("Fika.Core.FikaPlugin");
                var instance = fikaPluginType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                var settings = instance?.GetType().GetProperty("Settings")?.GetValue(instance);
                var extractKey = settings?.GetType().GetProperty("ExtractKey")?.GetValue(settings);
                if (extractKey == null)
                {
                    return false;
                }

                var isDownMethod = extractKey.GetType().GetMethod("IsDown");
                return isDownMethod != null && isDownMethod.Invoke(extractKey, null) is bool down && down;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveExitLocation(object coopHandler)
        {
            try
            {
                var localGameProp = coopHandler.GetType().GetProperty("LocalGameInstance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var game = localGameProp?.GetValue(coopHandler);
                return game?.GetType().GetProperty("ExitLocation")?.GetValue(game) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadEnum(object value)
        {
            return value == null ? "null" : value.ToString();
        }

        private static bool ReadBool(object value)
        {
            return value is bool b && b;
        }
    }
}
