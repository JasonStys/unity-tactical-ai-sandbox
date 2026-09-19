// File: PerformanceTests.cs
// Purpose: Guard broad pathfinding and headless-simulation latency budgets against regressions.
// Public API: Static performance test cases registered by the test entry point.
// Variables: Stopwatches measure generous CI budgets rather than microbenchmark claims.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using System.Diagnostics;
using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class PerformanceTests
{
    public static void AStarBudget()
    {
        var map = new GridMap(64, 64);
        var timer = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 500; iteration++)
        {
            PathResult path = GridAlgorithms.FindPath(map, new GridPosition(0, 0), new GridPosition(63, 63), expansionBudget: 8_192);
            Check.Equal(127, path.Path.Count);
        }

        timer.Stop();
        Check.True(timer.Elapsed < TimeSpan.FromSeconds(10), $"A* regression budget exceeded: {timer.Elapsed}.");
    }

    public static void BotBatchBudget()
    {
        var timer = Stopwatch.StartNew();
        SimulationSummary summary = new SimulationRunner(Fixtures.CreateEngine()).RunBatch(100, 100, maximumTurns: 100);
        timer.Stop();
        Check.Equal(100, summary.Matches);
        Check.True(timer.Elapsed < TimeSpan.FromSeconds(20), $"Bot batch regression budget exceeded: {timer.Elapsed}.");
    }
}
