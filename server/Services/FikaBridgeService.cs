using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace RaidAdminPanel.Services;

/// <summary>
/// Опциональная интеграция с Fika через reflection (без жёсткой зависимости на FikaServer.dll).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class FikaBridgeService(ISptLogger<FikaBridgeService> logger)
{
    private readonly object _sync = new();
    private bool _initialized;
    private object? _matchService;
    private PropertyInfo? _matchesProperty;
    private MethodInfo? _getMatchMethod;
    private MethodInfo? _endMatchMethod;
    private MethodInfo? _getMatchIdByPlayerMethod;

    public bool IsAvailable => EnsureInitialized();

    public bool EnsureInitialized()
    {
        if (_initialized)
        {
            return _matchService != null;
        }

        lock (_sync)
        {
            if (_initialized)
            {
                return _matchService != null;
            }

            _initialized = true;
            var matchType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(TryGetTypes)
                .SelectMany(static t => t)
                .FirstOrDefault(t => t.FullName == "FikaServer.Services.MatchService");

            if (matchType == null)
            {
                logger.Info("[RaidAdminPanel] Fika MatchService not found — coop raid list/actions limited");
                return false;
            }

            _matchService = ProgramStatics.ServiceProvider?.GetService(matchType);
            if (_matchService == null)
            {
                logger.Warning("[RaidAdminPanel] Fika loaded but MatchService not in DI");
                return false;
            }

            _matchesProperty = matchType.GetProperty("Matches");
            _getMatchMethod = matchType.GetMethod("GetMatch", [typeof(MongoId?)]);
            _endMatchMethod = matchType.GetMethod("EndMatch");
            _getMatchIdByPlayerMethod = matchType.GetMethod("GetMatchIdByPlayer", [typeof(MongoId)]);
            logger.Info("[RaidAdminPanel] Fika MatchService bridge ready");
            return true;
        }
    }

    public IReadOnlyList<RaidAdminPanel.Models.RaidSummaryDto> GetActiveRaids()
    {
        if (!EnsureInitialized() || _matchesProperty?.GetValue(_matchService) is not System.Collections.IDictionary dict)
        {
            return [];
        }

        var result = new List<RaidAdminPanel.Models.RaidSummaryDto>();
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            var matchId = entry.Key?.ToString() ?? "?";
            var match = entry.Value;
            if (match == null)
            {
                continue;
            }

            var matchType = match.GetType();
            var location = ResolveRaidLocation(matchType, match);
            var host = matchType.GetProperty("HostUsername")?.GetValue(match)?.ToString();
            var status = matchType.GetProperty("Status")?.GetValue(match)?.ToString();
            var headless = matchType.GetProperty("IsHeadless")?.GetValue(match) as bool? ?? false;
            var playersProp = matchType.GetProperty("Players");
            var playerIds = new List<string>();
            var deadPlayerIds = new List<string>();
            if (playersProp?.GetValue(match) is System.Collections.IDictionary players)
            {
                foreach (System.Collections.DictionaryEntry playerEntry in players)
                {
                    var pid = playerEntry.Key?.ToString();
                    if (string.IsNullOrWhiteSpace(pid))
                    {
                        continue;
                    }

                    playerIds.Add(pid);
                    if (playerEntry.Value != null
                        && playerEntry.Value.GetType().GetProperty("IsDead")?.GetValue(playerEntry.Value) is true)
                    {
                        deadPlayerIds.Add(pid);
                    }
                }
            }

            result.Add(new RaidAdminPanel.Models.RaidSummaryDto
            {
                MatchId = matchId,
                Location = location,
                HostProfileId = matchId,
                HostUsername = host,
                PlayerCount = playerIds.Count,
                PlayerIds = playerIds,
                DeadPlayerIds = deadPlayerIds,
                Status = status,
                Headless = headless
            });
        }

        return result;
    }

    public string? GetMatchIdForPlayer(MongoId profileId)
    {
        if (!EnsureInitialized() || _getMatchIdByPlayerMethod == null)
        {
            return null;
        }

        var result = _getMatchIdByPlayerMethod.Invoke(_matchService, [profileId]);
        return result?.ToString();
    }

    public bool TryEndMatch(MongoId matchId, out string message)
    {
        if (!EnsureInitialized() || _endMatchMethod == null)
        {
            message = "Fika not available";
            return false;
        }

        try
        {
            var enumType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(TryGetTypes)
                .SelectMany(static t => t)
                .FirstOrDefault(t => t.FullName == "FikaServer.Models.Enums.EFikaMatchEndSessionMessage");

            var reason = enumType != null
                ? Enum.Parse(enumType, "HostShutdown")
                : 0;

            var task = _endMatchMethod.Invoke(_matchService, [matchId, reason]);
            if (task is ValueTask vt)
            {
                vt.AsTask().GetAwaiter().GetResult();
            }
            else if (task is Task t)
            {
                t.GetAwaiter().GetResult();
            }

            message = $"Fika match {matchId} ended (HostShutdown)";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static string? ResolveRaidLocation(Type matchType, object match)
    {
        var raidConfig = matchType.GetProperty("RaidConfig")?.GetValue(match);
        if (raidConfig != null)
        {
            var fromConfig = raidConfig.GetType().GetProperty("Location")?.GetValue(raidConfig)?.ToString();
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                return fromConfig;
            }
        }

        var locationData = matchType.GetProperty("LocationData")?.GetValue(match);
        if (locationData != null)
        {
            return locationData.GetType().GetProperty("Name")?.GetValue(locationData)?.ToString()
                ?? locationData.GetType().GetProperty("Id")?.GetValue(locationData)?.ToString();
        }

        return null;
    }

    private static IEnumerable<Type> TryGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch
        {
            return [];
        }
    }
}

/// <summary>
/// Заглушка для доступа к DI из reflection bridge (заполняется в OnLoad).
/// </summary>
public static class ProgramStatics
{
    public static IServiceProvider? ServiceProvider { get; set; }
}
