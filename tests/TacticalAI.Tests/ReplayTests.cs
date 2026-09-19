// File: ReplayTests.cs
// Purpose: Verify replay recording, deterministic validation, and tamper detection.
// Public API: Static test cases registered by the test entry point.
// Variables: Commands and altered hashes remain local to each test.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class ReplayTests
{
    public static void RecordValidates()
    {
        TacticalState state = Fixtures.CreateDuel();
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument replay = ReplayService.Record(state, engine, new[]
        {
            new ActionCommand(ActionKind.Move, "blue", new GridPosition(3, 2)),
            new ActionCommand(ActionKind.Wait, "red", new GridPosition(5, 2)),
            new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(5, 2), "red", "shot"),
        });
        ReplayValidation validation = ReplayService.Validate(replay, engine);
        Check.True(validation.Succeeded, validation.Error);
        Check.Equal(3, validation.StepsValidated);
    }

    public static void TamperingIsDetected()
    {
        TacticalState state = Fixtures.CreateDuel();
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument original = ReplayService.Record(state, engine, new[]
        {
            new ActionCommand(ActionKind.Wait, "blue", new GridPosition(1, 2)),
        });
        var tampered = new ReplayDocument(1, original.InitialState, new[]
        {
            new ReplayStep(original.Steps[0].Command, new string('0', 64)),
        });
        ReplayValidation validation = ReplayService.Validate(tampered, engine);
        Check.False(validation.Succeeded);
        Check.True(validation.Error.Contains("mismatch", StringComparison.Ordinal));
    }
}
