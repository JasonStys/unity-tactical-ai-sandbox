// File: GridTests.cs
// Purpose: Verify path optimality, line-of-sight symmetry, influence decay, and grid invariants.
// Public API: Static test cases registered by the test entry point.
// Variables: Maps, paths, and seeds are isolated within individual tests.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class GridTests
{
    public static void AStarFindsLowestCostPath()
    {
        var map = new GridMap(5, 3);
        map[new GridPosition(1, 1)] = new TerrainCell(true, moveCost: 9);
        map[new GridPosition(2, 1)] = new TerrainCell(true, moveCost: 9);
        map[new GridPosition(3, 1)] = new TerrainCell(true, moveCost: 9);
        PathResult result = GridAlgorithms.FindPath(map, new GridPosition(0, 1), new GridPosition(4, 1));
        Check.Equal(6, result.TotalCost);
        Check.Equal(new GridPosition(0, 1), result.Path[0]);
        Check.Equal(new GridPosition(4, 1), result.Path[^1]);
    }

    public static void AStarRejectsBlockedGoal()
    {
        var map = new GridMap(4, 4);
        var goal = new GridPosition(3, 3);
        map[goal] = new TerrainCell(false);
        PathResult result = GridAlgorithms.FindPath(map, new GridPosition(0, 0), goal);
        Check.Equal(0, result.Path.Count);
    }

    public static void AStarObeysExpansionBudget()
    {
        PathResult result = GridAlgorithms.FindPath(new GridMap(20, 20), new GridPosition(0, 0), new GridPosition(19, 19), expansionBudget: 1);
        Check.True(result.BudgetExhausted);
        Check.Equal(1, result.NodesExpanded);
    }

    public static void LineOfSightIsSymmetric()
    {
        var map = new GridMap(9, 7);
        map[new GridPosition(3, 3)] = new TerrainCell(false);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var a = new GridPosition(x, y);
                var b = new GridPosition(map.Width - 1 - x, map.Height - 1 - y);
                Check.Equal(GridAlgorithms.HasLineOfSight(map, a, b), GridAlgorithms.HasLineOfSight(map, b, a));
            }
        }
    }

    public static void LineOfSightDetectsBlocker()
    {
        var map = new GridMap(7, 3);
        map[new GridPosition(3, 1)] = new TerrainCell(false);
        Check.False(GridAlgorithms.HasLineOfSight(map, new GridPosition(0, 1), new GridPosition(6, 1)));
        Check.True(GridAlgorithms.HasLineOfSight(map, new GridPosition(0, 0), new GridPosition(2, 0)));
    }

    public static void InfluenceDecaysWithDistance()
    {
        int[,] field = GridAlgorithms.BuildInfluenceMap(new GridMap(6, 6), new[] { new GridPosition(2, 2) }, radius: 4);
        Check.Equal(100, field[2, 2]);
        Check.True(field[3, 2] > field[4, 2]);
        Check.Equal(0, field[5, 5]);
    }

    public static void PathsAreCardinalAndWalkable()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            TacticalState state = EncounterGenerator.Create(seed);
            UnitState blue = state.Units.First(unit => unit.Team == Team.Blue);
            UnitState red = state.Units.First(unit => unit.Team == Team.Red);
            PathResult result = GridAlgorithms.FindPath(state.Map, blue.Position, red.Position, expansionBudget: 2_048);
            Check.True(result.Path.Count > 0, $"Seed {seed} did not preserve a route.");
            for (int index = 1; index < result.Path.Count; index++)
            {
                Check.Equal(1, result.Path[index - 1].ManhattanDistance(result.Path[index]));
                Check.True(state.Map[result.Path[index]].IsWalkable);
            }
        }
    }
}
