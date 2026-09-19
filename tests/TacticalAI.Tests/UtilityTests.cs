// File: UtilityTests.cs
// Purpose: Verify explainable scoring, stable decisions, and evaluation-budget behavior.
// Public API: Static test cases registered by the test entry point.
// Variables: Decisions are made from independent state clones.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;

namespace TacticalAI.Tests;

internal static class UtilityTests
{
    public static void SelectsLethalAttack()
    {
        TacticalState state = Fixtures.CreateDuel(redHitPoints: 20);
        UtilityDecision decision = new UtilityAgent().ChooseAction(state, Fixtures.CreateEngine());
        Check.Equal(ActionKind.UseAbility, decision.Command.Kind);
        Check.Equal("red", decision.Command.TargetUnitId);
        Check.True(decision.Explanation.Contains("defeatBonus=", StringComparison.Ordinal));
    }

    public static void DecisionIsRepeatable()
    {
        TacticalState state = EncounterGenerator.Create(9876);
        TacticalEngine engine = Fixtures.CreateEngine();
        var agent = new UtilityAgent();
        string expected = agent.ChooseAction(state.Clone(), engine).Command.StableKey();
        for (int iteration = 0; iteration < 50; iteration++)
        {
            Check.Equal(expected, agent.ChooseAction(state.Clone(), engine).Command.StableKey());
        }
    }

    public static void ReportsBudgetExhaustion()
    {
        TacticalState state = EncounterGenerator.Create(4, unitsPerTeam: 4);
        UtilityDecision decision = new UtilityAgent().ChooseAction(state, Fixtures.CreateEngine(), evaluationBudget: 1);
        Check.True(decision.BudgetExhausted);
        Check.Equal(1, decision.CandidatesEvaluated);
    }
}
