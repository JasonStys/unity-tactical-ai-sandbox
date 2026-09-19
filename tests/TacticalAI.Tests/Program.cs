// File: Program.cs
// Purpose: Run the dependency-free test suite and emit machine-readable validation evidence.
// Public API: Test executable entry point.
// Variables: Test cases, timings, and failures exist only for the current test process.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using System.Diagnostics;
using System.Text.Json;
using TacticalAI.Tests;

var tests = new (string Name, Action Body)[]
{
    ("grid/a-star-finds-lowest-cost-path", GridTests.AStarFindsLowestCostPath),
    ("grid/a-star-rejects-blocked-goal", GridTests.AStarRejectsBlockedGoal),
    ("grid/a-star-obeys-expansion-budget", GridTests.AStarObeysExpansionBudget),
    ("grid/line-of-sight-is-symmetric", GridTests.LineOfSightIsSymmetric),
    ("grid/line-of-sight-detects-blocker", GridTests.LineOfSightDetectsBlocker),
    ("grid/influence-decays-with-distance", GridTests.InfluenceDecaysWithDistance),
    ("grid/property-paths-are-cardinal-and-walkable", GridTests.PathsAreCardinalAndWalkable),
    ("engine/rejects-wrong-team", EngineTests.RejectsWrongTeam),
    ("engine/move-advances-turn", EngineTests.MoveAdvancesTurn),
    ("engine/rejects-occupied-destination", EngineTests.RejectsOccupiedDestination),
    ("engine/cover-mitigates-damage", EngineTests.CoverMitigatesDamage),
    ("engine/cooldown-prevents-immediate-reuse", EngineTests.CooldownPreventsImmediateReuse),
    ("engine/defeat-produces-winner", EngineTests.DefeatProducesWinner),
    ("engine/state-rejects-overlap", EngineTests.StateRejectsOverlap),
    ("engine/state-requires-opponents", EngineTests.StateRequiresOpponents),
    ("ai/selects-lethal-attack", UtilityTests.SelectsLethalAttack),
    ("ai/decision-is-repeatable", UtilityTests.DecisionIsRepeatable),
    ("ai/reports-budget-exhaustion", UtilityTests.ReportsBudgetExhaustion),
    ("encounter/same-seed-same-state", EncounterTests.SameSeedSameState),
    ("encounter/different-seeds-vary-state", EncounterTests.DifferentSeedsVaryState),
    ("encounter/spawns-are-valid", EncounterTests.SpawnsAreValid),
    ("replay/record-validates", ReplayTests.RecordValidates),
    ("replay/tampering-is-detected", ReplayTests.TamperingIsDetected),
    ("serialization/save-round-trip", SerializationTests.SaveRoundTrip),
    ("serialization/legacy-save-migrates", SerializationTests.LegacySaveMigrates),
    ("serialization/rejects-unknown-version", SerializationTests.RejectsUnknownVersion),
    ("serialization/replay-round-trip", SerializationTests.ReplayRoundTrip),
    ("simulation/match-is-repeatable", SimulationTests.MatchIsRepeatable),
    ("simulation/batch-accounting-is-complete", SimulationTests.BatchAccountingIsComplete),
    ("simulation/commands-stay-bounded", SimulationTests.CommandsStayBounded),
    ("performance/a-star-budget", PerformanceTests.AStarBudget),
    ("performance/bot-batch-budget", PerformanceTests.BotBatchBudget),
};

string? reportPath = ReadReportPath(args);
var outcomes = new List<object>(tests.Length);
int failures = 0;
var suiteTimer = Stopwatch.StartNew();
foreach ((string name, Action body) in tests)
{
    var timer = Stopwatch.StartNew();
    try
    {
        body();
        timer.Stop();
        Console.WriteLine($"PASS {name} ({timer.ElapsedMilliseconds} ms)");
        outcomes.Add(new { name, passed = true, durationMs = timer.ElapsedMilliseconds, error = string.Empty });
    }
    catch (Exception exception)
    {
        timer.Stop();
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        outcomes.Add(new { name, passed = false, durationMs = timer.ElapsedMilliseconds, error = exception.ToString() });
    }
}

suiteTimer.Stop();
var report = new
{
    schemaVersion = "1.0",
    total = tests.Length,
    passed = tests.Length - failures,
    failed = failures,
    durationMs = suiteTimer.ElapsedMilliseconds,
    tests = outcomes,
};
if (reportPath is not null)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }

    string reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    File.WriteAllText(reportPath, reportJson);
}

Console.WriteLine($"RESULT total={tests.Length} passed={tests.Length - failures} failed={failures} durationMs={suiteTimer.ElapsedMilliseconds}");
return failures == 0 ? 0 : 1;

static string? ReadReportPath(string[] arguments)
{
    int index = Array.IndexOf(arguments, "--report");
    if (index < 0)
    {
        return null;
    }

    if (index + 1 >= arguments.Length)
    {
        throw new ArgumentException("--report requires a path.");
    }

    return arguments[index + 1];
}
