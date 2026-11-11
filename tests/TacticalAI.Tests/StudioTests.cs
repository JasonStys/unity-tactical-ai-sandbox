// File: StudioTests.cs
// Purpose: Independently verify real designer commands, historical state, rejected edits, round trips and bounds.
// Public API: Explicit dependency-free regression cases registered in the existing original test runner.
// Variables: small human-authored board data and literal damage/turn/position oracles; no network or UI mock.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Text.Json;
using TacticalAI.Core;
using TacticalAI.Serialization;
using TacticalAI.Studio;

namespace TacticalAI.Tests;

internal static class StudioTests
{
    /// <summary>Literal four-by-two empty board, independent of the encounter generator or target outputs.</summary>
    internal static string Duel => """
        {"formatVersion":1,"state":{"width":4,"height":2,"activeTeam":0,"turnNumber":1,
        "cells":[{"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0}],
        "units":[{"id":"BlueA","team":0,"x":0,"y":0,"hitPoints":100,"maxHitPoints":100,"cooldowns":{}},
        {"id":"RedA","team":1,"x":3,"y":0,"hitPoints":100,"maxHitPoints":100,"cooldowns":{}}]}}
        """;

    public static void LiteralMovementAndDamage()
    {
        var session = DesignerSession.FromSave(Duel, "literal duel");
        Check.True(session.Append(new ActionCommand(ActionKind.Move, "BlueA", new GridPosition(1, 0))).Succeeded);
        Check.Equal(2, session.Frame.Turn);
        Check.Equal("Red", session.Frame.ActiveTeam);
        Check.Equal(1, session.Frame.Units.Single(u => u.Id == "BlueA").X);
        Check.True(session.Append(new ActionCommand(ActionKind.UseAbility, "RedA", default, "BlueA", "pulse-shot")).Succeeded);
        Check.Equal(66, session.Frame.Units.Single(u => u.Id == "BlueA").HitPoints);
        Check.Equal(3, session.Frame.Turn);
        Check.Equal(2, session.Count);
        Check.True(ReplayService.Validate(JsonCodec.DeserializeReplay(session.ReplayJson()), DesignerSession.CreateEngine()).Succeeded);
    }

    public static void RejectedCommandsPreserveAllAcceptedState()
    {
        var session = DesignerSession.FromSave(Duel, "atomic refusals");
        string before = session.ProjectJson();
        foreach (ActionCommand command in new[] {
            new ActionCommand(ActionKind.Wait, "RedA", default),
            new ActionCommand(ActionKind.Move, "BlueA", new GridPosition(3, 0)),
            new ActionCommand(ActionKind.UseAbility, "BlueA", default, "BlueA", "pulse-shot"),
            new ActionCommand(ActionKind.UseAbility, "BlueA", default, "RedA", "unknown"),
        })
        {
            Check.False(session.Append(command).Succeeded);
            Check.Equal(before, session.ProjectJson());
        }
    }

    public static void HistoricalFramesCannotAppendOrTruncate()
    {
        var session = DesignerSession.FromSave(Duel, "history");
        session.Append(new ActionCommand(ActionKind.Wait, "BlueA", default));
        string tailHash = session.Frame.StateHash;
        session.SetCursor(0);
        string before = session.ProjectJson();
        Check.Throws<InvalidOperationException>(() => session.Append(new ActionCommand(ActionKind.Wait, "BlueA", default)));
        Check.Equal(before, session.ProjectJson());
        Check.Equal(1, session.Count);
        session.SetCursor(1);
        Check.Equal(tailHash, session.Frame.StateHash);
    }

    public static void ProjectRoundTripRetainsFullHistoryAndSelectedFrame()
    {
        var session = DesignerSession.FromSave(Duel, "round trip");
        session.Append(new ActionCommand(ActionKind.Wait, "BlueA", default));
        session.Append(new ActionCommand(ActionKind.Wait, "RedA", default));
        session.SetCursor(1);
        var loaded = DesignerSession.FromProject(session.ProjectJson());
        Check.Equal(2, loaded.Count);
        Check.Equal(1, loaded.Cursor);
        Check.Equal(2, loaded.Frame.Turn);
        Check.Equal(session.ReplayJson(), loaded.ReplayJson());
        Check.Equal(session.ProjectJson(), loaded.ProjectJson());
    }

    public static void TamperedReplayNeverExposesAFrame()
    {
        var session = DesignerSession.FromSave(Duel, "tamper");
        session.Append(new ActionCommand(ActionKind.Wait, "BlueA", default));
        string invalid = session.ReplayJson().Replace(session.Frame.StateHash, new string('0', 64), StringComparison.Ordinal);
        Check.Throws<InvalidDataException>(() => DesignerSession.FromReplay(invalid, "invalid"));
    }

