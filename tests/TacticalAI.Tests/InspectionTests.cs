// File: InspectionTests.cs
// Purpose: Independently verify game review state, hash, bounds, terminal and HTML safety contracts.
// Variables: fixtures are private clones; expected coordinates/health are hand-calculated.
// Declaration index: docs/CODE_INDEX.md records exact declarations. Author: Jason Stys.
using TacticalAI.Core;
using TacticalAI.Serialization;

namespace TacticalAI.Tests;

internal static class InspectionTests
{
    public static void FramesMatchRecordedStates()
    {
        TacticalState initial = Fixtures.CreateDuel();
        string before = StateHasher.Compute(initial);
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument replay = ReplayService.Record(initial, engine, new[] {
            new ActionCommand(ActionKind.Move, "blue", new GridPosition(3, 2)),
            new ActionCommand(ActionKind.Wait, "red", new GridPosition(5, 2)),
            new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(5, 2), "red", "shot"),
        });
        ReplayInspection report = ReplayInspectionService.Create(replay, engine);
        Check.Equal(4, report.Frames.Count);
        Check.Equal(1, report.Frames[0].Units.Single(unit => unit.Id == "blue").X);
        Check.Equal(3, report.Frames[1].Units.Single(unit => unit.Id == "blue").X);
        Check.Equal(60, report.Frames[3].Units.Single(unit => unit.Id == "red").HitPoints);
        Check.Equal(before, StateHasher.Compute(initial));
        Check.Equal(before, StateHasher.Compute(replay.InitialState));
        for (int index = 1; index < report.Frames.Count; index++)
        {
            Check.Equal(replay.Steps[index - 1].StateHash, report.Frames[index].StateHash);
            Check.True(report.Frames[index].DefaultBot is null || report.Frames[index].DefaultBot!.CandidatesEvaluated <= 64);
        }
        Check.Equal(report.Frames[^1].StateHash, report.FinalHash);
        Check.Equal("Move", report.Frames[0].NextRecordedCommand!.Kind);
        Check.True(report.Frames[^1].NextRecordedCommand is null);
    }

    public static void RejectsInvalidOrOversizedReplay()
    {
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument replay = ReplayService.Record(Fixtures.CreateDuel(), engine, new[] { new ActionCommand(ActionKind.Wait, "blue", new GridPosition(1, 2)) });
        var tampered = new ReplayDocument(1, replay.InitialState, new[] { new ReplayStep(replay.Steps[0].Command, new string('0', 64)) });
        Check.Throws<InvalidDataException>(() => ReplayInspectionService.Create(tampered, engine));
        Check.Throws<InvalidDataException>(() => ReplayInspectionService.Create(new ReplayDocument(1, replay.InitialState, Enumerable.Repeat(replay.Steps[0], 129)), engine));
        Check.Throws<ArgumentOutOfRangeException>(() => ReplayInspectionService.Create(replay, engine, 0));
        Check.Throws<ArgumentOutOfRangeException>(() => ReplayInspectionService.Create(replay, engine, 257));
    }

    public static void TerminalAndBudgetEvidence()
    {
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument replay = ReplayService.Record(Fixtures.CreateDuel(redHitPoints: 1), engine, new[] { new ActionCommand(ActionKind.UseAbility, "blue", new GridPosition(5, 2), "red", "shot") });
        ReplayInspection report = ReplayInspectionService.Create(replay, engine);
        Check.Equal("Blue", report.Frames[^1].Winner);
        Check.True(report.Frames[^1].DefaultBot is null);
        var bounded = new ReplayDocument(1, EncounterGenerator.Create(4, unitsPerTeam: 4), Array.Empty<ReplayStep>());
        InspectionDecision decision = ReplayInspectionService.Create(bounded, engine, 1).Frames[0].DefaultBot!;
        Check.Equal(1, decision.CandidatesEvaluated);
        Check.True(decision.BudgetExhausted);
    }

    public static void HtmlDoesNotInterpretIdentifiers()
    {
        var state = new TacticalState(new GridMap(8, 6), new[] {
            new UnitState("</script><img src=x onerror=alert(1)>", Team.Blue, new GridPosition(1,2),100,100),
            new UnitState("red", Team.Red, new GridPosition(5,2),100,100),
        });
        ReplayInspection report = ReplayInspectionService.Create(new ReplayDocument(1, state, Array.Empty<ReplayStep>()), Fixtures.CreateEngine());
        string html = ReplayInspectionService.RenderHtml(report);
        Check.False(html.Contains("</script><img", StringComparison.Ordinal));
        Check.True(html.Contains("\\u003C", StringComparison.Ordinal));
        Check.True(html.Contains("connect-src 'none'", StringComparison.Ordinal));
        Check.True(html.Contains("script-src 'sha256-", StringComparison.Ordinal));
        Check.Throws<InvalidDataException>(() => ReplayInspectionService.RenderHtml(report with { Notice = new string('x', 9 * 1024 * 1024) }));
    }
}
