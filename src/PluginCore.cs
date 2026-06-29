using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RaidAdminPanel.Patches;

namespace RaidAdminPanel
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public sealed class PluginCore : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private AdminCommandPoller _poller;
        private Harmony _harmony;

        /// <summary>
        /// Временный флаг для диагностики лагов.
        /// При false — PollLoop (HTTP poll команд админки) не запускается.
        /// </summary>
        internal ConfigEntry<bool> PollEnabled;

        /// <summary>
        /// F8/extract diagnostics (Harmony prefix на CoopHandler.ProcessQuitting).
        /// По умолчанию ВЫКЛЮЧЕНО — вызывает лаги из-за тяжёлой рефлексии.
        /// Включайте только при проблемах с массовым extract.
        /// </summary>
        internal ConfigEntry<bool> EnableF8Diagnostics;

        private void Awake()
        {
            Log = Logger;

            PollEnabled = Config.Bind("Debug", "PollEnabled", true,
                "Включить poll-цикл команд админ-панели (HTTP каждые 3 сек в рейде). " +
                "Выключите для диагностики лагов — если FPS вернётся, проблема в RaidAdminPanel poll.");

            EnableF8Diagnostics = Config.Bind("Debug", "EnableF8Diagnostics", false,
                "Включить Harmony-патч на Fika CoopHandler.ProcessQuitting (F8 diagnostics). " +
                "По умолчанию ВЫКЛЮЧЕНО — вызывает лаги. Включайте только при проблемах с массовым extract.");

            _poller = new AdminCommandPoller(Logger);

            if (PollEnabled.Value)
            {
                _poller.Start(this);
            }
            else
            {
                Logger.LogWarning("[RaidAdminPanel] PollLoop DISABLED via config (diagnostic mode)");
            }

            _harmony = new Harmony(PluginInfo.GUID);
            if (EnableF8Diagnostics.Value)
            {
                try
                {
                    CoopHandlerExtractPatch.TryApply(_harmony);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[RaidAdminPanel.Extract] F8 diagnostics patch failed: {ex.Message}");
                }
            }
            else
            {
                Logger.LogInfo("[RaidAdminPanel] F8 diagnostics disabled (default). Enable only if mass extract is broken.");
            }

            Logger.LogInfo($"{PluginInfo.NAME} v{PluginInfo.VERSION} loaded");
        }

        private void OnDestroy()
        {
            _poller?.Stop();
            _harmony?.UnpatchSelf();
        }
    }
}