    public static void StrictInputRefusesAmbiguityAndUnknownFields()
    {
        Check.Throws<InvalidDataException>(() => DesignerSession.FromSave(Duel.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1", StringComparison.Ordinal), "duplicate"));
        Check.Throws<JsonException>(() => DesignerSession.FromSave(Duel.Replace("\"width\":4", "\"width\":4,\"unknown\":true", StringComparison.Ordinal), "unknown"));
        Check.Throws<InvalidDataException>(() => DesignerSession.FromSave(Duel.Replace("\"width\":4", "\"width\":33", StringComparison.Ordinal), "oversized"));
        Check.Throws<InvalidDataException>(() => DesignerSession.FromSave(Duel.Replace("\"turnNumber\":1", "\"turnNumber\":2147483647", StringComparison.Ordinal), "turn-overflow"));
        Check.Throws<InvalidDataException>(() => DesignerSession.FromSave(new string('x', DesignerSession.MaximumInputBytes + 1), "oversized text"));
        Check.Throws<InvalidDataException>(() => DesignerSession.FromSave(Duel.Replace("\"cooldowns\":{}", "\"cooldowns\":null", StringComparison.Ordinal), "null map"));
    }

    public static void LegacySaveIsStillSupported()
    {
        using JsonDocument document = JsonDocument.Parse(Duel);
        var old = DesignerSession.FromSave(document.RootElement.GetProperty("state").GetRawText(), "legacy");
        Check.Equal(4, old.Inspection.Width);
        Check.Equal(0, old.Count);
        Check.Equal(100, old.Frame.Units.Single(u => u.Id == "BlueA").HitPoints);
    }

    public static void TerrainPreservesOccupiedAndRecordedStates()
    {
        var session = DesignerSession.FromSave(Duel, "terrain");
        string before = session.ProjectJson();
        Check.Throws<InvalidDataException>(() => session.EditTerrain(0, 0, false, 1, 0));
        Check.Equal(before, session.ProjectJson());
        session.EditTerrain(2, 1, false, 2, 50);
        Check.False(session.Inspection.Cells[6].IsWalkable);
        session.Append(new ActionCommand(ActionKind.Wait, "BlueA", default));
        string recorded = session.ProjectJson();
        Check.Throws<InvalidOperationException>(() => session.EditTerrain(1, 1, true, 1, 0));
        Check.Equal(recorded, session.ProjectJson());
    }

    public static void TerminalStateHasNoInventedBotDecision()
    {
        var session = DesignerSession.FromSave(Duel.Replace("\"id\":\"RedA\",\"team\":1,\"x\":3,\"y\":0,\"hitPoints\":100", "\"id\":\"RedA\",\"team\":1,\"x\":3,\"y\":0,\"hitPoints\":34", StringComparison.Ordinal), "terminal");
        Check.True(session.Append(new ActionCommand(ActionKind.UseAbility, "BlueA", default, "RedA", "pulse-shot")).Succeeded);
        Check.Equal("Blue", session.Frame.Winner);
        Check.Equal(0, session.Frame.Units.Single(u => u.Id == "RedA").HitPoints);
        Check.True(session.Frame.DefaultBot is null);
        Check.Equal(1, session.Count);
    }

    public static void BotAndTournamentRemainOriginalAndBounded()
    {
        var first = DesignerSession.FromSave(Duel, "default bot");
        var second = DesignerSession.FromSave(Duel, "default bot");
        first.AdvanceDefaultBot(64); second.AdvanceDefaultBot(64);
        Check.Equal(first.ProjectJson(), second.ProjectJson());
        Check.Throws<ArgumentOutOfRangeException>(() => first.AdvanceDefaultBot(0));
        var summary = DesignerSession.Tournament(42, 3, 4);
        Check.Equal(3, summary.Matches);
        Check.Equal(3, summary.BlueWins + summary.RedWins + summary.Draws);
        Check.True(summary.Results.All(r => r.Commands <= 4));
        Check.Equal(42UL, summary.Results[0].Seed);
        Check.Equal(44UL, summary.Results[2].Seed);
        Check.Throws<ArgumentOutOfRangeException>(() => DesignerSession.Tournament(ulong.MaxValue, 2, 2));
        Check.Throws<ArgumentOutOfRangeException>(() => DesignerSession.Tournament(0, 101, 1));
    }

    public static void ReviewExportsRetainOriginalInertViewer()
    {
        var session = DesignerSession.FromSave(Duel, "<script>inert title</script>");
        string html = session.InspectionHtml();
        Check.True(html.Contains("Content-Security-Policy", StringComparison.Ordinal));
        Check.True(session.InspectionJson().Contains("\"defaultBot\"", StringComparison.Ordinal));
        Check.False(html.Contains("<script>inert title</script>", StringComparison.Ordinal));
    }

    public static void LegalMoveOverDefeatedAvatarStillRoundTrips()
    {
        // Three avatars keep play active after RedA is defeated; this is a legal original-engine move.
        string json = Duel.Replace("\"id\":\"RedA\",\"team\":1,\"x\":3,\"y\":0,\"hitPoints\":100", "\"id\":\"RedA\",\"team\":1,\"x\":3,\"y\":0,\"hitPoints\":34", StringComparison.Ordinal)
            .Replace("\"cooldowns\":{}}]", "\"cooldowns\":{}},{\"id\":\"RedB\",\"team\":1,\"x\":3,\"y\":1,\"hitPoints\":100,\"maxHitPoints\":100,\"cooldowns\":{}}]", StringComparison.Ordinal);
        var session = DesignerSession.FromSave(json, "defeated-cell regression");
        Check.True(session.Append(new ActionCommand(ActionKind.UseAbility, "BlueA", default, "RedA", "pulse-shot")).Succeeded);
        Check.True(session.Append(new ActionCommand(ActionKind.Wait, "RedB", default)).Succeeded);
        Check.True(session.Append(new ActionCommand(ActionKind.Move, "BlueA", new GridPosition(3, 0))).Succeeded);
        Check.Equal(0, session.Frame.Units.Single(u => u.Id == "RedA").HitPoints);
        Check.Equal(3, session.Frame.Units.Single(u => u.Id == "BlueA").X);
        var imported = DesignerSession.FromSave(session.SaveJson(), "legal exported state");
        Check.Equal(session.Frame.StateHash, imported.Frame.StateHash);
        Check.Equal(session.Frame.StateHash, StateHasher.Compute(session.StateAt(3).Clone()));
    }
}
