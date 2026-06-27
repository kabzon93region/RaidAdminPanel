using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using RaidAdminPanel.Models;
using RaidAdminPanel.Services;

namespace RaidAdminPanel.Routers;

[Injectable]
public class RaidAdminStaticRouter(JsonUtil jsonUtil, RaidAdminRouteCallbacks callbacks) : StaticRouter(jsonUtil, [
    new RouteAction<ClientCommandAckRequest>(
        "/raidadminpanel/client/commands/ack",
        async (url, info, sessionId, output) => await callbacks.AckCommand(url, info, sessionId)
    ),
    new RouteAction<EmptyRequestData>(
        "/raidadminpanel/client/inventory-snapshot",
        async (url, info, sessionId, output) => await callbacks.SaveInventorySnapshot(url, info, sessionId)
    ),
    new RouteAction<EmptyRequestData>(
        "/raidadminpanel/client/commands/poll",
        async (url, info, sessionId, output) => await callbacks.PollCommand(url, info, sessionId)
    ),
]);

[Injectable]
public class RaidAdminRouteCallbacks(
    RaidAdminService adminService,
    RaidAdminCommandQueue commandQueue,
    HttpResponseUtil httpResponseUtil,
    ISptLogger<RaidAdminRouteCallbacks> logger)
{
    public ValueTask<string> PollCommand(string url, EmptyRequestData info, MongoId sessionId)
    {
        // В Fika coop sessionId != profileId, читаем profileId из query string
        var profileId = ExtractProfileIdFromQuery(url, sessionId.ToString());
        logger.Debug($"[RaidAdminPanel] PollCommand: sessionId={sessionId}, profileId={profileId}, url={url}");
        var cmd = commandQueue.Dequeue(profileId);
        if (cmd == null)
        {
            logger.Debug($"[RaidAdminPanel] PollCommand: no command for {profileId}");
            return new ValueTask<string>(httpResponseUtil.NullResponse());
        }

        logger.Info($"[RaidAdminPanel] PollCommand: returning command {cmd.Type} for {profileId}");
        return new ValueTask<string>(httpResponseUtil.GetBody(cmd));
    }

    private static string ExtractProfileIdFromQuery(string url, string fallbackSessionId)
    {
        if (string.IsNullOrEmpty(url))
        {
            return fallbackSessionId;
        }

        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
        {
            return fallbackSessionId;
        }

        var query = url.Substring(queryIndex + 1);
        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2 && parts[0] == "profileId" && !string.IsNullOrEmpty(parts[1]))
            {
                return parts[1];
            }
        }

        return fallbackSessionId;
    }

    public ValueTask<string> AckCommand(string url, ClientCommandAckRequest info, MongoId sessionId)
    {
        adminService.AcknowledgeCommand(sessionId.ToString(), info);
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    public async ValueTask<string> SaveInventorySnapshot(string url, EmptyRequestData info, MongoId sessionId)
    {
        // Inventory snapshot is deprecated - just return null response
        return httpResponseUtil.NullResponse();
    }
}
