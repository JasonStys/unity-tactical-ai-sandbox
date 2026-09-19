// File: Documents.cs
// Purpose: Define versioned JSON transfer objects for saves, replays, and telemetry.
// Public API: SaveDocument, StateDocument, ReplayFile, ReplayStepDocument, and TelemetryDocument.
// Variables: Properties are mutable only to support System.Text.Json materialization.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Serialization;

/// <summary>Top-level versioned save envelope.</summary>
public sealed class SaveDocument
{
    public int FormatVersion { get; set; }

    public StateDocument State { get; set; } = new();
}

/// <summary>Serializable tactical state snapshot.</summary>
public sealed class StateDocument
{
    public int Width { get; set; }

    public int Height { get; set; }

    public int ActiveTeam { get; set; }

    public int TurnNumber { get; set; }

    public List<CellDocument> Cells { get; set; } = new();

    public List<UnitDocument> Units { get; set; } = new();
}

/// <summary>Serializable terrain cell.</summary>
public sealed class CellDocument
{
    public bool IsWalkable { get; set; }

    public int MoveCost { get; set; }

    public int Cover { get; set; }
}

/// <summary>Serializable unit state.</summary>
public sealed class UnitDocument
{
    public string Id { get; set; } = string.Empty;

    public int Team { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int HitPoints { get; set; }

    public int MaxHitPoints { get; set; }

    public SortedDictionary<string, int> Cooldowns { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Versioned replay envelope with an initial snapshot and expected hashes.</summary>
public sealed class ReplayFile
{
    public int FormatVersion { get; set; }

    public StateDocument InitialState { get; set; } = new();

    public List<ReplayStepDocument> Steps { get; set; } = new();
}

/// <summary>Serializable command and expected post-command hash.</summary>
public sealed class ReplayStepDocument
{
    public int Kind { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public int TargetX { get; set; }

    public int TargetY { get; set; }

    public string? TargetUnitId { get; set; }

    public string? AbilityId { get; set; }

    public string StateHash { get; set; } = string.Empty;
}

/// <summary>Stable JSON shape for a batch simulation report.</summary>
public sealed class TelemetryDocument
{
    public string SchemaVersion { get; set; } = "1.0";

    public ulong FirstSeed { get; set; }

    public int Matches { get; set; }

    public int BlueWins { get; set; }

    public int RedWins { get; set; }

    public int Draws { get; set; }

    public double AverageTurns { get; set; }

    public int TotalDamage { get; set; }

    public SortedDictionary<string, int> ActionCounts { get; set; } = new(StringComparer.Ordinal);

    public List<TelemetryMatchDocument> Results { get; set; } = new();
}

/// <summary>Compact deterministic record for one telemetry match.</summary>
public sealed class TelemetryMatchDocument
{
    public ulong Seed { get; set; }

    public string Winner { get; set; } = "Draw";

    public int Turns { get; set; }

    public int Commands { get; set; }

    public int Damage { get; set; }

    public string FinalHash { get; set; } = string.Empty;
}
