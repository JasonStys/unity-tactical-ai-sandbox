// File: Replay.cs
// Purpose: Capture commands and validate deterministic state hashes without serialization dependencies.
// Public API: ReplayStep, ReplayDocument, ReplayValidation, StateHasher, and ReplayService.
// Variables: Replay entries are append-only copies; hashes use canonical ordering and SHA-256.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using System.Security.Cryptography;
using System.Text;

namespace TacticalAI.Core;

/// <summary>Pairs a resolved command with the expected post-command state hash.</summary>
public sealed class ReplayStep
{
    public ReplayStep(ActionCommand command, string stateHash)
    {
        Command = command;
        StateHash = stateHash;
    }

    public ActionCommand Command { get; }

    public string StateHash { get; }
}

/// <summary>Stores the initial match and its immutable sequence of resolved steps.</summary>
public sealed class ReplayDocument
{
    public ReplayDocument(int formatVersion, TacticalState initialState, IEnumerable<ReplayStep> steps)
    {
        FormatVersion = formatVersion;
        InitialState = initialState.Clone();
        Steps = steps.ToArray();
    }

    public int FormatVersion { get; }

    public TacticalState InitialState { get; }

    public IReadOnlyList<ReplayStep> Steps { get; }
}

/// <summary>Reports the first replay mismatch or successful final hash.</summary>
public sealed class ReplayValidation
{
    public ReplayValidation(bool succeeded, int stepsValidated, string finalHash, string error)
    {
        Succeeded = succeeded;
        StepsValidated = stepsValidated;
        FinalHash = finalHash;
        Error = error;
    }

    public bool Succeeded { get; }

    public int StepsValidated { get; }

    public string FinalHash { get; }

    public string Error { get; }
}

/// <summary>Produces stable state fingerprints for replay and regression checks.</summary>
public static class StateHasher
{
    /// <summary>Hashes a canonical, culture-independent representation of state.</summary>
    public static string Compute(TacticalState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var builder = new StringBuilder();
        builder.Append(state.Map.Width).Append('x').Append(state.Map.Height)
            .Append('|').Append((int)state.ActiveTeam).Append('|').Append(state.TurnNumber);
        for (int y = 0; y < state.Map.Height; y++)
        {
            for (int x = 0; x < state.Map.Width; x++)
            {
                TerrainCell cell = state.Map[new GridPosition(x, y)];
                builder.Append('|').Append(cell.IsWalkable ? '1' : '0').Append(',').Append(cell.MoveCost).Append(',').Append(cell.Cover);
            }
        }

        foreach (UnitState unit in state.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            builder.Append('|').Append(unit.Id).Append(',').Append((int)unit.Team).Append(',')
                .Append(unit.Position.X).Append(',').Append(unit.Position.Y).Append(',')
                .Append(unit.HitPoints).Append(',').Append(unit.MaxHitPoints);
            foreach (KeyValuePair<string, int> cooldown in unit.Cooldowns)
            {
                builder.Append(',').Append(cooldown.Key).Append('=').Append(cooldown.Value);
            }
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
        var hex = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
        {
            hex.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
}

/// <summary>Records and replays resolved command sequences.</summary>
public static class ReplayService
{
    public const int CurrentFormatVersion = 1;

    /// <summary>Creates a replay by applying commands to a private state clone.</summary>
    public static ReplayDocument Record(TacticalState initialState, TacticalEngine engine, IEnumerable<ActionCommand> commands)
    {
        TacticalState state = initialState.Clone();
        var steps = new List<ReplayStep>();
        foreach (ActionCommand command in commands)
        {
            ActionResult result = engine.Apply(state, command);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Cannot record rejected command: {result.Error}");
            }

            steps.Add(new ReplayStep(command, StateHasher.Compute(state)));
        }

        return new ReplayDocument(CurrentFormatVersion, initialState, steps);
    }

    /// <summary>Replays every command and compares each expected state hash.</summary>
    public static ReplayValidation Validate(ReplayDocument replay, TacticalEngine engine)
    {
        if (replay.FormatVersion != CurrentFormatVersion)
        {
            return new ReplayValidation(false, 0, string.Empty, $"Unsupported replay format {replay.FormatVersion}.");
        }

        TacticalState state = replay.InitialState.Clone();
        for (int index = 0; index < replay.Steps.Count; index++)
        {
            ReplayStep step = replay.Steps[index];
            ActionResult result = engine.Apply(state, step.Command);
            if (!result.Succeeded)
            {
                return new ReplayValidation(false, index, StateHasher.Compute(state), $"Step {index} was rejected: {result.Error}");
            }

            string actualHash = StateHasher.Compute(state);
            if (!string.Equals(actualHash, step.StateHash, StringComparison.Ordinal))
            {
                return new ReplayValidation(false, index, actualHash, $"Step {index} hash mismatch.");
            }
        }

        return new ReplayValidation(true, replay.Steps.Count, StateHasher.Compute(state), string.Empty);
    }
}
