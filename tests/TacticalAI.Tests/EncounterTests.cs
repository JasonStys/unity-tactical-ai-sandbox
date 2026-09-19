// File: EncounterTests.cs
// Purpose: Verify seeded generation determinism, variation, and legal placements.
// Public API: Static test cases registered by the test entry point.
// Variables: Seed ranges are deliberately fixed to make failures reproducible.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class EncounterTests
{
    public static void SameSeedSameState()
    {
        Check.Equal(StateHasher.Compute(EncounterGenerator.Create(12345)), StateHasher.Compute(EncounterGenerator.Create(12345)));
    }

    public static void DifferentSeedsVaryState()
    {
        Check.False(string.Equals(
            StateHasher.Compute(EncounterGenerator.Create(12345)),
            StateHasher.Compute(EncounterGenerator.Create(12346)),
            StringComparison.Ordinal));
    }

    public static void SpawnsAreValid()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            TacticalState state = EncounterGenerator.Create(seed);
            foreach (UnitState unit in state.Units)
            {
                Check.True(state.Map.Contains(unit.Position));
                Check.True(state.Map[unit.Position].IsWalkable);
                Check.Equal(1, state.Units.Count(other => other.Position == unit.Position));
            }
        }
    }
}
