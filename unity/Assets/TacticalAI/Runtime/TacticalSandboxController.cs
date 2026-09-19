// File: TacticalSandboxController.cs
// Purpose: Provide a thin Unity component for running seeded simulation batches in the Editor.
// Public API: TacticalSandboxController.RunSimulation.
// Variables: Serialized seed and match count allow repeatable Inspector-driven experiments.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;
using UnityEngine;

namespace TacticalAI.Unity;

/// <summary>Unity-facing adapter that delegates all rules and AI work to the portable core.</summary>
public sealed class TacticalSandboxController : MonoBehaviour
{
    [SerializeField]
    private int seed = 42;

    [SerializeField]
    private int matchCount = 100;

    /// <summary>Runs a deterministic batch and writes its compact result to the Unity Console.</summary>
    [ContextMenu("Run deterministic simulation")]
    public void RunSimulation()
    {
        var engine = new TacticalEngine(new[]
        {
            new AbilityDefinition("pulse-shot", 34, 5, 1),
            new AbilityDefinition("close-burst", 52, 2, 2),
        });
        SimulationSummary summary = new SimulationRunner(engine).RunBatch((ulong)Math.Max(0, seed), Math.Max(1, matchCount));
        Debug.Log($"Tactical batch: matches={summary.Matches}, blue={summary.BlueWins}, red={summary.RedWins}, draws={summary.Draws}");
    }
}
