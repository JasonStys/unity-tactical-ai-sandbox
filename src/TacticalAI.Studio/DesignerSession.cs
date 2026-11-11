// File: DesignerSession.cs
// Purpose: Compose original game rules, validated replays, scenario authoring and seeded balance evidence.
// Public API: DesignerSession provides atomic bounded edits and explicit historical-frame inspection.
// Variables: session replay/cursor are private; failed input/actions never replace accepted state.
// Declaration index: docs/CODE_INDEX.md contains generated exact locations. Author: Jason Stys.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TacticalAI.Core;
using TacticalAI.Serialization;

namespace TacticalAI.Studio;

/// <summary>A useful offline game-design session over the real portable engine, not Unity PlayMode.</summary>
public sealed class DesignerSession
{
    public const int MaximumInputBytes = 1_048_576;
    private static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
    };
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private ReplayDocument replay;

    private DesignerSession(string title, ReplayDocument replay, int cursor)
    {
        Title = ValidateTitle(title);
        this.replay = replay;
        Inspection = ReplayInspectionService.Create(replay, CreateEngine());
        SetCursor(cursor);
    }

    public string Title { get; private set; }
    public int Cursor { get; private set; }
    public int Count => replay.Steps.Count;
    public ReplayInspection Inspection { get; private set; }
    public InspectionFrame Frame => Inspection.Frames[Cursor];
    public bool AtTail => Cursor == Count;

    /// <summary>Use the unchanged original two-ability catalog shared with the headless CLI.</summary>
    public static TacticalEngine CreateEngine() => new(new[]
    {
        new AbilityDefinition("pulse-shot", 34, 5, 1),
        new AbilityDefinition("close-burst", 52, 2, 2),
    });

    /// <summary>Prepare data inside the full designer; no different demo executable is involved.</summary>
    public static DesignerSession Prepared(ulong seed) => new($"Seeded encounter {seed}",
        new ReplayDocument(1, EncounterGenerator.Create(seed), Array.Empty<ReplayStep>()), 0);

    /// <summary>Reject duplicates, oversized text and non-object JSON before domain materialization.</summary>
    private static JsonDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumInputBytes || Encoding.UTF8.GetByteCount(json) > MaximumInputBytes)
            throw new InvalidDataException("Input exceeds one MiB of UTF-8.");
        var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("A complete JSON object is required.");
            Unique(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>Property ambiguity at any nesting level is an error, not last-value-wins input.</summary>
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property.");
                Unique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) Unique(item);
    }

    /// <summary>Keep designer evidence within the original replay viewer's supported state budget.</summary>
    private static void ValidateState(StateDocument? state)
    {
        if (state is null || state.Width is < 2 or > 32 || state.Height is < 2 or > 32 ||
            state.TurnNumber is < 1 or > int.MaxValue - 128 ||
            state.Cells is null || state.Cells.Count != state.Width * state.Height ||
            state.Units is null || state.Units.Count is < 2 or > 16 ||
            state.Cells.Any(cell => cell is null || cell.MoveCost is < 1 or > 100) ||
            state.Units.Any(unit => unit is null || unit.Id is null || unit.Id.Length is < 1 or > 64 ||
                unit.Cooldowns is null || unit.Cooldowns.Count > 32 || unit.Cooldowns.Keys.Any(key => key.Length is < 1 or > 64)) ||
            !state.Units.Any(unit => unit.HitPoints > 0))
            throw new InvalidDataException("Designer supports 2..32 cells per side, 2..16 avatars, bounded identifiers and movement costs 1..100.");
    }

    /// <summary>Import current or original legacy scenario JSON without silently dropping unknown fields.</summary>
    public static DesignerSession FromSave(string json, string title)
    {
        using JsonDocument document = Parse(json);
        StateDocument? state;
        if (document.RootElement.TryGetProperty("formatVersion", out JsonElement version))
        {
            if (version.GetInt32() != 1) throw new InvalidDataException("Unsupported save version.");
            state = JsonSerializer.Deserialize<SaveDocument>(json, Strict)?.State;
        }
        else state = JsonSerializer.Deserialize<StateDocument>(json, Strict);
        ValidateState(state);
        TacticalState accepted = JsonCodec.FromDocument(state!);
        return new DesignerSession(title, new ReplayDocument(1, accepted, Array.Empty<ReplayStep>()), 0);
    }

    /// <summary>Fully validate command/hash history before exposing any imported frame.</summary>
    public static DesignerSession FromReplay(string json, string title, int? cursor = null)
    {
        using JsonDocument document = Parse(json);
        ReplayFile? file = JsonSerializer.Deserialize<ReplayFile>(json, Strict);
        if (file is null || file.FormatVersion != 1 || file.Steps is null || file.Steps.Count > 128 ||
            file.Steps.Any(step => step is null || step.ActorId is null || step.StateHash is null ||
                step.ActorId.Length > 64 || step.TargetUnitId?.Length > 64 || step.AbilityId?.Length > 64 ||
                step.StateHash.Length != 64 || !step.StateHash.All(Uri.IsHexDigit)))
            throw new InvalidDataException("Replay schema, command count or hash is invalid.");
        ValidateState(file.InitialState);
        ReplayDocument accepted = JsonCodec.DeserializeReplay(json);
        return new DesignerSession(title, accepted, cursor ?? accepted.Steps.Count);
    }

    /// <summary>Open the closed owner-local project envelope and validate its complete replay.</summary>
    public static DesignerSession FromProject(string json)
    {
        using JsonDocument document = Parse(json);
        JsonElement root = document.RootElement;
        if (!root.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[] { "schemaVersion", "title", "replay", "cursor" }) || root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported complete designer project.");
        return FromReplay(root.GetProperty("replay").GetRawText(), root.GetProperty("title").GetString()!, root.GetProperty("cursor").GetInt32());
    }

    /// <summary>Serialize title/history/frame, not a misleading hash-only or GUI screenshot snapshot.</summary>
    public string ProjectJson()
    {
        using JsonDocument replayJson = JsonDocument.Parse(JsonCodec.SerializeReplay(replay));
        return JsonSerializer.Serialize(new { schemaVersion = 1, title = Title, replay = replayJson.RootElement, cursor = Cursor }, Pretty) + "\n";
    }

    public string ReplayJson() => JsonCodec.SerializeReplay(replay);
    public string SaveJson() => JsonCodec.SerializeSave(StateAt(Cursor));
    public string InspectionHtml() => ReplayInspectionService.RenderHtml(Inspection);
    public string InspectionJson() => JsonSerializer.Serialize(Inspection, Pretty) + "\n";

    /// <summary>Historical navigation does not mutate or truncate recorded commands.</summary>
    public void SetCursor(int cursor)
    {
        if (cursor < 0 || cursor > Count) throw new ArgumentOutOfRangeException(nameof(cursor));
        Cursor = cursor;
    }

    public void SetTitle(string title) => Title = ValidateTitle(title);

    private static string ValidateTitle(string title) =>
        !string.IsNullOrWhiteSpace(title) && title.Length <= 100 && !title.Any(char.IsControl)
            ? title : throw new InvalidDataException("A readable title of 1..100 characters is required.");

    /// <summary>Replay only accepted commands with the original engine into a private clone.</summary>
    public TacticalState StateAt(int cursor)
    {
        if (cursor < 0 || cursor > Count) throw new ArgumentOutOfRangeException(nameof(cursor));
        TacticalState state = replay.InitialState.Clone();
        var engine = CreateEngine();
        for (int index = 0; index < cursor; index++)
        {
            ReplayStep step = replay.Steps[index];
            if (!engine.Apply(state, step.Command).Succeeded || StateHasher.Compute(state) != step.StateHash)
                throw new InvalidDataException("Accepted replay no longer validates.");
        }
        return state;
    }

    /// <summary>Apply one legal command atomically; reject appending while a historical frame is selected.</summary>
    public ActionResult Append(ActionCommand command)
    {
        if (!AtTail) throw new InvalidOperationException("Return to the final frame before issuing a new command.");
        if (Count >= 128) throw new InvalidOperationException("The 128-command recording bound was reached; export it before starting another scenario.");
        TacticalState next = StateAt(Count);
        ActionResult result = CreateEngine().Apply(next, command);
        if (!result.Succeeded) return result;
        var accepted = new ReplayDocument(1, replay.InitialState, replay.Steps.Append(new ReplayStep(command, StateHasher.Compute(next))));
        ReplayInspection inspection = ReplayInspectionService.Create(accepted, CreateEngine());
        replay = accepted;
        Inspection = inspection;
        Cursor = Count;
        return result;
    }

    /// <summary>The actual bounded default agent chooses and executes a fictional game command.</summary>
    public UtilityDecision AdvanceDefaultBot(int budget)
    {
        if (budget is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(budget));
        if (!AtTail) throw new InvalidOperationException("Return to the final frame before advancing the bot.");
        UtilityDecision decision = new UtilityAgent().ChooseAction(StateAt(Count), CreateEngine(), budget);
        ActionResult result = Append(decision.Command);
        if (!result.Succeeded) throw new InvalidOperationException(result.Error);
        return decision;
    }

    /// <summary>Edit initial terrain explicitly before any turn; avatars may not become embedded in walls.</summary>
    public void EditTerrain(int x, int y, bool walkable, int cost, int cover)
    {
        if (Count != 0) throw new InvalidOperationException("Terrain editing requires an initial scenario with no recorded commands.");
        if (cost is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(cost));
        TacticalState next = StateAt(0);
        var position = new GridPosition(x, y);
        if (!walkable && next.IsOccupied(position)) throw new InvalidDataException("An avatar occupies that cell.");
        next.Map[position] = new TerrainCell(walkable, cost, cover);
        var accepted = new ReplayDocument(1, next, Array.Empty<ReplayStep>());
        ReplayInspection inspection = ReplayInspectionService.Create(accepted, CreateEngine());
        replay = accepted;
        Inspection = inspection;
    }

    /// <summary>Benchmark original generated encounters, explicitly separate from an edited board.</summary>
    public static SimulationSummary Tournament(ulong firstSeed, int matches, int turns)
    {
        if (matches is < 1 or > 100 || turns is < 1 or > 128 || firstSeed > ulong.MaxValue - (ulong)(matches - 1))
            throw new ArgumentOutOfRangeException(nameof(matches), "Use 1..100 matches, 1..128 turns and nonwrapping seeds.");
        return new SimulationRunner(CreateEngine()).RunBatch(firstSeed, matches, turns);
    }
}
