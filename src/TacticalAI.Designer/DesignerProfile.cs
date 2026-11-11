// File: DesignerProfile.cs
// Purpose: Keep actual designer projects outside the application with retained previous revision and exclusive ownership.
// Public API: DesignerProfile loads/saves complete validated projects and retains data on malformed input.
// Variables: fixed marker/lock/current/previous paths; temporary writes are exclusive and flushed before rename.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Security.Cryptography;
using TacticalAI.Studio;

namespace TacticalAI.Designer;

/// <summary>One trusted Windows owner; plaintext documents and hashes are not account authentication.</summary>
public sealed class DesignerProfile : IDisposable
{
    private const string Marker = "JasonStys/unity-tactical-ai-sandbox/designer-profile-v1\n";
    private readonly FileStream ownership;
    private string? expectedHash;
    public string Directory { get; }
    private string Current => Path.Combine(Directory, "current.json");
    private string Previous => Path.Combine(Directory, "previous.json");

    public DesignerProfile(string directory)
    {
        Directory = NativeFiles.DirectoryPath(directory, forbidProgramTree: true);
        foreach (string name in new[] { "owner.txt", "owner.lock", "current.json", "previous.json" })
            NativeFiles.OrdinaryIfPresent(Path.Combine(Directory, name));
        string marker = Path.Combine(Directory, "owner.txt");
        if (!File.Exists(marker) && System.IO.Directory.Exists(Directory) && System.IO.Directory.EnumerateFileSystemEntries(Directory).Any())
            throw new InvalidDataException("An unmarked nonempty directory cannot become a designer workspace.");
        if (File.Exists(marker) && NativeFiles.ReadText(marker, 4096) != Marker)
            throw new InvalidDataException("This directory belongs to another application.");
        System.IO.Directory.CreateDirectory(Directory);
        ownership = new FileStream(Path.Combine(Directory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (!File.Exists(marker)) NativeFiles.WriteNew(marker, Marker);
            if (File.Exists(Current)) expectedHash = Hash(NativeFiles.ReadText(Current));
        }
        catch
        {
            ownership.Dispose();
            throw;
        }
    }

    public static string DefaultDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(home)) throw new InvalidDataException("Local application data directory is unavailable.");
        return Path.Combine(home, "JasonStysPortfolio", "unity-tactical-ai-sandbox", "workspace");
    }

    public DesignerSession? Load() => File.Exists(Current) ? DesignerSession.FromProject(NativeFiles.ReadText(Current)) : null;
    public DesignerSession LoadPrevious() => DesignerSession.FromProject(NativeFiles.ReadText(Previous));

    /// <summary>Admit every fixed mutable path before saving; failed current validation preserves both revisions.</summary>
    public void Save(DesignerSession session)
    {
        string serialized = session.ProjectJson();
        DesignerSession.FromProject(serialized);
        NativeFiles.DirectoryPath(Directory, forbidProgramTree: true);
        NativeFiles.OrdinaryIfPresent(Current);
        NativeFiles.OrdinaryIfPresent(Previous);
        string? current = File.Exists(Current) ? NativeFiles.ReadText(Current) : null;
        if ((current is null ? null : Hash(current)) != expectedHash)
            throw new InvalidDataException("Stored project changed outside this owner; close and review it before writing.");
        if (current is not null)
        {
            DesignerSession.FromProject(current);
            Replace(Previous, current);
        }
        Replace(Current, serialized);
        expectedHash = Hash(serialized);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(NativeFiles.Utf8.GetBytes(text)));

    /// <summary>Stage an exclusive bounded file then atomically replace only an admitted fixed private name.</summary>
    private void Replace(string target, string text)
    {
        string temporary = Path.Combine(Directory, "pending-" + Guid.NewGuid().ToString("N") + ".json");
        NativeFiles.WriteNew(temporary, text);
        try
        {
            NativeFiles.OrdinaryIfPresent(target);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            // Only this call's newly created name is eligible; never clean another file or directory.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Dispose() => ownership.Dispose();
}
