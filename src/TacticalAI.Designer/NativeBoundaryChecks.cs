// File: NativeBoundaryChecks.cs
// Purpose: Prove real Windows profile/export alias and recovery refusals using only a marked native acceptance root.
// Public API: Internal developer acceptance checks; no default data, installer or external provider access.
// Variables: literal sentinels, actual NTFS links and retained current/previous bytes form independent refusal oracles.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Diagnostics;
using TacticalAI.Studio;

namespace TacticalAI.Designer;

internal static class NativeBoundaryChecks
{
    /// <summary>Each failure must precede any replacement of the owned external sentinel or existing profile.</summary>
    internal static void Run(string acceptanceRoot)
    {
        string root = Path.Combine(acceptanceRoot, "boundary-checks");
        System.IO.Directory.CreateDirectory(root);
        foreach (string remote in new[] { "\\\\qa.invalid\\share\\missing", "\\\\?\\C:\\unsupported-device-path" })
        {
            Refuse(() => NativeFiles.DirectoryPath(remote));
            Refuse(() => NativeFiles.ReadText(remote));
        }
        Refuse(() => NativeFiles.WriteNew(Path.Combine(root, "retained.json:alternate"), "must not write"));
        string unknown = Path.Combine(root, "unknown");
        System.IO.Directory.CreateDirectory(unknown);
        string sentinel = Path.Combine(unknown, "retained.txt"); NativeFiles.WriteNew(sentinel, "retained unknown data");
        Refuse(() => { using var profile = new DesignerProfile(unknown); });
        Check(NativeFiles.ReadText(sentinel) == "retained unknown data" && System.IO.Directory.GetFileSystemEntries(unknown).Length == 1, "Unknown directories must remain unchanged.");
        string forbidden = Path.Combine(AppContext.BaseDirectory, "forbidden-acceptance-profile-" + Guid.NewGuid().ToString("N"));
        Refuse(() => { using var profile = new DesignerProfile(forbidden); });
        Check(!System.IO.Directory.Exists(forbidden), "Program-tree refusal must precede creation.");
        foreach (string member in new[] { "owner.txt", "owner.lock", "current.json", "previous.json" })
        {
            string directory = Path.Combine(root, "hardlink-" + member.Replace('.', '-'));
            using (var initial = new DesignerProfile(directory)) { }
            string target = Path.Combine(directory, member);
            if (File.Exists(target)) File.Delete(target); // Only the fixture's just-created ordinary member.
            string outside = Path.Combine(root, "outside-" + member.Replace('.', '-'));
            NativeFiles.WriteNew(outside, "outside sentinel " + member);
            Link("/H", target, outside);
            Refuse(() => { using var rejected = new DesignerProfile(directory); });
            Check(File.ReadAllText(outside) == "outside sentinel " + member, "Actual hardlink refusal must preserve outside bytes.");
        }
        string junctionTarget = Path.Combine(root, "junction-target");
        System.IO.Directory.CreateDirectory(junctionTarget);
        NativeFiles.WriteNew(Path.Combine(junctionTarget, "sentinel.txt"), "junction retained");
        string junction = Path.Combine(root, "junction-alias");
        Link("/J", junction, junctionTarget);
        Refuse(() => { using var rejected = new DesignerProfile(Path.Combine(junction, "new-profile")); });
        Check(!System.IO.Directory.Exists(Path.Combine(junctionTarget, "new-profile")) && File.ReadAllText(Path.Combine(junctionTarget, "sentinel.txt")) == "junction retained", "Ancestor junction refusal must precede writes to target.");
        string wrong = Path.Combine(root, "wrong-marker");
        using (var initial = new DesignerProfile(wrong)) { }
        File.WriteAllText(Path.Combine(wrong, "owner.txt"), "another application");
        Refuse(() => { using var rejected = new DesignerProfile(wrong); });
        Check(File.ReadAllText(Path.Combine(wrong, "owner.txt")) == "another application", "Wrong markers must not be reset.");
        string store = Path.Combine(root, "recovery");
        DesignerSession session = DesignerSession.Prepared(42);
        string first;
        using (var owner = new DesignerProfile(store))
        {
            owner.Save(session); first = NativeFiles.ReadText(Path.Combine(store, "current.json"));
            session.SetTitle("Retained second revision"); owner.Save(session);
            Check(NativeFiles.ReadText(Path.Combine(store, "previous.json")) == first, "Previous must contain the exact first project.");
            string altered = "{ corrupt external edit";
            File.WriteAllText(Path.Combine(store, "current.json"), altered);
            Refuse(() => owner.Save(session));
            Check(File.ReadAllText(Path.Combine(store, "current.json")) == altered && NativeFiles.ReadText(Path.Combine(store, "previous.json")) == first, "Outside change must preserve both revisions.");
        }
        using (var reopened = new DesignerProfile(store))
        {
            Refuse(() => reopened.Load());
            Check(reopened.LoadPrevious().ProjectJson() == first, "Previous revision remains independently recoverable.");
            Refuse(() => reopened.Save(session));
            Check(File.ReadAllText(Path.Combine(store, "current.json")) == "{ corrupt external edit", "Corrupt current must not be silently reset.");
        }
        string export = Path.Combine(root, "existing-export.json"); NativeFiles.WriteNew(export, "existing input");
        Refuse(() => NativeFiles.WriteNew(export, "replacement"));
        Check(NativeFiles.ReadText(export) == "existing input", "Export overwrite refusal must preserve the original.");
        string invalidText = Path.Combine(root, "invalid-utf8.json"); File.WriteAllBytes(invalidText, new byte[] { 0xff, 0xfe, 0xff });
        bool invalidUtf8 = false;
        try { NativeFiles.ReadText(invalidText); } catch (System.Text.DecoderFallbackException) { invalidUtf8 = true; }
        Check(invalidUtf8, "Input decoding must be strict UTF-8.");
        string oversized = Path.Combine(root, "oversized.json"); File.WriteAllBytes(oversized, new byte[1_048_577]);
        Refuse(() => NativeFiles.ReadText(oversized));
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static void Refuse(Action action)
    {
        bool refused = false;
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { refused = true; }
        Check(refused, "An unsafe existing path was accepted.");
    }

    /// <summary>Only fixed Windows link builtin and generated owned fixture paths; no user-provided command.</summary>
    private static void Link(string kind, string alias, string target)
    {
        if (kind is not ("/H" or "/J")) throw new ArgumentOutOfRangeException(nameof(kind));
        var options = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "/d", "/c", "mklink", kind, alias, target }) options.ArgumentList.Add(argument);
        using var child = Process.Start(options) ?? throw new InvalidOperationException("Fixture link process did not start.");
        if (!child.WaitForExit(5000)) throw new InvalidOperationException("Owned fixture link command did not finish.");
        Check(child.ExitCode == 0, "Actual NTFS link fixture could not be created.");
    }
}
