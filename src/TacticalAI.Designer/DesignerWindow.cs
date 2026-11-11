// File: DesignerWindow.cs
// Purpose: Present a complete game-scenario editor, actual turn controls, replay inspection and seeded balancing.
// Public API: DesignerWindow owns only its local profile; native dialogs select explicit input/new export paths.
// Variables: accepted session, unapplied draft, retained task and quit intent are separate state.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Globalization;
using System.Text.Json;
using TacticalAI.Core;
using TacticalAI.Serialization;
using TacticalAI.Studio;

namespace TacticalAI.Designer;

/// <summary>Native file/confirmation boundary; the ordinary app always uses visible OS dialogs.</summary>
public interface IDesignerDialogs
{
    Task<string?> InputAsync(IWin32Window owner);
    Task<string?> OutputAsync(IWin32Window owner, string filename);
    bool Discard(IWin32Window owner, string message);
}

/// <summary>Local user choices only; no automatic external browser, network or filesystem reset.</summary>
public sealed class NativeDialogs : IDesignerDialogs
{
    public Task<string?> InputAsync(IWin32Window owner)
    {
        using var dialog = new OpenFileDialog { Filter = "Game JSON (*.json)|*.json", CheckFileExists = true, Multiselect = false };
        return Task.FromResult(dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null);
    }

    public Task<string?> OutputAsync(IWin32Window owner, string filename)
    {
        using var dialog = new SaveFileDialog { FileName = filename, OverwritePrompt = true, AddExtension = true };
        return Task.FromResult(dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null);
    }

    public bool Discard(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "Unsaved designer work", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}

/// <summary>Fictional turn-based game designer; no real-world targeting, control or Unity rendering claim.</summary>
public sealed class DesignerWindow : Form
{
    private readonly DesignerProfile profile;
    private readonly IDesignerDialogs dialogs;
    private DesignerSession session = DesignerSession.Prepared(42);
    private string? savedProject;
    private bool rendering, draftDirty, busy, quitRequested, recoveryBlocked;
    private readonly List<Control> editing = new();
    private readonly Label status = new() { AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(8), Text = "Opening owner-local workspace…" };
    private readonly Label summary = new() { AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly TextBox title = new() { Text = "Seeded encounter 42", Width = 230, MaxLength = 100 };
    private readonly TextBox draft = new() { Multiline = true, AcceptsTab = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, MaxLength = DesignerSession.MaximumInputBytes, Font = new Font("Consolas", 10) };
    private readonly TextBox details = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
    private readonly ComboBox actors = Choice(), targets = Choice(), abilities = Choice(), kinds = Choice(), prepared = Choice();
    private readonly NumericUpDown x = Number(0, 31, 0), y = Number(0, 31, 0), cost = Number(1, 100, 1), cover = Number(0, 100, 0);
    private readonly NumericUpDown budget = Number(1, 256, 64), matches = Number(1, 100, 10), turns = Number(1, 128, 80);
    private readonly TextBox seed = new() { Width = 140, Text = "1000", MaxLength = 20 };
    private readonly CheckBox walkable = new() { Text = "Walkable", Checked = true, AutoSize = true };
    private readonly TrackBar cursor = new() { Minimum = 0, Maximum = 0, TickStyle = TickStyle.None, Dock = DockStyle.Fill };
    private readonly BoardPanel board = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(239, 244, 249), MinimumSize = new Size(300, 300) };
    private readonly DataGridView units = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
    private SimulationSummary? tournament;

    public DesignerWindow(DesignerProfile profile, IDesignerDialogs? dialogs = null)
    {
        this.profile = profile;
        this.dialogs = dialogs ?? new NativeDialogs();
        Text = "Unity Tactical AI Designer | Jason Stys | 1.1.0";
        Name = "TacticalDesigner";
        MinimumSize = new Size(1120, 740);
        Size = new Size(1400, 940);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        foreach ((Control control, string name) in new (Control, string)[] {
            (title, "ProjectTitle"), (draft, "ScenarioJson"), (details, "FrameEvidence"),
            (actors, "Actor"), (targets, "Target"), (abilities, "Ability"), (kinds, "ActionKind"),
            (prepared, "PreparedInput"), (x, "CellX"), (y, "CellY"), (walkable, "Walkable"),
            (cost, "MoveCost"), (cover, "Cover"), (budget, "BotBudget"), (matches, "Matches"),
            (turns, "MaximumTurns"), (seed, "FirstSeed"), (cursor, "FrameCursor"), (units, "Avatars"),
        }) control.Name = name;
        BuildLayout();
        try
        {
            DesignerSession? loaded = profile.Load();
            if (loaded is not null) { session = loaded; savedProject = loaded.ProjectJson(); }
            status.Text = loaded is null ? "New workspace. Choose a prepared encounter or edit a complete scenario, then Save project." : "Stored project opened and every recorded command/hash validated.";
        }
        catch (Exception error) when (Expected(error))
        {
            recoveryBlocked = true;
            status.Text = "Current project was not replaced. Load previous for inspection/export, then use a new empty workspace for recovery. " + error.Message;
        }
        ResetDraft(replaceTitle: true);
        RefreshView();
        FormClosing += ClosingOwner;
    }

