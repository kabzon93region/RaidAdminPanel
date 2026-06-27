using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using UnityEngine;

namespace RaidAdminPanel
{
    internal sealed class AdminCommandPoller
    {
        private const string PollRoute = "/raidadminpanel/client/commands/poll";
        private const string AckRoute = "/raidadminpanel/client/commands/ack";
        private readonly ManualLogSource _log;
        private Coroutine _routine;
        private MonoBehaviour _host;
        private float _interval = 3f;

        public AdminCommandPoller(ManualLogSource log)
        {
            _log = log;
        }

        public void Start(MonoBehaviour host)
        {
            _host = host;
            _routine = host.StartCoroutine(PollLoop());
        }

        public void Stop()
        {
            if (_host != null && _routine != null)
            {
                _host.StopCoroutine(_routine);
            }
        }

        private IEnumerator PollLoop()
        {
            var wait = new WaitForSeconds(_interval);
            while (true)
            {
                yield return wait;
                if (!IsInRaid())
                {
                    continue;
                }

                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    _log.LogDebug($"[RaidAdminPanel] poll error: {ex.Message}");
                }
            }
        }

        private void PollOnce()
        {
            string json;
            try
            {
                // Передаём ProfileId явно — в Fika coop session ID может отличаться от profileId
                var profileId = GetClientProfileId();
                var pollUrl = string.IsNullOrEmpty(profileId)
                    ? PollRoute
                    : $"{PollRoute}?profileId={profileId}";
                
                _log.LogInfo($"[RaidAdminPanel] ===== POLL START =====");
                _log.LogInfo($"[RaidAdminPanel] profileId from GetClientProfileId: {profileId ?? "NULL"}");
                _log.LogInfo($"[RaidAdminPanel] pollUrl: {pollUrl}");
                
                json = RequestHandler.GetJson(pollUrl);
                
                _log.LogInfo($"[RaidAdminPanel] Raw response (length: {json?.Length ?? 0}): {(json == null ? "NULL" : json)}");
                
                // Логируем что получили от сервера
                if (!string.IsNullOrWhiteSpace(json) && json != "null" && json != "{}")
                {
                    _log.LogInfo($"[RaidAdminPanel] POLL RESPONSE (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
                }
                else
                {
                    _log.LogInfo($"[RaidAdminPanel] POLL RESPONSE is empty/null/no-command");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[RaidAdminPanel] poll request FAILED: {ex.GetType().Name}: {ex.Message}");
                _log.LogWarning($"[RaidAdminPanel] Stack trace: {ex.StackTrace}");
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "{}")
            {
                _log.LogInfo($"[RaidAdminPanel] ===== POLL END (no data) =====");
                return;
            }

            JObject cmd;
            try
            {
                cmd = JObject.Parse(json);
                _log.LogInfo($"[RaidAdminPanel] JSON parsed successfully");
                _log.LogInfo($"[RaidAdminPanel] Top-level keys: {string.Join(", ", cmd.Properties().Select(p => p.Name))}");
                _log.LogInfo($"[RaidAdminPanel] Has 'err' field: {cmd["err"] != null} (value: {cmd["err"]})");
                _log.LogInfo($"[RaidAdminPanel] Has 'errmsg' field: {cmd["errmsg"] != null} (value: {cmd["errmsg"]})");
                _log.LogInfo($"[RaidAdminPanel] Has 'data' field: {cmd["data"] != null} (type: {cmd["data"]?.Type})");
                
                // SPT оборачивает ответ в {err, errmsg, data} - нужно достать data
                if (cmd["data"] != null && cmd["data"] is JObject dataObj)
                {
                    cmd = dataObj;
                    _log.LogInfo($"[RaidAdminPanel] Extracted 'data' object");
                    _log.LogInfo($"[RaidAdminPanel] Data keys: {string.Join(", ", cmd.Properties().Select(p => p.Name))}");
                    _log.LogInfo($"[RaidAdminPanel] Data full content: {cmd.ToString()}");
                }
                else
                {
                    _log.LogInfo($"[RaidAdminPanel] No 'data' field, using top-level JSON as command");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[RaidAdminPanel] JSON parse FAILED: {ex.GetType().Name}: {ex.Message}");
                _log.LogWarning($"[RaidAdminPanel] Failed JSON (first 200 chars): {json.Substring(0, Math.Min(200, json.Length))}");
                return;
            }

            var commandId = cmd.Value<string>("commandId") ?? cmd.Value<string>("CommandId");
            _log.LogInfo($"[RaidAdminPanel] commandId (string): '{commandId ?? "NULL"}'");
            
            if (string.IsNullOrEmpty(commandId))
            {
                _log.LogWarning($"[RaidAdminPanel] commandId is null/empty - skipping command");
                _log.LogInfo($"[RaidAdminPanel] ===== POLL END (no commandId) =====");
                return;
            }

            var typeToken = cmd["type"] ?? cmd["Type"];
            _log.LogInfo($"[RaidAdminPanel] type token: {typeToken}");
            _log.LogInfo($"[RaidAdminPanel] type token type: {typeToken?.Type} (Integer={JTokenType.Integer}, String={JTokenType.String})");
            
            var type = ResolveCommandType(typeToken);
            _log.LogInfo($"[RaidAdminPanel] ResolveCommandType result: '{type ?? "NULL"}'");
            
            if (string.IsNullOrEmpty(type))
            {
                _log.LogWarning($"[RaidAdminPanel] Unknown command type - skipping. Token: {typeToken}, Type: {typeToken?.Type}");
                _log.LogInfo($"[RaidAdminPanel] ===== POLL END (unknown type) =====");
                return;
            }

            _log.LogInfo($"[RaidAdminPanel] Command resolved: type='{type}', commandId='{commandId}'");
            _log.LogInfo($"[RaidAdminPanel] Checking if in raid before execution...");
            
            var inRaid = IsInRaid();
            _log.LogInfo($"[RaidAdminPanel] IsInRaid: {inRaid}");

            var success = false;
            string message;
            try
            {
                _log.LogInfo($"[RaidAdminPanel] Calling RaidAdminClientActions.Execute('{type}', out message)...");
                success = RaidAdminClientActions.Execute(type, out message);
                _log.LogInfo($"[RaidAdminPanel] Execute returned: success={success}, message='{message}'");
            }
            catch (Exception ex)
            {
                message = ex.Message;
                _log.LogError($"[RaidAdminPanel] Execute THREW EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                _log.LogError($"[RaidAdminPanel] Stack trace: {ex.StackTrace}");
            }

            _log.LogInfo($"[RaidAdminPanel] Command execution result: type='{type}', success={success}, message='{message}'");

            var ack = $"{{\"CommandId\":\"{commandId}\",\"Success\":{success.ToString().ToLowerInvariant()},\"Message\":\"{Escape(message)}\"}}";
            _log.LogInfo($"[RaidAdminPanel] ACK payload: {ack}");
            
            try
            {
                _log.LogInfo($"[RaidAdminPanel] Sending ACK to {AckRoute}...");
                RequestHandler.PostJson(AckRoute, ack);
                _log.LogInfo($"[RaidAdminPanel] ACK sent successfully");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[RaidAdminPanel] ACK FAILED: {ex.GetType().Name}: {ex.Message}");
            }
            
            _log.LogInfo($"[RaidAdminPanel] ===== POLL END (processed) =====");
        }

        private static string ResolveCommandType(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>() switch
                {
                    0 => "ForceExtractSurvived",
                    1 => "RequestInventorySnapshot",
                    2 => "PingClient",
                    3 => "ForceExtractAll",
                    _ => null
                };
            }

            return token.ToString();
        }

        private static string Escape(string s) =>
            (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static bool IsInRaid()
        {
            var world = Singleton<GameWorld>.Instance;
            return world != null && world.MainPlayer != null;
        }

        private static string GetClientProfileId()
        {
            try
            {
                var world = Singleton<GameWorld>.Instance;
                var player = world?.MainPlayer;
                var profileId = player?.Profile?.Id;
                return profileId ?? null;
            }
            catch
            {
                return null;
            }
        }
    }
}
