namespace RaidAdminPanel.Models;

public sealed class RaidAdminConfig
{
    public string AdminApiKey { get; set; } = "change-me-raid-admin";
    public bool RequireApiKey { get; set; } = false;
    public int ClientPollIntervalSeconds { get; set; } = 3;
    public int MaxActionLogEntries { get; set; } = 200;
    public string? Notes { get; set; }
}

public enum AdminCommandType
{
    ForceExtractSurvived,
    ForceExtractAll,
    RequestInventorySnapshot,
    PingClient
}

public sealed class AdminClientCommand
{
    public required string CommandId { get; init; }
    public required AdminCommandType Type { get; init; }
    public required string ProfileId { get; init; }
    public string? MatchId { get; init; }
    public string? Reason { get; init; }
    public long CreatedAtUnix { get; init; }
}

public sealed class AdminActionLogEntry
{
    public required long TimestampUnix { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
}

public sealed class PlayerSummaryDto
{
    public required string ProfileId { get; init; }
    public string? Nickname { get; init; }
    public int Level { get; init; }
    public bool ActiveRecently { get; init; }
    public bool InRaid { get; init; }
    public string? Location { get; init; }
    public string? Side { get; init; }
    public bool HasPendingCommand { get; init; }
}

public sealed class RaidSummaryDto
{
    public required string MatchId { get; init; }
    public string? Location { get; init; }
    public string? HostProfileId { get; init; }
    public string? HostUsername { get; init; }
    public int PlayerCount { get; init; }
    public List<string> PlayerIds { get; init; } = [];
    public List<string> DeadPlayerIds { get; init; } = [];
    public string? Status { get; init; }
    public bool Headless { get; init; }
}

public sealed class DashboardStatusDto
{
    public required string ServerTimeUtc { get; init; }
    public int ProfileCount { get; init; }
    public int ActiveProfiles { get; init; }
    public int ActiveRaids { get; init; }
    public bool FikaDetected { get; init; }
    public string PanelUrlHint { get; init; } = "/RaidAdminPanel/index.html";
    public List<AdminActionLogEntry> RecentLogs { get; init; } = [];
}

public sealed class ForceExtractRequest
{
    public string? MatchId { get; set; }
    public string? ProfileId { get; set; }
    public bool IncludeDead { get; set; }
    public string? Reason { get; set; }
}

public sealed class ClientCommandAckRequest
{
    public required string CommandId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public sealed class InventorySnapshotRequest
{
    public string? Note { get; set; }
    public string? RaidLocation { get; set; }
    public int ItemCount { get; set; }
}
