using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace RaidAdminPanel.Routers;

/// <summary>
/// Stub for PitFire Team raid-outcomes HTTP call so extract flow does not fail with 404.
/// </summary>
[Injectable]
public class PitFireCompatStaticRouter(JsonUtil jsonUtil, PitFireCompatCallbacks callbacks) : StaticRouter(jsonUtil, [
    new RouteAction<EmptyRequestData>(
        "/singleplayer/pitfireteam/teammate/raid-outcomes",
        async (url, info, sessionId, output) => await callbacks.RaidOutcomesStub(url, info, sessionId)
    ),
]);

[Injectable]
public class PitFireCompatCallbacks(HttpResponseUtil httpResponseUtil, ISptLogger<PitFireCompatCallbacks> logger)
{
    public ValueTask<string> RaidOutcomesStub(string url, EmptyRequestData info, MongoId sessionId)
    {
        logger.Debug($"[RaidAdminPanel] PitFire raid-outcomes stub for session={sessionId}");
        return new ValueTask<string>(httpResponseUtil.GetBody(new { err = 0, errmsg = (string?)null, data = new { } }));
    }
}
