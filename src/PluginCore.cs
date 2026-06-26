using System;
using BepInEx;
using BepInEx.Logging;

namespace RaidAdminPanel
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public sealed class PluginCore : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private AdminCommandPoller _poller;

        private void Awake()
        {
            Log = Logger;
            _poller = new AdminCommandPoller(Logger);
            _poller.Start(this);
            Logger.LogInfo($"{PluginInfo.NAME} v{PluginInfo.VERSION} loaded");
        }

        private void OnDestroy()
        {
            _poller?.Stop();
        }
    }
}
