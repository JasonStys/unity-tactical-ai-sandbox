// File: JsonCodec.cs
// Purpose: Convert tactical models to validated, versioned JSON and CSV documents.
// Public API: JsonCodec save, load, replay, telemetry, and CSV methods.
// Variables: Serializer options are process-wide immutable configuration.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using System.Globalization;
using System.Text;
using System.Text.Json;
using TacticalAI.Core;

namespace TacticalAI.Serialization;

/// <summary>Provides explicit mappings between domain objects and external document formats.</summary>
public static class JsonCodec
{
    public const int CurrentSaveFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Serializes state into the current versioned save format.</summary>
    public static string SerializeSave(TacticalState state)
    {
        var save = new SaveDocument { FormatVersion = CurrentSaveFormatVersion, State = ToDocument(state) };
        return SerializeNormalized(save);
    }

    /// <summary>Loads current saves and migrates the legacy version-zero state-only format.</summary>
    public static TacticalState DeserializeSave(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Save JSON is required.", nameof(json));
        }

        using JsonDocument probe = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        int version = probe.RootElement.TryGetProperty("formatVersion", out JsonElement versionElement)
            ? versionElement.GetInt32()
            : 0;
        StateDocument document;
        if (version == 0)
        {
            document = JsonSerializer.Deserialize<StateDocument>(json, Options)
                ?? throw new InvalidDataException("The legacy save did not contain state.");
        }
        else if (version == CurrentSaveFormatVersion)
        {
            SaveDocument save = JsonSerializer.Deserialize<SaveDocument>(json, Options)
                ?? throw new InvalidDataException("The save document was empty.");
            document = save.State;
        }
        else
        {
            throw new InvalidDataException($"Unsupported save format {version}.");
        }

