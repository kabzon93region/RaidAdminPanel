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
    new RouteAction<InventorySnapshotRequest>(
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
    HttpResponseUtil httpResponseUtil)
{
    public ValueTask<string> PollCommand(string url, EmptyRequestData info, MongoId sessionId)
    {
        var cmd = commandQueue.Dequeue(sessionId.ToString());
        if (cmd == null)
        {
            return new ValueTask<string>(httpResponseUtil.NullResponse());
        }

        return new ValueTask<string>(httpResponseUtil.GetBody(cmd));
    }

    public ValueTask<string> AckCommand(string url, ClientCommandAckRequest info, MongoId sessionId)
    {
        adminService.AcknowledgeCommand(sessionId.ToString(), info);
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    public async ValueTask<string> SaveInventorySnapshot(string url, InventorySnapshotRequest info, MongoId sessionId)
    {
        await adminService.SaveInventorySnapshotAsync(sessionId, info);
        return httpResponseUtil.NullResponse();
    }
}
