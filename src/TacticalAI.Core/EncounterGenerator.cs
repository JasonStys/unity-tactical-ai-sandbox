// File: EncounterGenerator.cs
// Purpose: Generate repeatable tactical maps and opposing squads from a 64-bit seed.
// Public API: EncounterGenerator.Create and SplitMix64.
// Variables: PRNG state is explicit and local to each generator instance.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Small deterministic generator with a fixed cross-platform algorithm.</summary>
public sealed class SplitMix64
{
    private ulong state;

    public SplitMix64(ulong seed)
    {
        state = seed;
    }

    /// <summary>Returns the next uniformly distributed 64-bit value.</summary>
    public ulong NextUInt64()
    {
        ulong z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a value in the half-open interval [minimum, maximum).</summary>
    public int NextInt32(int minimum, int maximum)
    {
        if (minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        return minimum + (int)(NextUInt64() % (uint)(maximum - minimum));
    }
}

/// <summary>Builds seeded maps that preserve reachable lanes between both teams.</summary>
public static class EncounterGenerator
{
    /// <summary>Creates a repeatable encounter with symmetric squad sizes and varied terrain.</summary>
    public static TacticalState Create(ulong seed, int width = 12, int height = 8, int unitsPerTeam = 2)
    {
        if (unitsPerTeam < 1 || unitsPerTeam > Math.Min(height, 8))
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerTeam));
        }

        var random = new SplitMix64(seed);
        var map = new GridMap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 2; x < width - 2; x++)
            {
                var position = new GridPosition(x, y);
                int roll = random.NextInt32(0, 100);
                if (roll < 12 && y != height / 2)
                {
                    map[position] = new TerrainCell(false);
                }
                else
                {
                    int cover = roll < 42 ? random.NextInt32(15, 51) : 0;
                    int moveCost = roll >= 90 ? 2 : 1;
                    map[position] = new TerrainCell(true, moveCost, cover);
                }
            }
        }

        var units = new List<UnitState>(unitsPerTeam * 2);
        for (int index = 0; index < unitsPerTeam; index++)
        {
            int y = ((index + 1) * height) / (unitsPerTeam + 1);
            units.Add(new UnitState($"blue-{index + 1}", Team.Blue, new GridPosition(0, y), 100, 100));
            units.Add(new UnitState($"red-{index + 1}", Team.Red, new GridPosition(width - 1, height - 1 - y), 100, 100));
        }

        return new TacticalState(map, units);
    }
}
