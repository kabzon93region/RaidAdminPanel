using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;

namespace RaidAdminPanel.Services;

/// <summary>
/// Печатает URL админок после строк «Сервер запущен» (IOnLoad уже завершён к этому моменту).
/// </summary>
[Injectable]
public class RaidAdminPanelStartupBanner(
    ISptLogger<RaidAdminPanelStartupBanner> logger,
    ConfigServer configServer) : IOnUpdate
{
    private bool _logged;

    public Task<bool> OnUpdate(long secondsSinceLastRun)
    {
        if (_logged)
        {
            return Task.FromResult(false);
        }

        _logged = true;
        LogPanelUrls();
        return Task.FromResult(true);
    }

    private void LogPanelUrls()
    {
        var http = configServer.GetConfig<HttpConfig>();
        var baseUrl = BuildBrowserBaseUrl(http);

        logger.Success($"[RaidAdminPanel] Админка: {baseUrl}/RaidAdminPanel/index.html  |  коротко: {baseUrl}/admin");
        logger.Info("[RaidAdminPanel] (корень / — страница SPT Thank You, не админка)");

        if (IsAssemblyLoaded("TraderServicesPanel"))
        {
            logger.Success(
                $"[TraderServicesPanel] Панель торговцев: {baseUrl}/TraderServicesPanel/index.html  |  коротко: {baseUrl}/traderservices"
            );
        }
    }

    internal static string BuildBrowserBaseUrl(HttpConfig http)
    {
        var listenIp = (http.Ip ?? "127.0.0.1").Trim();
        var host = listenIp is "0.0.0.0" or "+" or "*"
            ? (string.IsNullOrWhiteSpace(http.BackendIp) ? "127.0.0.1" : http.BackendIp.Trim())
            : listenIp;

        return $"https://{host}:{http.Port}";
    }

    private static bool IsAssemblyLoaded(string assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assemblyName);
}
