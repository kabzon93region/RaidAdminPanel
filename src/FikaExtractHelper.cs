using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace RaidAdminPanel
{
    /// <summary>
    /// Fika coop extract: учитывает экран F8 (ExitLocation/ExitStatus) и обходит падение Stop() при Dispose ботов.
    /// </summary>
    internal static class FikaExtractHelper
    {
        internal sealed class ExtractPlan
        {
            internal object Game;
            internal string ProfileId;
            internal ExitStatus ExitStatus;
            internal string ExitName;
            internal bool IsExtractedWaiting;
            internal bool IsFikaCoop;
            internal object CoopHandler;
        }

        internal static bool TryBuildPlan(out ExtractPlan plan, out string error)
        {
            plan = null;
            error = null;

            var world = Singleton<GameWorld>.Instance;
            if (world == null)
            {
                error = "GameWorld == null";
                return false;
            }

            var mainPlayer = world.MainPlayer;
            if (mainPlayer == null)
            {
                error = "MainPlayer == null";
                return false;
            }

            var game = ResolveGameInstance();
            if (game == null)
            {
                error = "LocalGame/CoopGame не найден";
                return false;
            }

            var isFikaCoop = game.GetType().FullName?.Contains("CoopGame") == true;
            var coopHandler = isFikaCoop ? ResolveCoopHandler(game) : null;
            var isExtractedWaiting = isFikaCoop && IsExtractedWaiting(coopHandler, game, mainPlayer);

            if (!isExtractedWaiting && mainPlayer.HealthController != null && !mainPlayer.HealthController.IsAlive)
            {
                error = "Игрок мёртв";
                return false;
            }

            if (!TryResolveExitName(world, mainPlayer, game, isExtractedWaiting, out var exitName, out error))
            {
                return false;
            }

            var exitStatus = ResolveExitStatus(game, isExtractedWaiting);

            plan = new ExtractPlan
            {
                Game = game,
                ProfileId = mainPlayer.ProfileId,
                ExitStatus = exitStatus,
                ExitName = exitName,
                IsExtractedWaiting = isExtractedWaiting,
                IsFikaCoop = isFikaCoop,
                CoopHandler = coopHandler
            };

            PluginCore.Log.LogInfo(
                $"[RaidAdminPanel.Extract] plan: fika={isFikaCoop} waitingF8={isExtractedWaiting} " +
                $"status={exitStatus} exit='{exitName}' profile={plan.ProfileId}");

            return true;
        }

        internal static bool TryStandardStop(ExtractPlan plan, out string message)
        {
            message = null;

            if (plan.IsFikaCoop)
            {
                SafeDisposeAndClearCoopPlayers(plan.CoopHandler);
            }

            return InvokeStop(plan.Game, plan.ProfileId, plan.ExitStatus, plan.ExitName, out message);
        }

        internal static bool TryEmergencyExtract(ExtractPlan plan, out string message)
        {
            PluginCore.Log.LogWarning("[RaidAdminPanel.Extract] EMERGENCY path — method_15 + UI cleanup");

            try
            {
                if (plan.IsFikaCoop)
                {
                    SafeDisposeAndClearCoopPlayers(plan.CoopHandler);
                }

                TryCloseScreens();
                TryStartBlackScreen(plan.Game);

                if (InvokeMethod15(plan.Game, plan.ProfileId, plan.ExitStatus, plan.ExitName, out message))
                {
                    message = $"Emergency extract: method_15 ({plan.ExitName})";
                    return true;
                }

                message = message ?? "method_15 failed";
                return false;
            }
            catch (Exception ex)
            {
                message = FormatException(ex);
                PluginCore.Log.LogError($"[RaidAdminPanel.Extract] Emergency failed: {message}");
                return false;
            }
        }

        private static bool TryResolveExitName(
            GameWorld world,
            Player mainPlayer,
            object game,
            bool isExtractedWaiting,
            out string exitName,
            out string error)
        {
            error = null;
            exitName = null;

            if (isExtractedWaiting)
            {
                exitName = GetGameStringProperty(game, "ExitLocation");
                if (!string.IsNullOrEmpty(exitName))
                {
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.Extract] F8 waiting — ExitLocation='{exitName}'");
                    return true;
                }
            }

            var controller = world.ExfiltrationController;
            if (controller == null)
            {
                error = "ExfiltrationController не найден";
                return false;
            }

            ExfiltrationPoint exfilPoint;
            if (mainPlayer.IsYourPlayer && mainPlayer.Fraction == ETagStatus.Scav)
            {
                var scavPoints = controller.ScavExfiltrationPoints.Where(x => x.isActiveAndEnabled).ToArray();
                if (scavPoints.Length == 0)
                {
                    error = "Нет активных выходов Scav";
                    return false;
                }

                exfilPoint = scavPoints[UnityEngine.Random.Range(0, scavPoints.Length)];
            }
            else
            {
                var pmcPoints = controller.ExfiltrationPoints.Where(x => x.isActiveAndEnabled).ToArray();
                if (pmcPoints.Length == 0)
                {
                    error = "Нет активных выходов PMC";
                    return false;
                }

                exfilPoint = pmcPoints[UnityEngine.Random.Range(0, pmcPoints.Length)];
            }

            exitName = exfilPoint.Settings.Name;
            return true;
        }

        private static ExitStatus ResolveExitStatus(object game, bool isExtractedWaiting)
        {
            if (!isExtractedWaiting)
            {
                return ExitStatus.Survived;
            }

            var prop = game.GetType().GetProperty("ExitStatus", BindingFlags.Instance | BindingFlags.Public);
            if (prop?.GetValue(game) is ExitStatus status)
            {
                return status;
            }

            return ExitStatus.Survived;
        }

        private static bool IsExtractedWaiting(object coopHandler, object game, Player mainPlayer)
        {
            if (coopHandler == null || game == null || mainPlayer == null)
            {
                return false;
            }

            try
            {
                var quitStateProp = coopHandler.GetType().GetProperty("QuitState", BindingFlags.Instance | BindingFlags.Public);
                if (quitStateProp?.GetValue(coopHandler) is Enum quitState)
                {
                    if (quitState.ToString() == "Extracted")
                    {
                        return true;
                    }
                }

                var extractedProp = game.GetType().GetProperty("ExtractedPlayers", BindingFlags.Instance | BindingFlags.Public);
                if (extractedProp?.GetValue(game) is IList extractedList)
                {
                    var netIdProp = mainPlayer.GetType().GetProperty("NetId", BindingFlags.Instance | BindingFlags.Public);
                    if (netIdProp?.GetValue(mainPlayer) is int netId && extractedList.Contains(netId))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogDebug($"[RaidAdminPanel.Extract] IsExtractedWaiting: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// CoopGame.Stop() падает, если Dispose бота кидает (BossNotifier.Fika и т.п.).
        /// Убираем ботов из CoopHandler до Stop и глотаем ошибки Dispose.
        /// </summary>
        internal static void SafeDisposeAndClearCoopPlayers(object coopHandler)
        {
            if (coopHandler == null)
            {
                return;
            }

            try
            {
                var playersProp = coopHandler.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public);
                if (playersProp?.GetValue(coopHandler) is not IDictionary playersDict)
                {
                    PluginCore.Log.LogWarning("[RaidAdminPanel.Extract] CoopHandler.Players not found");
                    return;
                }

                var keys = new List<object>();
                foreach (DictionaryEntry entry in playersDict)
                {
                    keys.Add(entry.Key);
                }

                var disposed = 0;
                var errors = 0;

                foreach (var key in keys)
                {
                    if (playersDict[key] is not object player || player == null)
                    {
                        continue;
                    }

                    var isYourPlayer = player.GetType().GetProperty("IsYourPlayer")?.GetValue(player) as bool? == true;
                    if (isYourPlayer)
                    {
                        continue;
                    }

                    try
                    {
                        player.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public)?.Invoke(player, null);

                        var goProp = player.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public);
                        var go = goProp?.GetValue(player) as GameObject;
                        if (go != null)
                        {
                            var poolType = Type.GetType("AssetPoolObject, Assembly-CSharp");
                            var returnMethod = poolType?.GetMethod(
                                "ReturnToPool",
                                BindingFlags.Static | BindingFlags.Public,
                                null,
                                new[] { typeof(GameObject), typeof(bool) },
                                null);
                            returnMethod?.Invoke(null, new object[] { go, true });
                        }

                        disposed++;
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        var nick = player.GetType().GetProperty("Profile")?.GetValue(player)
                            ?.GetType().GetMethod("GetCorrectedNickname")?.Invoke(null, null) ?? "?";
                        PluginCore.Log.LogWarning($"[RaidAdminPanel.Extract] SafeDispose skip [{nick}]: {ex.InnerException?.Message ?? ex.Message}");
                    }

                    playersDict.Remove(key);
                }

                PluginCore.Log.LogInfo($"[RaidAdminPanel.Extract] SafeDispose: removed={disposed} errors={errors} remaining={playersDict.Count}");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogWarning($"[RaidAdminPanel.Extract] SafeDisposeAndClearCoopPlayers: {ex.Message}");
            }
        }

        private static bool InvokeStop(object game, string profileId, ExitStatus exitStatus, string exitName, out string message)
        {
            message = null;

            var stopMethod = game.GetType().GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public);
            if (stopMethod == null)
            {
                message = "Stop method not found";
                return false;
            }

            var parameters = stopMethod.GetParameters();
            object[] args;

            if (parameters.Length == 4)
            {
                args = new object[] { profileId, exitStatus, exitName, 0f };
            }
            else if (parameters.Length == 3)
            {
                args = new object[] { profileId, exitStatus, exitName };
            }
            else
            {
                message = $"Unexpected Stop param count: {parameters.Length}";
                return false;
            }

            PluginCore.Log.LogInfo(
                $"[RaidAdminPanel.Extract] Stop({profileId}, {exitStatus}, '{exitName}', delay=0)");

            try
            {
                stopMethod.Invoke(game, args);
                message = $"Stop() invoked ({exitName})";
                return true;
            }
            catch (Exception ex)
            {
                message = FormatException(ex);
                PluginCore.Log.LogError($"[RaidAdminPanel.Extract] Stop failed: {message}");
                return false;
            }
        }

        private static bool InvokeMethod15(object game, string profileId, ExitStatus exitStatus, string exitName, out string message)
        {
            message = null;

            var method15 = game.GetType().GetMethod(
                "method_15",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method15 == null)
            {
                message = "method_15 not found";
                return false;
            }

            PluginCore.Log.LogInfo($"[RaidAdminPanel.Extract] method_15({profileId}, {exitStatus}, '{exitName}', 0)");

            try
            {
                var taskObj = method15.Invoke(game, new object[] { profileId, exitStatus, exitName, 0f });
                InvokeHandleExceptions(taskObj);
                message = "method_15 started";
                return true;
            }
            catch (Exception ex)
            {
                message = FormatException(ex);
                return false;
            }
        }

        private static void InvokeHandleExceptions(object taskObj)
        {
            if (taskObj == null)
            {
                return;
            }

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (method.Name != "HandleExceptions")
                            {
                                continue;
                            }

                            var ps = method.GetParameters();
                            if (ps.Length != 1 || !ps[0].ParameterType.IsInstanceOfType(taskObj))
                            {
                                continue;
                            }

                            method.Invoke(null, new[] { taskObj });
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogDebug($"[RaidAdminPanel.Extract] HandleExceptions not found: {ex.Message}");
            }
        }

        private static void TryCloseScreens()
        {
            try
            {
                var screens = CurrentScreenSingletonClass.Instance;
                screens?.CloseAllScreensForced();
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogDebug($"[RaidAdminPanel.Extract] CloseAllScreensForced: {ex.Message}");
            }
        }

        private static void TryStartBlackScreen(object game)
        {
            try
            {
                var preloaderType = Type.GetType("EFT.UI.PreloaderUI, Assembly-CSharp");
                if (preloaderType == null)
                {
                    return;
                }

                var instanceProp = preloaderType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                var preloader = instanceProp?.GetValue(null);
                if (preloader == null)
                {
                    return;
                }

                var startMethod = preloaderType.GetMethod("StartBlackScreenShow", BindingFlags.Instance | BindingFlags.Public);
                if (startMethod == null)
                {
                    return;
                }

                startMethod.Invoke(preloader, new object[] { 1f, 1f, (Action)(() => { }) });
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogDebug($"[RaidAdminPanel.Extract] StartBlackScreenShow: {ex.Message}");
            }
        }

        private static object ResolveCoopHandler(object game)
        {
            try
            {
                var gameController = game.GetType().GetProperty("GameController", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(game);
                return gameController?.GetType().GetProperty("CoopHandler", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(gameController);
            }
            catch
            {
                return null;
            }
        }

        internal static object ResolveGameInstance()
        {
            var world = Singleton<GameWorld>.Instance;
            if (world == null)
            {
                return null;
            }

            var gameProp = typeof(GameWorld).GetProperty("Game", BindingFlags.Instance | BindingFlags.Public);
            var game = gameProp?.GetValue(world);
            if (game != null)
            {
                return game;
            }

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
            return singletonType.GetProperty("Instance")?.GetValue(null);
        }

        private static string GetGameStringProperty(object game, string propertyName)
        {
            return game.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(game) as string;
        }

        internal static string FormatException(Exception ex)
        {
            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                return $"{tie.InnerException.GetType().Name}: {tie.InnerException.Message}";
            }

            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
