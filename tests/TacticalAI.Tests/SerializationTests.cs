// File: SerializationTests.cs
// Purpose: Verify save migration, strict format handling, and replay JSON round trips.
// Public API: Static test cases registered by the test entry point.
// Variables: JSON payloads are in-memory fixtures with no filesystem side effects.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;
using TacticalAI.Serialization;

namespace TacticalAI.Tests;

internal static class SerializationTests
{
    public static void SaveRoundTrip()
    {
        TacticalState original = EncounterGenerator.Create(55);
        TacticalState restored = JsonCodec.DeserializeSave(JsonCodec.SerializeSave(original));
        Check.Equal(StateHasher.Compute(original), StateHasher.Compute(restored));
    }

    public static void LegacySaveMigrates()
    {
        TacticalState original = EncounterGenerator.Create(91);
        string legacyJson = System.Text.Json.JsonSerializer.Serialize(
            JsonCodec.ToDocument(original),
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        TacticalState restored = JsonCodec.DeserializeSave(legacyJson);
        Check.Equal(StateHasher.Compute(original), StateHasher.Compute(restored));
    }

    public static void RejectsUnknownVersion()
    {
        Check.Throws<InvalidDataException>(() => JsonCodec.DeserializeSave("{\"formatVersion\":99,\"state\":{}}"));
    }

    public static void ReplayRoundTrip()
    {
        TacticalEngine engine = Fixtures.CreateEngine();
        ReplayDocument replay = ReplayService.Record(Fixtures.CreateDuel(), engine, new[]
        {
            new ActionCommand(ActionKind.Wait, "blue", new GridPosition(1, 2)),
        });
        ReplayDocument restored = JsonCodec.DeserializeReplay(JsonCodec.SerializeReplay(replay));
        ReplayValidation validation = ReplayService.Validate(restored, engine);
        Check.True(validation.Succeeded, validation.Error);
    }
}
