using System;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Interactive;
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
        /// Как Fika console command extract — штатная высадка с сохранением лута через match/local/end.
        /// Использует метод Stop() как SPTExfilNow, а не Extract().
        /// </summary>
        private static bool TryForceExtract(out string message, bool isMassExtract = false)
        {
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] ENTER");
            
            var game = ResolveGameInstance();
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] ResolveGameInstance: {(game == null ? "NULL" : game.GetType().Name)}");
            
            if (game == null)
            {
                message = "Не в рейде (LocalGame/CoopGame не найден)";
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                return false;
            }

            var world = Singleton<GameWorld>.Instance;
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] GameWorld: {(world == null ? "NULL" : "OK")}");
            
            var mainPlayer = world?.MainPlayer;
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] MainPlayer: {(mainPlayer == null ? "NULL" : $"OK, Id={mainPlayer.ProfileId}, Fraction={mainPlayer.Fraction}, IsYourPlayer={mainPlayer.IsYourPlayer}")}");
            
            if (mainPlayer == null)
            {
                message = "MainPlayer не найден";
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                return false;
            }

            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] IsAlive: {mainPlayer.HealthController?.IsAlive}");
            if (!mainPlayer.HealthController.IsAlive)
            {
                message = "Игрок мёртв";
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                return false;
            }

            // Выбираем точку выхода
            var exfiltrationController = world.ExfiltrationController;
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] ExfiltrationController: {(exfiltrationController == null ? "NULL" : "OK")}");
            
            if (exfiltrationController == null)
            {
                message = "ExfiltrationController не найден";
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                return false;
            }

            EFT.Interactive.ExfiltrationPoint exfilPoint;
            if (mainPlayer.IsYourPlayer && mainPlayer.Fraction == ETagStatus.Scav)
            {
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Player is Scav, looking for ScavExfiltrationPoints");
                var scavPoints = exfiltrationController.ScavExfiltrationPoints
                    .Where(x => x.isActiveAndEnabled).ToArray();
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] ScavExfiltrationPoints active: {scavPoints.Length}");
                
                if (scavPoints.Length == 0)
                {
                    message = "Нет доступных точек выхода для Scav";
                    PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                    return false;
                }
                exfilPoint = scavPoints[UnityEngine.Random.Range(0, scavPoints.Length)];
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Selected Scav exfil: {exfilPoint.Settings.Name}");
            }
            else
            {
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Player is PMC, looking for ExfiltrationPoints");
                var pmcPoints = exfiltrationController.ExfiltrationPoints
                    .Where(x => x.isActiveAndEnabled).ToArray();
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] ExfiltrationPoints active: {pmcPoints.Length}");
                
                if (pmcPoints.Length == 0)
                {
                    message = "Нет доступных точек выхода для PMC";
                    PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                    return false;
                }
                exfilPoint = pmcPoints[UnityEngine.Random.Range(0, pmcPoints.Length)];
                PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Selected PMC exfil: {exfilPoint.Settings.Name}");
            }

            var exfilName = exfilPoint.Settings.Name;
            var profileId = mainPlayer.ProfileId;
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] exfilName='{exfilName}', profileId='{profileId}'");

            // Вызываем Stop() как SPTExfilNow
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Game type: {game.GetType().FullName}");
            var stopMethod = game.GetType().GetMethod("Stop", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] Stop method: {(stopMethod == null ? "NOT FOUND" : $"OK, params: {string.Join(", ", stopMethod.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}")}");
            
            if (stopMethod == null)
            {
                message = "Stop method not found";
                PluginCore.Log.LogWarning($"[RaidAdminPanel.TryForceExtract] FAIL: {message}");
                return false;
            }

            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] INVOKING Stop(profileId={profileId}, ExitStatus.Survived, exfilName={exfilName}, delay=0)");
            stopMethod.Invoke(game, new object[] { profileId, EFT.ExitStatus.Survived, exfilName, 0f });
            
            // Show notification to player
            var notificationText = isMassExtract ? "Сервер отправил всех на выход" : "Сервер отправил вас на выход";
            NotificationManagerClass.DisplayMessageNotification(notificationText);
            
            message = $"Extract запущен через Stop() на точке {exfilName}";
            PluginCore.Log.LogInfo($"[RaidAdminPanel.TryForceExtract] SUCCESS: {message}");
            return true;
        }

        /// <summary>
        /// Получает экземпляр игры (LocalGame или CoopGame через Singleton).
        /// </summary>
        private static object? ResolveGameInstance()
        {
            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] ENTER");
            
            // Пробуем получить через Singleton<GameWorld> и его свойство Game
            var world = Singleton<GameWorld>.Instance;
            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] GameWorld: {(world == null ? "NULL" : "OK")}");
            
            if (world == null)
            {
                PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] RETURN NULL (no GameWorld)");
                return null;
            }

            // В SPT/Fika GameWorld имеет свойство Game
            var gameProp = typeof(GameWorld).GetProperty("Game", BindingFlags.Instance | BindingFlags.Public);
            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] GameWorld.Game property: {(gameProp == null ? "NOT FOUND" : $"OK ({gameProp.PropertyType.Name})")}");
            
            var game = gameProp?.GetValue(world);
            if (game != null)
            {
                PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] RETURN via GameWorld.Game: {game.GetType().FullName}");
                return game;
            }

            // Fallback: пробуем найти через Fika IFikaGame Singleton
            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] Trying Fika IFikaGame fallback...");
            var fikaAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Fika.Core");
            
            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] Fika.Core assembly: {(fikaAsm == null ? "NOT FOUND" : $"OK ({fikaAsm.GetName().Version})")}");
            
            if (fikaAsm != null)
            {
                var iface = fikaAsm.GetType("Fika.Core.Main.GameMode.IFikaGame");
                PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] IFikaGame interface: {(iface == null ? "NOT FOUND" : $"OK")}");
                
                if (iface != null)
                {
                    var singletonType = typeof(Singleton<>).MakeGenericType(iface);
                    var instanceProp = singletonType.GetProperty("Instance");
                    PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] Singleton<IFikaGame>.Instance property: {(instanceProp == null ? "NOT FOUND" : "OK")}");
                    
                    var fikaGame = instanceProp?.GetValue(null);
                    if (fikaGame != null)
                    {
                        PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] RETURN via Fika IFikaGame: {fikaGame.GetType().FullName}");
                        return fikaGame;
                    }
                }
            }

            PluginCore.Log.LogInfo($"[RaidAdminPanel.ResolveGameInstance] RETURN NULL (all methods failed)");
            return null;
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
