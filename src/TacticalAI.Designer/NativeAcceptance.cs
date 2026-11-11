// File: NativeAcceptance.cs
// Purpose: Exercise the actual compiled native window and ordinary handlers in a fresh explicitly marked synthetic workspace.
// Public API: Explicit --acceptance-root PATH --phase 1|2 developer contract; never opens default user data.
// Variables: controlled file-dialog choices, actual UI completion events and literal independent game oracles.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using TacticalAI.Serialization;
using TacticalAI.Studio;

namespace TacticalAI.Designer;

/// <summary>QA data in the real application; no engine result, native window or storage operation is substituted.</summary>
internal static class NativeAcceptance
{
    internal const string Marker = "JasonStys/unity-native-acceptance-v1\n";
    private const string Duel = """
        {"formatVersion":1,"state":{"width":4,"height":2,"activeTeam":0,"turnNumber":1,
        "cells":[{"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0},
        {"isWalkable":true,"moveCost":1,"cover":0},{"isWalkable":true,"moveCost":1,"cover":0}],
        "units":[{"id":"BlueA","team":0,"x":0,"y":0,"hitPoints":100,"maxHitPoints":100,"cooldowns":{}},
        {"id":"RedA","team":1,"x":3,"y":0,"hitPoints":100,"maxHitPoints":100,"cooldowns":{}}]}}
        """;
    internal static int ExitCode { get; private set; } = 1;

