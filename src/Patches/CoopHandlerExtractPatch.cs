using System.Reflection;
using HarmonyLib;
using RaidAdminPanel.Diagnostics;

namespace RaidAdminPanel.Patches
{
    /// <summary>
    /// Logs F8 / extract key attempts and block reasons from Fika CoopHandler.ProcessQuitting.
    /// </summary>
    [HarmonyPatch]
    internal static class CoopHandlerExtractPatch
    {
        internal static bool TryApply(Harmony harmony)
        {
            var coopHandlerType = AccessTools.TypeByName("Fika.Core.Main.Components.CoopHandler");
            if (coopHandlerType == null)
            {
                PluginCore.Log.LogInfo("[RaidAdminPanel.Extract] CoopHandler not found — F8 diagnostics skipped.");
                return false;
            }

            var method = AccessTools.Method(coopHandlerType, "ProcessQuitting");
            if (method == null)
            {
                PluginCore.Log.LogWarning("[RaidAdminPanel.Extract] ProcessQuitting not found.");
                return false;
            }

            harmony.Patch(method, prefix: new HarmonyMethod(typeof(CoopHandlerExtractPatch), nameof(Prefix)));
            PluginCore.Log.LogInfo("[RaidAdminPanel.Extract] CoopHandler.ProcessQuitting diagnostics enabled.");
            return true;
        }

        private static void Prefix(object __instance)
        {
            ExtractKeyDiagnostics.LogProcessQuittingAttempt(__instance);
        }
    }
}
