// File: EngineTests.cs
// Purpose: Verify command validation, turn transitions, cover, cooldowns, and victory rules.
// Public API: Static test cases registered by the test entry point.
// Variables: Each test owns a fresh duel fixture and engine.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class EngineTests
{
    public static void RejectsWrongTeam()
    {
        TacticalState state = Fixtures.CreateDuel();
        ActionResult result = Fixtures.CreateEngine().Apply(state, new ActionCommand(ActionKind.Wait, "red", new GridPosition(5, 2)));
        Check.False(result.Succeeded);
        Check.Equal(1, state.TurnNumber);
    }

    public static void MoveAdvancesTurn()
    {
        TacticalState state = Fixtures.CreateDuel();
        ActionResult result = Fixtures.CreateEngine().Apply(state, new ActionCommand(ActionKind.Move, "blue", new GridPosition(3, 2)));
        Check.True(result.Succeeded, result.Error);
        Check.Equal(new GridPosition(3, 2), state.FindUnit("blue")!.Position);
        Check.Equal(Team.Red, state.ActiveTeam);
        Check.Equal(2, state.TurnNumber);
    }

    public static void RejectsOccupiedDestination()
    {
        TacticalState state = Fixtures.CreateDuel();
        ActionResult result = Fixtures.CreateEngine().Apply(state, new ActionCommand(ActionKind.Move, "blue", new GridPosition(5, 2)));
        Check.False(result.Succeeded);
    }

    public static void CoverMitigatesDamage()
    {
        TacticalState state = Fixtures.CreateDuel(redCover: 50);
        ActionResult result = Fixtures.CreateEngine().Apply(state, new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(5, 2), "red", "shot"));
        Check.True(result.Succeeded, result.Error);
        Check.Equal(80, state.FindUnit("red")!.HitPoints);
    }

    public static void CooldownPreventsImmediateReuse()
    {
        TacticalState state = Fixtures.CreateDuel(bluePosition: new GridPosition(2, 2), redPosition: new GridPosition(4, 2));
        TacticalEngine engine = Fixtures.CreateEngine();
        Check.True(engine.Apply(state, new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(4, 2), "red", "burst")).Succeeded);
        Check.True(engine.Apply(state, new ActionCommand(ActionKind.Wait, "red", new GridPosition(4, 2))).Succeeded);
        ActionResult result = engine.Apply(state, new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(4, 2), "red", "burst"));
        Check.False(result.Succeeded);
    }

    public static void DefeatProducesWinner()
    {
        TacticalState state = Fixtures.CreateDuel(redHitPoints: 20);
        ActionResult result = Fixtures.CreateEngine().Apply(state, new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(5, 2), "red", "shot"));
        Check.True(result.Succeeded, result.Error);
        Check.Equal(Team.Blue, state.Winner);
        Check.True(result.Events.Any(value => value.Type == "defeat"));
    }

    public static void StateRejectsOverlap()
    {
        var map = new GridMap(4, 4);
        var position = new GridPosition(1, 1);
        Check.Throws<ArgumentException>(() => new TacticalState(map, new[]
        {
            new UnitState("blue", Team.Blue, position, 100, 100),
            new UnitState("red", Team.Red, position, 100, 100),
        }));
    }

    public static void StateRequiresOpponents()
    {
        var map = new GridMap(4, 4);
        Check.Throws<ArgumentException>(() => new TacticalState(map, new[]
        {
            new UnitState("blue-1", Team.Blue, new GridPosition(0, 0), 100, 100),
            new UnitState("blue-2", Team.Blue, new GridPosition(1, 0), 100, 100),
        }));
    }
}