    /// <summary>The launcher must create a new marker-only directory; reuse needs explicit second phase and first receipt.</summary>
    internal static int Run(string path, int phase)
    {
        string root = NativeFiles.DirectoryPath(path, forbidProgramTree: true);
        if (!System.IO.Directory.Exists(root) || NativeFiles.ReadText(Path.Combine(root, "acceptance.txt"), 4096) != Marker || phase is not (1 or 2))
            throw new InvalidDataException("A marked synthetic acceptance root and explicit phase are required.");
        if (phase == 1 && System.IO.Directory.EnumerateFileSystemEntries(root).Count() != 1)
            throw new InvalidDataException("First acceptance phase requires a new marker-only directory.");
        if (phase == 2 && !File.Exists(Path.Combine(root, "phase-1.json")))
            throw new InvalidDataException("Second phase requires the completed first native journey.");
        string receipt = Path.Combine(root, $"phase-{phase}.json");
        if (File.Exists(receipt)) throw new InvalidDataException("Acceptance receipts are never overwritten.");
        if (phase == 1) NativeBoundaryChecks.Run(root);
        var dialogs = new Choices();
        using var profile = new DesignerProfile(Path.Combine(root, "profile"));
        using var window = new DesignerWindow(profile, dialogs);
        window.Shown += async (_, _) =>
        {
            try
            {
                Require(window.Visible && IsWindowVisible(window.Handle), "Actual native window must be visible.");
                if (phase == 1) await First(window, profile, dialogs, root);
                else await Second(window, dialogs, root);
                NativeFiles.WriteNew(receipt, JsonSerializer.Serialize(new
                {
                    schema = 1,
                    passed = true,
                    product = "unity-tactical-ai-sandbox",
                    version = "1.1.0",
                    phase,
                    windowVisible = true,
                    acceptedCommands = window.Accepted.Count,
                    cursor = window.Accepted.Cursor,
                    title = window.Accepted.Title,
                    frameHash = window.Accepted.Frame.StateHash,
                    scope = "Actual compiled WinForms window/ordinary handlers and synthetic local profile; package/extraction identity is checked separately. No Unity Editor or clean-machine/signature claim.",
                }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
                ExitCode = 0;
                if (!window.IsDisposed) window.Close();
            }
            catch (Exception error)
            {
                ExitCode = 1;
                // The receipt contains only this explicitly synthetic journey's diagnostic, never default account data.
                if (!File.Exists(receipt)) NativeFiles.WriteNew(receipt, JsonSerializer.Serialize(new { schema = 1, passed = false, phase, error = error.ToString() }) + "\n");
                dialogs.ReleasePending();
                if (!window.IsDisposed) window.Close();
            }
        };
        Application.Run(window);
        return ExitCode;
    }

    /// <summary>Drive genuine visible handlers; expected positions/health/turns are literal arithmetic on this independent board.</summary>
    private static async Task First(DesignerWindow window, DesignerProfile profile, Choices dialogs, string root)
    {
        for (int index = 0; index < 3; index++)
        {
            Find<TabControl>(window, "DesignerTabs").SelectedIndex = 1;
            Find<ComboBox>(window, "PreparedInput").SelectedIndex = index;
            await Click(window, "Prepared");
            Require(window.Accepted.Count == 0 && window.Accepted.Frame.Units.Count >= 2, "Prepared input must be an actual original scenario.");
        }
        Find<TextBox>(window, "ProjectTitle").Text = "Designer literal duel";
        Find<TextBox>(window, "ScenarioJson").Text = Duel;
        await Click(window, "ApplyScenario");
        Require(window.Accepted.Inspection.Width == 4 && window.Accepted.Frame.Turn == 1, "Literal custom scenario was not applied.");
        string accepted = window.Accepted.ProjectJson();
        Find<TextBox>(window, "ScenarioJson").Text = "{ invalid";
        await Click(window, "ApplyScenario");
        Require(window.StatusText.StartsWith("Not applied:", StringComparison.Ordinal) && window.Accepted.ProjectJson() == accepted, "Invalid JSON must preserve accepted state.");
        await Click(window, "DiscardDraft");
        Find<TextBox>(window, "ScenarioJson").Text = Duel.Replace("\"width\":4", "\"width\":33", StringComparison.Ordinal);
        await Click(window, "ApplyScenario");
        Require(window.StatusText.StartsWith("Not applied:", StringComparison.Ordinal) && window.Accepted.ProjectJson() == accepted, "Valid JSON with out-of-bounds domain data must be rejected without losing the native window.");
        await Click(window, "DiscardDraft");
        Set(window, "CellX", 2); Set(window, "CellY", 1); Set(window, "MoveCost", 3); Set(window, "Cover", 20);
        await Click(window, "ApplyCell");
        Require(window.Accepted.Inspection.Cells[6].MoveCost == 3 && window.Accepted.Inspection.Cells[6].Cover == 20, "Terrain fields must reach the real engine.");
        Find<TabControl>(window, "DesignerTabs").SelectedIndex = 0;
        Find<ComboBox>(window, "Actor").SelectedItem = "BlueA";
        Find<ComboBox>(window, "ActionKind").SelectedItem = "Move";
        Set(window, "CellX", 3); Set(window, "CellY", 0);
        accepted = window.Accepted.ProjectJson();
        await Click(window, "ApplyCommand");
        Require(window.StatusText.StartsWith("REJECTED", StringComparison.Ordinal) && window.Accepted.ProjectJson() == accepted, "Occupied target refusal must not advance time.");
        Set(window, "CellX", 1);
        await Click(window, "ApplyCommand");
        Require(window.Accepted.Count == 1 && window.Accepted.Frame.Turn == 2 && window.Accepted.Frame.Units.Single(u => u.Id == "BlueA").X == 1, "Blue legal movement must advance one turn.");
        Find<ComboBox>(window, "Actor").SelectedItem = "RedA";
        Find<ComboBox>(window, "ActionKind").SelectedItem = "UseAbility";
        Find<ComboBox>(window, "Target").SelectedItem = "BlueA";
        Find<ComboBox>(window, "Ability").SelectedItem = "pulse-shot";
        await Click(window, "ApplyCommand");
        Require(window.Accepted.Count == 2 && window.Accepted.Frame.Turn == 3 && window.Accepted.Frame.Units.Single(u => u.Id == "BlueA").HitPoints == 66, "Real pulse damage must be 100 minus 34.");
        await Click(window, "SaveProject");
        string saved = NativeFiles.ReadText(Path.Combine(profile.Directory, "current.json"));
        await Click(window, "Previous");
        Require(window.Accepted.Cursor == 1 && window.Accepted.Count == 2, "History navigation must retain the complete recording.");
        accepted = window.Accepted.ProjectJson();
        await Click(window, "AdvanceBot");
        Require(window.StatusText.StartsWith("Not applied:", StringComparison.Ordinal) && window.Accepted.ProjectJson() == accepted, "Historical bot command must be rejected.");
        await Click(window, "SaveProject");
        Require(NativeFiles.ReadText(Path.Combine(profile.Directory, "previous.json")) == saved, "Saving must retain the actual previous revision.");
        Capture(window, Path.Combine(root, "native-designer.png"));
        foreach ((string button, string name) in new[] { ("ExportProject", "project.json"), ("ExportReplay", "replay.json"), ("ExportSave", "frame.json"), ("ExportInspection", "inspection.json"), ("ExportHtml", "review.html") })
        {
            dialogs.Outputs.Enqueue(Task.FromResult<string?>(Path.Combine(root, name)));
            await Click(window, button);
            Require(File.Exists(Path.Combine(root, name)), "The real export must create its selected new file.");
        }
        Require(DesignerSession.FromReplay(NativeFiles.ReadText(Path.Combine(root, "replay.json")), "independent export").Count == 2, "Exported replay must validate all commands and hashes.");
        string projectBytes = NativeFiles.ReadText(Path.Combine(root, "project.json"));
        dialogs.Outputs.Enqueue(Task.FromResult<string?>(Path.Combine(root, "project.json")));
        await Click(window, "ExportProject");
        Require(window.StatusText.StartsWith("Not applied:", StringComparison.Ordinal) && NativeFiles.ReadText(Path.Combine(root, "project.json")) == projectBytes, "Existing outputs must never be overwritten.");
        string invalid = Path.Combine(root, "invalid.json"); NativeFiles.WriteNew(invalid, "{ invalid");
        dialogs.Inputs.Enqueue(invalid); accepted = window.Accepted.ProjectJson();
        await Click(window, "OpenJson");
        Require(window.Accepted.ProjectJson() == accepted && window.StatusText.StartsWith("Not applied:", StringComparison.Ordinal), "Malformed imported file must preserve the complete accepted project.");
        Find<TabControl>(window, "DesignerTabs").SelectedIndex = 2;
        Set(window, "Matches", 5); Set(window, "MaximumTurns", 24); Find<TextBox>(window, "FirstSeed").Text = "1000";
        await Click(window, "Tournament");
        using (JsonDocument result = JsonDocument.Parse(Find<TextBox>(window, "TournamentResult").Text))
        {
            JsonElement summary = result.RootElement;
            Require(summary.GetProperty("matches").GetInt32() == 5, "Actual balance cohort denominator must be five.");
            Require(summary.GetProperty("blueWins").GetInt32() + summary.GetProperty("redWins").GetInt32() + summary.GetProperty("draws").GetInt32() == 5, "All actual matches must be accounted for.");
            Require(summary.GetProperty("results").EnumerateArray().All(row => row.GetProperty("commands").GetInt32() <= 24), "Every actual match must retain the chosen action cap.");
        }
        dialogs.Outputs.Enqueue(Task.FromResult<string?>(Path.Combine(root, "balance.csv")));
        await Click(window, "ExportCsv");
        string[] rows = NativeFiles.ReadText(Path.Combine(root, "balance.csv")).Trim().Split('\n');
        Require(rows.Length == 6 && rows[0] == "seed,winner,turns,commands,damage,final_hash", "Exported cohort must contain the actual five rows and expected schema.");
        for (int index = 1; index <= 5; index++) Require(rows[index].StartsWith((999 + index).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal), "Exported seeds must be consecutive 1000..1004.");
        Capture(window, Path.Combine(root, "native-balance.png"));
        // The actual asynchronous file-choice operation remains pending while repeated normal exit paths retain ownership.
        dialogs.HoldOutput();
        var completed = Completion(window);
        Find<Button>(window, "ExportProject").PerformClick();
        Require(window.WorkPending, "Real export handler must remain pending while its file choice is unresolved.");
        Find<Button>(window, "Quit").PerformClick(); window.Close();
        Require(window.Visible && window.WorkPending && !window.IsDisposed, "Repeated Quit and Close must retain the actual visible owner.");
        bool refused = false;
        try { using var duplicate = new DesignerProfile(profile.Directory); }
        catch (IOException) { refused = true; }
        Require(refused, "Pending exit must retain the actual profile lock.");
        dialogs.ReleasePending();
        await completed.WaitAsync(TimeSpan.FromSeconds(20));
    }

    /// <summary>A separate process must open the saved complete replay and selected frame using the ordinary profile constructor.</summary>
    private static async Task Second(DesignerWindow window, Choices dialogs, string root)
    {
        Require(window.Accepted.Title == "Designer literal duel" && window.Accepted.Count == 2 && window.Accepted.Cursor == 1 && window.Accepted.Frame.Turn == 2, "Second process must recover actual title, history and selected frame.");
        await Click(window, "Next");
        Require(window.Accepted.Cursor == 2 && window.Accepted.Frame.Units.Single(u => u.Id == "BlueA").HitPoints == 66, "Recovered final frame must retain literal damage.");
        dialogs.Inputs.Enqueue(Path.Combine(root, "replay.json"));
        await Click(window, "OpenJson");
        Require(window.Accepted.Count == 2 && window.Accepted.Cursor == 2, "The exported original replay must also be importable through the actual UI.");
        await Click(window, "LoadPrevious");
        Require(window.Accepted.Cursor == 2 && window.Accepted.Title == "Designer literal duel", "Previous revision must remain independently accessible.");
        Capture(window, Path.Combine(root, "native-restarted.png"));
    }

    private static Task Completion(DesignerWindow window)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) => { window.OperationCompleted -= handler; completion.TrySetResult(); };
        window.OperationCompleted += handler;
        return completion.Task;
    }

