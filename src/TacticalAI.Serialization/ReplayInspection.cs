// File: ReplayInspection.cs
// Purpose: Build bounded, hash-validated fictional game replay review data and a self-contained viewer.
// Author: Jason Stys. Public API: ReplayInspectionService and immutable inspection records.
// Variables: cloned replay/state prevent inspection mutating a recording; default-bot diagnostics are recomputed.
// Declaration index: docs/CODE_INDEX.md contains generated exact line locations.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TacticalAI.Core;

namespace TacticalAI.Serialization;

/// <summary>One recorded or independently suggested fictional game command.</summary>
public sealed record InspectionCommand(string Kind, string ActorId, int X, int Y, string? TargetUnitId, string? AbilityId);

/// <summary>Defensive copy of a game avatar's position, health and cooldown evidence.</summary>
public sealed record InspectionUnit(string Id, string Team, int X, int Y, int HitPoints, int MaxHitPoints, IReadOnlyDictionary<string, int> Cooldowns);

/// <summary>Terrain-only grid visibility; not ability range or general action eligibility.</summary>
public sealed record InspectionSight(string First, string Second, bool Clear);

/// <summary>Default-bot diagnostics are not an explanation of the policy that recorded the replay.</summary>
public sealed record InspectionDecision(InspectionCommand Command, int Score, int CandidatesEvaluated, bool BudgetExhausted, string Explanation);

/// <summary>One validated post-command state, or the initial frame at index zero.</summary>
public sealed record InspectionFrame(int Index, int Turn, string ActiveTeam, string? Winner, string StateHash,
    IReadOnlyList<InspectionUnit> Units, IReadOnlyList<InspectionSight> Sight,
    InspectionCommand? PreviousCommand, InspectionCommand? NextRecordedCommand, InspectionDecision? DefaultBot);

/// <summary>Portable bounded review document; original simulation, AI weights and hashes are unchanged.</summary>
public sealed record ReplayInspection(int SchemaVersion, int Width, int Height, IReadOnlyList<TerrainCell> Cells,
    string InitialHash, string FinalHash, int EvaluationBudget, IReadOnlyList<InspectionFrame> Frames, string Notice);

/// <summary>Read-only game designer review with no Unity Editor or external browser dependency at runtime.</summary>
public static class ReplayInspectionService
{
    public const int MaximumSteps = 128;
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Validate all recorded hashes before returning any review; reject unsupported report sizes.</summary>
    public static ReplayInspection Create(ReplayDocument replay, TacticalEngine engine, int evaluationBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(engine);
        if (evaluationBudget is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        }

        var snapshot = new ReplayDocument(replay.FormatVersion, replay.InitialState, replay.Steps);
        TacticalState state = snapshot.InitialState.Clone();
        if (snapshot.Steps.Count > MaximumSteps || state.Map.Width > 32 || state.Map.Height > 32 || state.Units.Count > 16 ||
            state.Units.Any(unit => unit.Id.Length > 64 || unit.Cooldowns.Count > 32 || unit.Cooldowns.Keys.Any(key => key.Length > 64)) ||
            state.Map.CopyCells().Any(cell => cell.MoveCost > 100))
        {
            throw new InvalidDataException("Inspection supports at most 128 steps, 16 avatars, 32x32 cells, move costs<=100 and bounded identifiers/cooldowns.");
        }

        ReplayValidation validation = ReplayService.Validate(snapshot, engine);
        if (!validation.Succeeded)
        {
            throw new InvalidDataException($"Replay review rejected: {validation.Error}");
        }

        var frames = new List<InspectionFrame>(snapshot.Steps.Count + 1);
        var agent = new UtilityAgent();
        for (int index = 0; index <= snapshot.Steps.Count; index++)
        {
            if (index > 0)
            {
                ActionResult result = engine.Apply(state, snapshot.Steps[index - 1].Command);
                if (!result.Succeeded)
                {
                    throw new InvalidDataException("Validated replay changed during inspection.");
                }
            }

            UnitState[] units = state.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal).ToArray();
            var sight = new List<InspectionSight>();
            for (int first = 0; first < units.Length; first++)
            {
                for (int second = first + 1; second < units.Length; second++)
                {
                    if (units[first].IsAlive && units[second].IsAlive)
                    {
                        sight.Add(new InspectionSight(units[first].Id, units[second].Id,
                            GridAlgorithms.HasLineOfSight(state.Map, units[first].Position, units[second].Position)));
                    }
                }
            }

            InspectionDecision? diagnostic = null;
            if (!state.Winner.HasValue)
            {
                UtilityDecision decision = agent.ChooseAction(state, engine, evaluationBudget);
                diagnostic = new InspectionDecision(Command(decision.Command), decision.Score,
                    decision.CandidatesEvaluated, decision.BudgetExhausted, decision.Explanation);
            }

            frames.Add(new InspectionFrame(index, state.TurnNumber, state.ActiveTeam.ToString(), state.Winner?.ToString(), StateHasher.Compute(state),
                units.Select(unit => new InspectionUnit(unit.Id, unit.Team.ToString(), unit.Position.X, unit.Position.Y,
                    unit.HitPoints, unit.MaxHitPoints, new SortedDictionary<string, int>(unit.Cooldowns.ToDictionary(item => item.Key, item => item.Value), StringComparer.Ordinal))).ToArray(),
                sight.ToArray(), index == 0 ? null : Command(snapshot.Steps[index - 1].Command),
                index == snapshot.Steps.Count ? null : Command(snapshot.Steps[index].Command), diagnostic));
        }

