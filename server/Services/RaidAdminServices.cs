using System.Collections.Concurrent;
using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using RaidAdminPanel.Models;

namespace RaidAdminPanel.Services;

[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class RaidAdminConfigService(ISptLogger<RaidAdminConfigService> logger, ModHelper modHelper) : IOnLoad
{
    public RaidAdminConfig Config { get; private set; } = new();

    public Task OnLoad()
    {
        try
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            Config = modHelper.GetJsonDataFromFile<RaidAdminConfig>(modPath, "config.json");
            logger.Info("[RaidAdminPanel] config loaded");
        }
        catch (Exception ex)
        {
            logger.Warning($"[RaidAdminPanel] config load failed, using defaults: {ex.Message}");
            Config = new RaidAdminConfig();
        }

        return Task.CompletedTask;
    }

    public bool ValidateApiKey(string? apiKey)
    {
        if (!Config.RequireApiKey)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(apiKey)
            && string.Equals(apiKey.Trim(), Config.AdminApiKey, StringComparison.Ordinal);
    }
}

[Injectable(InjectionType.Singleton)]
public class RaidAdminCommandQueue
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<AdminClientCommand>> _queues = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    public void Enqueue(AdminClientCommand command)
    {
        var q = _queues.GetOrAdd(command.ProfileId, static _ => new ConcurrentQueue<AdminClientCommand>());
        q.Enqueue(command);
        _pending[command.ProfileId] = 0;
    }

    public AdminClientCommand? Dequeue(string profileId)
    {
        if (!_queues.TryGetValue(profileId, out var q))
        {
            return null;
        }

        return q.TryDequeue(out var cmd) ? cmd : null;
    }

    public bool HasPending(string profileId) =>
        _queues.TryGetValue(profileId, out var q) && !q.IsEmpty;

    public int Clear(string profileId)
    {
        if (!_queues.TryRemove(profileId, out var q))
        {
            return 0;
        }

        var count = 0;
        while (q.TryDequeue(out _))
        {
            count++;
        }

        _pending.TryRemove(profileId, out _);
        return count;
    }
}

[Injectable(InjectionType.Singleton)]
public class RaidAdminActionLog(RaidAdminConfigService configService)
{
    private readonly ConcurrentQueue<AdminActionLogEntry> _entries = new();
    private readonly object _sync = new();

    public void Info(string message, string? details = null) => Add("info", message, details);
    public void Warning(string message, string? details = null) => Add("warn", message, details);
    public void Error(string message, string? details = null) => Add("error", message, details);

    public IReadOnlyList<AdminActionLogEntry> GetRecent(int? max = null)
    {
        var limit = max ?? configService.Config.MaxActionLogEntries;
        return _entries.Reverse().Take(limit).Reverse().ToList();
    }

    private void Add(string level, string message, string? details)
    {
        lock (_sync)
        {
            _entries.Enqueue(new AdminActionLogEntry
            {
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Level = level,
                Message = message,
                Details = details
            });

            var max = Math.Max(20, configService.Config.MaxActionLogEntries);
            while (_entries.Count > max && _entries.TryDequeue(out _))
            {
            }
        }
    }
}

