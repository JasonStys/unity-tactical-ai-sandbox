// File: TestSupport.cs
// Purpose: Provide focused assertions and deterministic test fixtures without external packages.
// Public API: Check assertion helpers and Fixtures state/engine factories.
// Variables: Fixtures return new objects so tests cannot leak mutable state.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class Check
{
    public static void True(bool condition, string message = "Expected condition to be true.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message = "Expected condition to be false.") => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected <{expected}> but found <{actual}>.");
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} to be thrown.");
    }
}

internal static class Fixtures
{
    public static TacticalEngine CreateEngine(int standardDamage = 40) => new(new[]
    {
        new AbilityDefinition("shot", standardDamage, range: 6, cooldown: 1),
        new AbilityDefinition("burst", 60, range: 2, cooldown: 2),
    });

    public static TacticalState CreateDuel(
        GridPosition? bluePosition = null,
        GridPosition? redPosition = null,
        int redHitPoints = 100,
        int redCover = 0)
    {
        var map = new GridMap(8, 6);
        GridPosition red = redPosition ?? new GridPosition(5, 2);
        map[red] = new TerrainCell(true, cover: redCover);
        return new TacticalState(map, new[]
        {
            new UnitState("blue", Team.Blue, bluePosition ?? new GridPosition(1, 2), 100, 100),
            new UnitState("red", Team.Red, red, redHitPoints, 100),
        });
    }
}