        return new ReplayInspection(1, state.Map.Width, state.Map.Height, state.Map.CopyCells(), frames[0].StateHash,
            validation.FinalHash, evaluationBudget, frames.ToArray(),
            "FICTIONAL TURN-BASED GAME REVIEW. All stored command hashes validated. Default-bot suggestions are independently recomputed with current default weights; the recording's policy/weights are unknown. Terrain line of sight is not an ability/range verdict. No real-world tracking, targeting or control.");
    }

    /// <summary>Map immutable game commands without exposing domain mutators to the browser.</summary>
    private static InspectionCommand Command(ActionCommand command) => new(command.Kind.ToString(), command.ActorId,
        command.TargetPosition.X, command.TargetPosition.Y, command.TargetUnitId, command.AbilityId);

    /// <summary>Encode data with the default HTML-safe JSON encoder, and hash only fixed inline JS/CSS for CSP.</summary>
    public static string RenderHtml(ReplayInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        string script = Resource("replay-viewer.js"), style = Resource("replay-viewer.css");
        string scriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        string styleHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(style)));
        string payload = JsonSerializer.Serialize(inspection, Options);
        if (Encoding.UTF8.GetByteCount(payload) > 8 * 1024 * 1024)
        {
            throw new InvalidDataException("Inspection output exceeds eight MiB.");
        }

        return $"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'sha256-{scriptHash}'; style-src 'sha256-{styleHash}'; connect-src 'none'; base-uri 'none'; form-action 'none'">
            <title>Fictional Game Replay Review — Jason Stys</title><style>{style}</style></head><body>
            <header><p>UNITY TACTICAL AI SANDBOX / OFFLINE REVIEW</p><h1>Fictional Game Replay Review</h1><p>Designed and engineered by <a href="https://github.com/JasonStys" rel="noreferrer">Jason Stys</a> · <a href="https://www.linkedin.com/in/jason-stys-31b30b351/" rel="noreferrer">Contact</a></p></header>
            <main><p id="notice"></p><section aria-labelledby="navigation"><h2 id="navigation">Validated timeline</h2><label for="frame">Replay frame <output id="frame-label"></output></label><input id="frame" type="range" min="0" value="0" step="1"><p id="status" role="status"></p><button id="download" type="button">Download inspection JSON</button></section>
            <section aria-labelledby="board"><h2 id="board">Fictional tile board</h2><label for="avatar">Inspect game avatar</label><select id="avatar"></select><p>Blue/red labels identify game teams. Dead avatars are gray. Green lines show terrain-only visibility from the selected living avatar; absent lines do not imply ability legality.</p><svg id="plot" role="img" aria-label="Fictional game grid and avatar positions"></svg><div class="scroll" role="region" aria-label="Scrollable avatar state table" tabindex="0"><table><caption>Avatars at selected frame</caption><thead><tr><th scope="col">Avatar / team</th><th scope="col">Cell</th><th scope="col">Health</th><th scope="col">Cooldowns</th></tr></thead><tbody id="units"></tbody></table></div></section>
            <section aria-labelledby="decisions"><h2 id="decisions">Recorded actions and diagnostic suggestion</h2><p id="previous"></p><p id="next"></p><p id="recommendation"></p><p id="hash"></p></section>
            <details><summary>How to interpret this review</summary><p>The timeline contains initial state plus every validated stored command. Next recorded action is replay hindsight, not a prediction. The default-bot suggestion is computed independently and may differ from the recorded command. Its candidate budget and incomplete search flag stay visible. It does not reconstruct the policy used in the recording. This viewer does not execute commands, edit replays or control Unity/live systems. Inspection is bounded to128 steps/16 avatars/32x32 cells. Manual screen-reader and actual Unity Editor/PlayMode validation remain separate.</p></details></main>
            <script id="data" type="application/json">{payload}</script><script>{script}</script></body></html>
            """;
    }

    /// <summary>Read only compile-time embedded, repository-owned viewer resources.</summary>
    private static string Resource(string name)
    {
        using Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream($"TacticalAI.Serialization.{name}")
            ?? throw new InvalidOperationException("Viewer resource missing.");
        using var reader = new StreamReader(source, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
