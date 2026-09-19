// File: SimulationTests.cs
// Purpose: Verify repeatable headless matches, complete aggregation, and hard command bounds.
// Public API: Static test cases registered by the test entry point.
// Variables: Batch sizes are fixed for stable local and CI runtime.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class SimulationTests
{
    public static void MatchIsRepeatable()
    {
        var runner = new SimulationRunner(Fixtures.CreateEngine());
        MatchResult first = runner.RunMatch(700);
        MatchResult second = runner.RunMatch(700);
        Check.Equal(first.FinalHash, second.FinalHash);
        Check.Equal(first.Winner, second.Winner);
        Check.Equal(first.Commands, second.Commands);
    }

    public static void BatchAccountingIsComplete()
    {
        SimulationSummary summary = new SimulationRunner(Fixtures.CreateEngine()).RunBatch(1, 40);
        Check.Equal(summary.Matches, summary.BlueWins + summary.RedWins + summary.Draws);
        Check.Equal(summary.Results.Sum(result => result.Damage), summary.TotalDamage);
        Check.Equal(summary.Results.Sum(result => result.Commands), summary.ActionCounts.Values.Sum());
        Check.True(summary.Draws <= 4, $"Unexpected disengagement regression: {summary.Draws} of 40 matches drew.");
    }

    public static void CommandsStayBounded()
    {
        var runner = new SimulationRunner(Fixtures.CreateEngine());
        for (ulong seed = 1; seed <= 25; seed++)
        {
            MatchResult result = runner.RunMatch(seed, maximumTurns: 80);
            Check.True(result.Commands <= 80);
            Check.True(result.Turns <= 81);
        }
    }
}