    public bool WorkPending => busy;
    public string StatusText => status.Text;
    public DesignerSession Accepted => session;
    /// <summary>Signals completion after actual accepted work and UI refresh, including rejected input.</summary>
    public event EventHandler? OperationCompleted;

    private static ComboBox Choice() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private static NumericUpDown Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 70 };
    private static Label TextLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(5, 9, 4, 3) };
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(5) };

    private Button Button(string text, string name, Func<Task> action)
    {
        var button = new Button { Text = text, Name = name, AutoSize = true, Padding = new Padding(5), Margin = new Padding(4) };
        button.Click += async (_, _) => await Run(action);
        editing.Add(button);
        return button;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.Controls.Add(new Label { Text = "TACTICAL AI  /  DESIGN + REPLAY     1.1.0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = Color.FromArgb(21, 48, 80), Padding = new Padding(12) }, 0, 0);
        var top = Flow();
        top.Controls.Add(TextLabel("Project")); top.Controls.Add(title);
        top.Controls.Add(Button("Apply title", "ApplyTitle", () => { session.SetTitle(title.Text); status.Text = "Project title applied; Save project retains it on disk."; return Task.CompletedTask; }));
        top.Controls.Add(Button("Save project", "SaveProject", async () => { if (draftDirty) throw new InvalidOperationException("Apply or discard the unapplied JSON draft first."); session.SetTitle(title.Text); await Task.Run(() => profile.Save(session)); savedProject = session.ProjectJson(); status.Text = "Complete validated project saved; previous revision retained."; }));
        top.Controls.Add(Button("Open JSON…", "OpenJson", Import));
        top.Controls.Add(Button("Load previous", "LoadPrevious", async () => { RequireReplace(); session = await Task.Run(profile.LoadPrevious); ResetDraft(replaceTitle: true); status.Text = "Previous revision loaded for review; no disk state was changed."; }));
        top.Controls.Add(Button("Export project…", "ExportProject", () => Export("project")));
        var help = Button("Help", "Help", () => { ShowHelp(); return Task.CompletedTask; }); top.Controls.Add(help);
        var quit = new Button { Text = "Exit application", Name = "Quit", AutoSize = true, Padding = new Padding(5), Margin = new Padding(4) };
        quit.Click += (_, _) => Close(); top.Controls.Add(quit);
        root.Controls.Add(top, 0, 1);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1300, 700), SplitterDistance = 690, Panel1MinSize = 450, Panel2MinSize = 400 };
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        left.Controls.Add(summary, 0, 0); left.Controls.Add(board, 0, 1);
        var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.Controls.Add(Button("Previous", "Previous", () => Navigate(-1)), 0, 0); nav.Controls.Add(cursor, 1, 0); nav.Controls.Add(Button("Next / tail", "Next", () => Navigate(1)), 2, 0);
        cursor.Scroll += (_, _) => { if (!busy && !draftDirty) { session.SetCursor(cursor.Value); RefreshView(); } else cursor.Value = session.Cursor; };
        editing.Add(cursor); left.Controls.Add(nav, 0, 2); left.Controls.Add(units, 0, 3);
        foreach (string name in new[] { "Avatar", "Team", "X", "Y", "HP", "Cooldowns" }) units.Columns.Add(name, name);
        split.Panel1.Controls.Add(left);
        var tabs = new TabControl { Dock = DockStyle.Fill, Name = "DesignerTabs" };
        TabPage play = new("Commands + evidence"), edit = new("Scenario editor"), batch = new("Seeded balance");
        play.Controls.Add(BuildPlay()); edit.Controls.Add(BuildEditor()); batch.Controls.Add(BuildBatch()); tabs.TabPages.AddRange(new[] { play, edit, batch });
        split.Panel2.Controls.Add(tabs); root.Controls.Add(split, 0, 2); root.Controls.Add(status, 0, 3);
        root.Controls.Add(new Label { Text = "Made by Jason Stys  •  github.com/JasonStys  |  Fictional game only. Owner-local plaintext data; no live control or Unity PlayMode claim.", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 4, 0), ForeColor = Color.FromArgb(54, 65, 80) }, 0, 4);
        Controls.Add(root);
        board.Paint += DrawBoard;
        board.MouseClick += SelectCell;
        editing.AddRange(new Control[] { title, draft, actors, targets, abilities, kinds, prepared, x, y, cost, cover, walkable, budget, matches, turns, seed });
        draft.TextChanged += (_, _) => { if (!rendering) { draftDirty = true; status.Text = "Unapplied scenario JSON. Apply starts a new history; Discard draft returns to accepted state."; } };
    }

    private Control BuildPlay()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var commands = Flow();
        kinds.Items.AddRange(new object[] { "Move", "UseAbility", "Wait" }); kinds.SelectedIndex = 0;
        abilities.Items.AddRange(new object[] { "pulse-shot", "close-burst" }); abilities.SelectedIndex = 0;
        var cellChoice = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        cellChoice.Controls.AddRange(new Control[] { TextLabel("X"), x, TextLabel("Y"), y });
        commands.Controls.AddRange(new Control[] { TextLabel("Actor"), actors, TextLabel("Action"), kinds, cellChoice, TextLabel("Opponent"), targets, TextLabel("Ability"), abilities });
        commands.Controls.Add(Button("Apply legal command", "ApplyCommand", () => { RequireApplied(); var command = new ActionCommand(Enum.Parse<ActionKind>((string)kinds.SelectedItem!), actors.Text, new GridPosition((int)x.Value, (int)y.Value), targets.Text, abilities.Text); ActionResult r = session.Append(command); status.Text = r.Succeeded ? string.Join(" | ", r.Events.Select(e => e.Message)) : "REJECTED; turn and saved state unchanged: " + r.Error; return Task.CompletedTask; }));
        commands.Controls.Add(TextLabel("Bot budget")); commands.Controls.Add(budget);
        commands.Controls.Add(Button("Advance default bot", "AdvanceBot", () => { RequireApplied(); UtilityDecision d = session.AdvanceDefaultBot((int)budget.Value); status.Text = $"Executed original default agent: {d.Explanation}; candidates {d.CandidatesEvaluated}; budget exhausted {d.BudgetExhausted}."; return Task.CompletedTask; }));
        panel.Controls.Add(commands, 0, 0);
        var exports = Flow();
        exports.Controls.Add(Button("Replay JSON…", "ExportReplay", () => Export("replay"))); exports.Controls.Add(Button("Review HTML…", "ExportHtml", () => Export("html"))); exports.Controls.Add(Button("Frame save JSON…", "ExportSave", () => Export("save"))); exports.Controls.Add(Button("Inspection JSON…", "ExportInspection", () => Export("inspection")));
        panel.Controls.Add(exports, 0, 1); panel.Controls.Add(details, 0, 2);
        return panel;
    }

    private Control BuildEditor()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var choices = Flow();
        prepared.Items.AddRange(new object[] { "Open encounter / 42", "Cover encounter / 7", "Alternate encounter / 99" }); prepared.SelectedIndex = 0;
        choices.Controls.Add(prepared);
        choices.Controls.Add(Button("Use prepared input", "Prepared", () => { RequireReplace(); session = DesignerSession.Prepared(prepared.SelectedIndex switch { 1 => 7UL, 2 => 99UL, _ => 42UL }); ResetDraft(replaceTitle: true); status.Text = "Prepared data opened inside the full designer; Save project is explicit."; return Task.CompletedTask; }));
        choices.Controls.Add(Button("Apply scenario JSON", "ApplyScenario", () => { var next = DesignerSession.FromSave(draft.Text, title.Text); if (session.Count > 0 && !dialogs.Discard(this, "Applying this scenario starts a new history. Discard the accepted recorded commands? Export or save them first if needed.")) throw new InvalidOperationException("Scenario replacement canceled."); session = next; ResetDraft(); status.Text = "Accepted complete scenario; started a new empty command history. Save is explicit."; return Task.CompletedTask; }));
        choices.Controls.Add(Button("Discard draft", "DiscardDraft", () => { ResetDraft(); status.Text = "Draft discarded; accepted history and stored files unchanged."; return Task.CompletedTask; }));
        choices.Controls.Add(Button("Copy selected frame", "CopyFrame", () => { if (draftDirty && !dialogs.Discard(this, "Replace the unapplied draft with the selected frame?")) throw new InvalidOperationException("Draft copy canceled."); ResetDraft(); status.Text = "Selected-frame save copied to the editor. Applying it deliberately starts a new history."; return Task.CompletedTask; }));
        panel.Controls.Add(choices, 0, 0);
        var terrain = Flow(); terrain.Controls.Add(TextLabel("Click a board cell; edit initial terrain only")); terrain.Controls.Add(walkable); terrain.Controls.Add(TextLabel("Cost")); terrain.Controls.Add(cost); terrain.Controls.Add(TextLabel("Cover%")); terrain.Controls.Add(cover);
        terrain.Controls.Add(Button("Apply cell", "ApplyCell", () => { RequireApplied(); session.EditTerrain((int)x.Value, (int)y.Value, walkable.Checked, (int)cost.Value, (int)cover.Value); ResetDraft(); status.Text = "Initial terrain changed with original state validation; avatar cells cannot become walls."; return Task.CompletedTask; }));
        panel.Controls.Add(terrain, 0, 1); panel.Controls.Add(draft, 0, 2); return panel;
    }

    private Control BuildBatch()
    {
        var panel = Flow();
        panel.Controls.Add(new Label { Text = "Runs ORIGINAL generated encounters for consecutive seeds, not the edited board. The cap limits executed actions per match. Original turn counters start at 1; CSV commands are the executed action count. Observed results are not forecasts.", AutoSize = true, MaximumSize = new Size(540, 0), Padding = new Padding(5) });
        panel.Controls.AddRange(new Control[] { TextLabel("First seed"), seed, TextLabel("Matches"), matches, TextLabel("Action cap/match"), turns });
        var result = new TextBox { Name = "TournamentResult", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 520, Height = 320, Font = new Font("Consolas", 10) };
        panel.Controls.Add(Button("Run seeded tournament", "Tournament", async () => { RequireApplied(); if (!ulong.TryParse(seed.Text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong first)) throw new InvalidDataException("Seed must be an unsigned integer."); int count = (int)matches.Value, maximumTurns = (int)turns.Value; SimulationSummary next = await Task.Run(() => DesignerSession.Tournament(first, count, maximumTurns)); tournament = next; result.Text = JsonCodec.SerializeTelemetry(next, first); status.Text = $"Actual original tournament complete: {next.Matches} matches; {next.BlueWins} Blue + {next.RedWins} Red + {next.Draws} draws."; }));
        panel.Controls.Add(Button("Export tournament CSV…", "ExportCsv", async () => { if (tournament is null) throw new InvalidOperationException("Run a tournament first."); string text = JsonCodec.SerializeTelemetryCsv(tournament); string? path = await dialogs.OutputAsync(this, "game-balance.csv"); if (path is not null) { await Task.Run(() => NativeFiles.WriteNew(path, text)); status.Text = "Actual completed cohort CSV exported to a new file."; } }));
        panel.Controls.Add(result); return panel;
    }

    private async Task Import()
    {
        RequireReplace(); string? path = await dialogs.InputAsync(this); if (path is null) return;
        DesignerSession next = await Task.Run(() => { string text = NativeFiles.ReadText(path); using var doc = JsonDocument.Parse(text); return doc.RootElement.TryGetProperty("schemaVersion", out _) ? DesignerSession.FromProject(text) : doc.RootElement.TryGetProperty("steps", out _) ? DesignerSession.FromReplay(text, "Imported replay") : DesignerSession.FromSave(text, "Imported scenario"); });
        session = next; ResetDraft(replaceTitle: true); status.Text = "Imported complete validated input; saved project was not silently replaced.";
    }

    private async Task Export(string kind)
    {
        RequireApplied();
        string content = kind switch { "project" => session.ProjectJson(), "replay" => session.ReplayJson(), "save" => session.SaveJson(), "inspection" => session.InspectionJson(), "html" => session.InspectionHtml(), _ => throw new InvalidOperationException("Unknown export") };
        string? path = await dialogs.OutputAsync(this, "game-" + kind + (kind == "html" ? ".html" : ".json"));
        if (path is null) { status.Text = "Export canceled; accepted/saved project unchanged."; return; }
        await Task.Run(() => NativeFiles.WriteNew(path, content)); status.Text = "Exact " + kind + " exported to a new file; existing files are never overwritten.";
    }

    private Task Navigate(int delta) { RequireApplied(); session.SetCursor(Math.Clamp(session.Cursor + delta, 0, session.Count)); return Task.CompletedTask; }
    private void RequireApplied() { if (draftDirty) throw new InvalidOperationException("Apply or discard the unapplied JSON draft first."); if (title.Text != session.Title) throw new InvalidOperationException("Apply title or Save project before using changed project metadata."); }
    private void RequireReplace() { if ((draftDirty || session.ProjectJson() != savedProject || title.Text != session.Title) && !dialogs.Discard(this, "Replace unsaved accepted work and any unapplied scenario JSON? Stored files will not change; export/save first if needed.")) throw new InvalidOperationException("Input replacement canceled."); }
    private static bool Expected(Exception error) => error is ArgumentException or InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException or System.Text.DecoderFallbackException;

    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; SetEditing();
        try { await action(); }
        catch (Exception error) when (Expected(error)) { status.Text = "Not applied: " + error.Message; }
        finally
        {
            busy = false; RefreshView(); SetEditing();
            OperationCompleted?.Invoke(this, EventArgs.Empty);
            if (quitRequested) BeginInvoke(Close);
        }
    }

    private void SetEditing()
    {
        foreach (Control control in editing) control.Enabled = !busy;
        Control? save = Controls.Find("SaveProject", true).SingleOrDefault(); if (save is not null) save.Enabled = !busy && !recoveryBlocked;
    }

    private void ResetDraft(bool replaceTitle = false)
    {
        rendering = true; if (replaceTitle) title.Text = session.Title; draft.Text = session.SaveJson(); draftDirty = false; rendering = false;
    }

    private void RefreshView()
    {
        InspectionFrame frame = session.Frame;
        summary.Text = $"{session.Title}  |  frame {session.Cursor}/{session.Count}  |  turn {frame.Turn} / {frame.ActiveTeam}  |  {(frame.Winner is null ? "in progress" : "winner " + frame.Winner)}\n{(session.ProjectJson() == savedProject ? "Saved project" : "Unsaved accepted work")}  •  Blue=B / Red=R; exact identifiers and health below";
        cursor.Maximum = session.Count; cursor.Value = session.Cursor;
        string actor = actors.Text, target = targets.Text; actors.Items.Clear(); targets.Items.Clear();
        foreach (InspectionUnit unit in frame.Units.Where(u => u.HitPoints > 0)) { if (unit.Team == frame.ActiveTeam) actors.Items.Add(unit.Id); else targets.Items.Add(unit.Id); }
        actors.SelectedIndex = actors.Items.Count == 0 ? -1 : Math.Max(0, actors.Items.IndexOf(actor)); targets.SelectedIndex = targets.Items.Count == 0 ? -1 : Math.Max(0, targets.Items.IndexOf(target));
        x.Maximum = session.Inspection.Width - 1; y.Maximum = session.Inspection.Height - 1;
        units.Rows.Clear(); foreach (InspectionUnit unit in frame.Units) units.Rows.Add(unit.Id, unit.Team, unit.X, unit.Y, $"{unit.HitPoints}/{unit.MaxHitPoints}", string.Join(", ", unit.Cooldowns.Select(c => c.Key + "=" + c.Value)));
        details.Text = $"Validated SHA256\r\n{frame.StateHash}\r\n\r\nRecorded previous command: {JsonSerializer.Serialize(frame.PreviousCommand)}\r\nRecorded NEXT action is hindsight: {JsonSerializer.Serialize(frame.NextRecordedCommand)}\r\n\r\nIndependently recomputed default-bot diagnostic (not the recording policy):\r\n{JsonSerializer.Serialize(frame.DefaultBot)}\r\n\r\nTerrain-only pair visibility, not attack/range eligibility:\r\n{string.Join("\r\n", frame.Sight.Select(s => s.First + " / " + s.Second + ": " + (s.Clear ? "clear" : "blocked")))}";
        board.Invalidate();
    }

    private void DrawBoard(object? sender, PaintEventArgs e)
    {
        int width = session.Inspection.Width, height = session.Inspection.Height;
        float cell = Math.Min((board.Width - 42f) / width, (board.Height - 70f) / height);
        if (cell <= 0) return;
        using var grid = new Pen(Color.FromArgb(180, 192, 207));
        using var font = new Font("Segoe UI", Math.Clamp(cell / 3, 7, 13), FontStyle.Bold);
        for (int row = 0; row < height; row++) for (int column = 0; column < width; column++)
        {
            TerrainCell terrain = session.Inspection.Cells[(row * width) + column];
            var rectangle = new RectangleF(24 + column * cell, 16 + (height - row - 1) * cell, cell, cell);
            using var fill = new SolidBrush(!terrain.IsWalkable ? Color.FromArgb(73, 83, 98) : terrain.Cover > 0 ? Color.FromArgb(192, 219, 196) : Color.White);
            e.Graphics.FillRectangle(fill, rectangle); e.Graphics.DrawRectangle(grid, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
        foreach ((InspectionUnit unit, int index) in session.Frame.Units.Select((u, i) => (u, i)))
        {
            if (unit.HitPoints == 0 && session.Frame.Units.Any(other => other.HitPoints > 0 && other.X == unit.X && other.Y == unit.Y)) continue;
            var rectangle = new RectangleF(27 + unit.X * cell, 19 + (height - unit.Y - 1) * cell, cell - 6, cell - 6);
            using var fill = new SolidBrush(unit.HitPoints == 0 ? Color.Gray : unit.Team == "Blue" ? Color.FromArgb(20, 75, 137) : Color.FromArgb(165, 44, 54));
            using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.FillEllipse(fill, rectangle); e.Graphics.DrawString((unit.Team == "Blue" ? "B" : "R") + (index + 1), font, Brushes.White, rectangle, centered);
        }
        e.Graphics.DrawString("X →    Y ↑    # wall / green cover; click to inspect/edit", Font, Brushes.Black, 20, 20 + height * cell);
    }

    private void SelectCell(object? sender, MouseEventArgs e)
    {
        if (busy) return;
        int width = session.Inspection.Width, height = session.Inspection.Height;
        float cell = Math.Min((board.Width - 42f) / width, (board.Height - 70f) / height);
        int column = (int)Math.Floor((e.X - 24) / cell), row = height - 1 - (int)Math.Floor((e.Y - 16) / cell);
        if (column < 0 || row < 0 || column >= width || row >= height) return;
        x.Value = column; y.Value = row;
        TerrainCell terrain = session.Inspection.Cells[row * width + column]; walkable.Checked = terrain.IsWalkable; cost.Value = terrain.MoveCost; cover.Value = terrain.Cover;
        status.Text = $"Cell({column},{row}) walkable={terrain.IsWalkable}, cost={terrain.MoveCost}, cover={terrain.Cover}%. Terrain edits apply only before any command.";
    }

    private void ClosingOwner(object? sender, FormClosingEventArgs e)
    {
        if (busy) { e.Cancel = true; quitRequested = true; status.Text = "Exit requested. Accepted work is still pending; this visible owner and project lock remain until it completes."; return; }
        quitRequested = false;
        if ((draftDirty || session.ProjectJson() != savedProject || title.Text != session.Title) && !dialogs.Discard(this, "There is unsaved accepted work or unapplied input. Discard it and exit? Choose No, then Save project to retain accepted changes.")) e.Cancel = true;
    }

    private void ShowHelp() => MessageBox.Show(this,
        "1. Prepared inputs are ordinary game scenarios inside this full app. Scenario JSON edits are drafts until Apply; applying starts a new history.\n\n2. Click cells to inspect terrain. Terrain edits require an empty history. Select an active-team avatar and a legal command, or advance the actual default bot. Rejected commands do not advance time.\n\n3. Previous/Next inspect validated stored hashes. Return to the tail before issuing new commands. Next recorded actions are hindsight; default-bot suggestions are separately recomputed.\n\n4. Save stores the complete project/cursor and retains previous revision. Imports and exports never replace saved state silently. New-file exports refuse existing names. If current storage is invalid, inspect/export previous and recover into a new empty profile; no automatic reset occurs.\n\n5. Seeded balance uses original generated encounters, not the edited board. Counts and bounds are explicit.\n\nOwner-local plaintext. Keep profiles/exports private and outside the app. No network, real-world targeting/control, Unity Editor, PlayMode or signing certification is implied. Made by Jason Stys: github.com/JasonStys.",
        "Using Unity Tactical AI Designer", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private sealed class BoardPanel : Panel
    {
        public BoardPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    }
}