[Injectable(InjectionType.Singleton)]
public class RaidAdminService(
    SaveServer saveServer,
    ProfileHelper profileHelper,
    ProfileActivityService profileActivityService,
    ProfileDataService profileDataService,
    RaidAdminCommandQueue commandQueue,
    RaidAdminActionLog actionLog,
    FikaBridgeService fikaBridge)
{
    public DashboardStatusDto GetDashboardStatus()
    {
        var profiles = saveServer.GetProfiles();
        var activeIds = profileActivityService.GetActiveProfileIdsWithinMinutes(15);
        return new DashboardStatusDto
        {
            ServerTimeUtc = DateTime.UtcNow.ToString("O"),
            ProfileCount = profiles?.Count ?? 0,
            ActiveProfiles = activeIds.Count,
            ActiveRaids = fikaBridge.GetActiveRaids().Count,
            FikaDetected = fikaBridge.IsAvailable,
            RecentLogs = actionLog.GetRecent(30).ToList()
        };
    }

    public IReadOnlyList<PlayerSummaryDto> GetPlayers(int activeMinutes = 60)
    {
        var activeSet = new HashSet<string>(
            profileActivityService.GetActiveProfileIdsWithinMinutes(activeMinutes),
            StringComparer.Ordinal);

        var profiles = saveServer.GetProfiles();
        if (profiles == null || profiles.Count == 0)
        {
            return [];
        }

        var list = new List<PlayerSummaryDto>();
        foreach (var (sessionId, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            var nickname = pmc?.Info?.Nickname ?? profile.CharacterData?.ScavData?.Info?.Nickname ?? sessionId.ToString();
            var raidData = profileActivityService.ContainsActiveProfile(sessionId)
                ? profileActivityService.GetProfileActivityRaidData(sessionId)
                : null;

            list.Add(new PlayerSummaryDto
            {
                ProfileId = sessionId.ToString(),
                Nickname = nickname,
                Level = pmc?.Info?.Level ?? 0,
                ActiveRecently = activeSet.Contains(sessionId.ToString()),
                InRaid = raidData?.RaidConfiguration != null,
                Location = raidData?.RaidConfiguration?.Location,
                Side = raidData?.RaidConfiguration?.Side?.ToString(),
                HasPendingCommand = commandQueue.HasPending(sessionId.ToString())
            });
        }

        return list.OrderByDescending(p => p.ActiveRecently).ThenBy(p => p.Nickname).ToList();
    }

    public async Task<int> SaveAllProfilesAsync()
    {
        var profiles = saveServer.GetProfiles();
        if (profiles == null)
        {
            return 0;
        }

        var count = 0;
        foreach (var sessionId in profiles.Keys)
        {
            await saveServer.SaveProfileAsync(sessionId);
            count++;
        }

        actionLog.Info($"Сохранены все профили на диск ({count})");
        return count;
    }

    public async Task SaveProfileAsync(MongoId profileId)
    {
        if (!saveServer.GetProfiles().ContainsKey(profileId))
        {
            throw new KeyNotFoundException($"Profile not found: {profileId}");
        }

        await saveServer.SaveProfileAsync(profileId);
        var nick = profileHelper.GetPmcProfile(profileId)?.Info?.Nickname ?? profileId.ToString();
        actionLog.Info($"Профиль сохранён: {nick}", profileId.ToString());
    }

    public AdminClientCommand QueueForceExtract(MongoId profileId, string? matchId, string? reason)
    {
        var cmd = new AdminClientCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            Type = AdminCommandType.ForceExtractSurvived,
            ProfileId = profileId.ToString(),
            MatchId = matchId,
            Reason = reason ?? "Admin force extract (survived)",
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        commandQueue.Enqueue(cmd);
        actionLog.Info($"Команда extract → клиент {profileId}", cmd.CommandId);
        return cmd;
    }

    public AdminClientCommand QueueInventorySnapshot(MongoId profileId, string? reason)
    {
        var cmd = new AdminClientCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            Type = AdminCommandType.RequestInventorySnapshot,
            ProfileId = profileId.ToString(),
            Reason = reason ?? "Admin inventory snapshot",
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        commandQueue.Enqueue(cmd);
        actionLog.Info($"Запрос снимка инвентаря → клиент {profileId}", cmd.CommandId);
        return cmd;
    }

    public IReadOnlyList<AdminClientCommand> QueueForceExtractForMatch(string matchId, bool includeDead, string? reason)
    {
        var raid = fikaBridge.GetActiveRaids().FirstOrDefault(r => r.MatchId == matchId);
        if (raid == null)
        {
            throw new KeyNotFoundException($"Fika match not found: {matchId}");
        }

        var commands = new List<AdminClientCommand>();
        foreach (var pid in raid.PlayerIds)
        {
            if (!MongoId.IsValidMongoId(pid))
            {
                continue;
            }

            if (!includeDead && raid.DeadPlayerIds.Contains(pid))
            {
                continue;
            }

            var profileId = new MongoId(pid);
            commands.Add(QueueForceExtract(profileId, matchId, reason));
        }

        actionLog.Info($"Массовый extract для рейда {matchId}", $"players={commands.Count}");
        return commands;
    }

    public void AcknowledgeCommand(string profileId, ClientCommandAckRequest ack)
    {
        if (ack.Success)
        {
            actionLog.Info($"Клиент {profileId} выполнил {ack.CommandId}", ack.Message);
        }
        else
        {
            actionLog.Warning($"Клиент {profileId} не выполнил {ack.CommandId}", ack.Message);
        }
    }

    public async Task SaveInventorySnapshotAsync(MongoId profileId, InventorySnapshotRequest snapshot)
    {
        var data = new
        {
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            snapshot.RaidLocation,
            snapshot.Note,
            snapshot.ItemCount
        };
        profileDataService.SaveProfileData(profileId.ToString(), "last_inventory_snapshot", data);
        await saveServer.SaveProfileAsync(profileId);
        actionLog.Info($"Снимок инвентаря записан для {profileId}", $"items={snapshot.ItemCount}");
    }

    public bool TryEndFikaMatch(MongoId matchId, out string message)
    {
        var ok = fikaBridge.TryEndMatch(matchId, out message);
        if (ok)
        {
            actionLog.Info(message);
        }
        else
        {
            actionLog.Warning("EndMatch failed", message);
        }

        return ok;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
public class RaidAdminPanelOnLoad(
    ISptLogger<RaidAdminPanelOnLoad> logger,
    IServiceProvider serviceProvider) : IOnLoad
{
    public Task OnLoad()
    {
        ProgramStatics.ServiceProvider = serviceProvider;
        logger.Info("[RaidAdminPanel] loaded — open /RaidAdminPanel/index.html on SPT HTTPS port");
        return Task.CompletedTask;
    }
}
