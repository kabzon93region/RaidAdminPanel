using System;
using System.Collections;
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
                json = RequestHandler.GetJson(PollRoute);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "{}")
            {
                return;
            }

            JObject cmd;
            try
            {
                cmd = JObject.Parse(json);
            }
            catch
            {
                return;
            }

            var commandId = cmd.Value<string>("commandId");
            if (string.IsNullOrEmpty(commandId))
            {
                return;
            }

            var typeToken = cmd["type"];
            var type = ResolveCommandType(typeToken);
            if (string.IsNullOrEmpty(type))
            {
                return;
            }

            _log.LogInfo($"[RaidAdminPanel] command received: {type} ({commandId})");

            var success = false;
            string message;
            try
            {
                success = RaidAdminClientActions.Execute(type, out message);
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            var ack = $"{{\"commandId\":\"{commandId}\",\"success\":{success.ToString().ToLowerInvariant()},\"message\":\"{Escape(message)}\"}}";
            try
            {
                RequestHandler.PostJson(AckRoute, ack);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[RaidAdminPanel] ack failed: {ex.Message}");
            }
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
    }
}