    private static async Task Click(DesignerWindow window, string name)
    {
        Button button = Find<Button>(window, name);
        for (Control? parent = button.Parent; parent is not null; parent = parent.Parent)
            if (parent is TabPage page && page.Parent is TabControl tabs) tabs.SelectedTab = page;
        Require(button.Enabled && button.Visible, "The real visible control must be enabled: " + name);
        Task completed = Completion(window); button.PerformClick();
        await completed.WaitAsync(TimeSpan.FromSeconds(30));
        Require(!window.WorkPending, "UI completion must follow actual operation completion.");
    }

    private static T Find<T>(DesignerWindow window, string name) where T : Control =>
        window.Controls.Find(name, true).SingleOrDefault() as T ?? throw new InvalidDataException("Missing actual native control: " + name);
    private static void Set(DesignerWindow window, string name, int value) => Find<NumericUpDown>(window, name).Value = value;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static void Capture(DesignerWindow window, string path)
    {
        window.Refresh();
        using var image = new Bitmap(window.Width, window.Height);
        window.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        image.Save(output, ImageFormat.Png); output.Flush(true);
    }

    private sealed class Choices : IDesignerDialogs
    {
        internal readonly Queue<string?> Inputs = new();
        internal readonly Queue<Task<string?>> Outputs = new();
        private TaskCompletionSource<string?>? pending;
        public Task<string?> InputAsync(IWin32Window owner) => Task.FromResult(Inputs.Count == 0 ? throw new InvalidOperationException("Unexpected input dialog.") : Inputs.Dequeue());
        public Task<string?> OutputAsync(IWin32Window owner, string filename) => Outputs.Count == 0 ? throw new InvalidOperationException("Unexpected output dialog.") : Outputs.Dequeue();
        public bool Discard(IWin32Window owner, string message) => true;
        internal void HoldOutput() { pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Outputs.Enqueue(pending.Task); }
        internal void ReleasePending() => pending?.TrySetResult(null);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
}