        return FromDocument(document);
    }

    /// <summary>Serializes a replay with its initial state and expected hashes.</summary>
    public static string SerializeReplay(ReplayDocument replay)
    {
        var file = new ReplayFile
        {
            FormatVersion = replay.FormatVersion,
            InitialState = ToDocument(replay.InitialState),
            Steps = replay.Steps.Select(step => new ReplayStepDocument
            {
                Kind = (int)step.Command.Kind,
                ActorId = step.Command.ActorId,
                TargetX = step.Command.TargetPosition.X,
                TargetY = step.Command.TargetPosition.Y,
                TargetUnitId = step.Command.TargetUnitId,
                AbilityId = step.Command.AbilityId,
                StateHash = step.StateHash,
            }).ToList(),
        };
        return SerializeNormalized(file);
    }

    /// <summary>Deserializes a replay while applying strict enum and shape validation.</summary>
    public static ReplayDocument DeserializeReplay(string json)
    {
        ReplayFile file = JsonSerializer.Deserialize<ReplayFile>(json, Options)
            ?? throw new InvalidDataException("The replay document was empty.");
        if (file.FormatVersion != ReplayService.CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unsupported replay format {file.FormatVersion}.");
        }

        var steps = new List<ReplayStep>(file.Steps.Count);
        foreach (ReplayStepDocument step in file.Steps)
        {
            if (!Enum.IsDefined((ActionKind)step.Kind) || step.StateHash.Length != 64)
            {
                throw new InvalidDataException("The replay contains an invalid command or state hash.");
            }

            steps.Add(new ReplayStep(
                new ActionCommand(
                    (ActionKind)step.Kind,
                    step.ActorId,
                    new GridPosition(step.TargetX, step.TargetY),
                    step.TargetUnitId,
                    step.AbilityId),
                step.StateHash));
        }

        return new ReplayDocument(file.FormatVersion, FromDocument(file.InitialState), steps);
    }

    /// <summary>Serializes a simulation summary using a stable report schema.</summary>
    public static string SerializeTelemetry(SimulationSummary summary, ulong firstSeed)
    {
        var document = new TelemetryDocument
        {
            FirstSeed = firstSeed,
            Matches = summary.Matches,
            BlueWins = summary.BlueWins,
            RedWins = summary.RedWins,
            Draws = summary.Draws,
            AverageTurns = Math.Round(summary.AverageTurns, 3),
            TotalDamage = summary.TotalDamage,
            ActionCounts = new SortedDictionary<string, int>(
                summary.ActionCounts.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                StringComparer.Ordinal),
            Results = summary.Results.Select(result => new TelemetryMatchDocument
            {
                Seed = result.Seed,
                Winner = result.Winner?.ToString() ?? "Draw",
                Turns = result.Turns,
                Commands = result.Commands,
                Damage = result.Damage,
                FinalHash = result.FinalHash,
            }).ToList(),
        };
        return SerializeNormalized(document);
    }

    /// <summary>Creates RFC 4180-compatible CSV telemetry without locale-dependent numbers.</summary>
    public static string SerializeTelemetryCsv(SimulationSummary summary)
    {
        var builder = new StringBuilder("seed,winner,turns,commands,damage,final_hash\n");
        foreach (MatchResult result in summary.Results)
        {
            builder.Append(result.Seed.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.Winner?.ToString() ?? "Draw").Append(',')
                .Append(result.Turns.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.Commands.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.Damage.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.FinalHash).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Maps domain state to a serialization-only snapshot.</summary>
    public static StateDocument ToDocument(TacticalState state)
    {
        var document = new StateDocument
        {
            Width = state.Map.Width,
            Height = state.Map.Height,
            ActiveTeam = (int)state.ActiveTeam,
            TurnNumber = state.TurnNumber,
        };
        for (int y = 0; y < state.Map.Height; y++)
        {
            for (int x = 0; x < state.Map.Width; x++)
            {
                TerrainCell cell = state.Map[new GridPosition(x, y)];
                document.Cells.Add(new CellDocument
                {
                    IsWalkable = cell.IsWalkable,
                    MoveCost = cell.MoveCost,
                    Cover = cell.Cover,
                });
            }
        }

        foreach (UnitState unit in state.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            document.Units.Add(new UnitDocument
            {
                Id = unit.Id,
                Team = (int)unit.Team,
                X = unit.Position.X,
                Y = unit.Position.Y,
                HitPoints = unit.HitPoints,
                MaxHitPoints = unit.MaxHitPoints,
                Cooldowns = new SortedDictionary<string, int>(
                    unit.Cooldowns.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            });
        }

        return document;
    }

    /// <summary>Validates and maps a document snapshot back to the domain.</summary>
    public static TacticalState FromDocument(StateDocument document)
    {
        if (document.Width is < 2 or > 128 || document.Height is < 2 or > 128 || document.Cells.Count != document.Width * document.Height)
        {
            throw new InvalidDataException("The save has invalid map dimensions or cell count.");
        }

        if (!Enum.IsDefined((Team)document.ActiveTeam) || document.TurnNumber < 1)
        {
            throw new InvalidDataException("The save has invalid turn state.");
        }

        var map = new GridMap(document.Width, document.Height, document.Cells.Select(cell =>
            new TerrainCell(cell.IsWalkable, cell.MoveCost, cell.Cover)));
        var units = new List<UnitState>(document.Units.Count);
        foreach (UnitDocument item in document.Units)
        {
            if (!Enum.IsDefined((Team)item.Team))
            {
                throw new InvalidDataException("The save contains an invalid team value.");
            }

            var unit = new UnitState(item.Id, (Team)item.Team, new GridPosition(item.X, item.Y), item.HitPoints, item.MaxHitPoints);
            foreach (KeyValuePair<string, int> cooldown in item.Cooldowns)
            {
                if (cooldown.Value < 0)
                {
                    throw new InvalidDataException("Cooldown values cannot be negative.");
                }

                unit.SetCooldown(cooldown.Key, cooldown.Value);
            }

            units.Add(unit);
        }

        return new TacticalState(map, units, (Team)document.ActiveTeam, document.TurnNumber);
    }

    private static string SerializeNormalized<T>(T value) =>
        JsonSerializer.Serialize(value, Options).ReplaceLineEndings("\n") + "\n";
}
