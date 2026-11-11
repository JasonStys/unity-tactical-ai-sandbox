// File: NativeFiles.cs
// Purpose: Admit static Windows paths and single-link ordinary files before bounded local I/O.
// Public API: NativeFiles provides reusable input/new-output/profile admission, not same-owner race protection.
// Variables: fixed System32 metadata calls own their SafeFileHandle; input and output have explicit caps.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TacticalAI.Designer;

/// <summary>Boundary for accidental existing aliases under a trusted Windows OS owner.</summary>
public static class NativeFiles
{
    public static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>Admit ordinary local-drive syntax before metadata probes; UNC/device paths and alternate data streams are not local documents.</summary>
    private static string LocalPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.Length < 3 || !char.IsAsciiLetter(full[0]) || full[1] != ':' || full[2] != '\\' || full[3..].Contains(':'))
            throw new InvalidDataException("Choose an ordinary local disk path, not a network/device path or alternate data stream.");
        DriveType type = new DriveInfo(full[..3]).DriveType;
        if (type is not (DriveType.Fixed or DriveType.Removable))
            throw new InvalidDataException("Choose a fixed or removable local disk; network filesystems are unsupported.");
        return full;
    }

    /// <summary>Reject existing reparse/file ancestors before any directory creation or output.</summary>
    public static string DirectoryPath(string path, bool forbidProgramTree = false)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Choose an absolute directory.");
        string full = Path.TrimEndingDirectorySeparator(LocalPath(path));
        if (forbidProgramTree)
        {
            string program = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
            if (full.Equals(program, StringComparison.OrdinalIgnoreCase) || full.StartsWith(program + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Keep durable data outside the complete application folder.");
        }
        for (var part = new DirectoryInfo(full); part is not null; part = part.Parent)
        {
            FileAttributes? attributes = Attributes(part.FullName);
            if (attributes is not null && ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0))
                throw new InvalidDataException("Directories must be real directories, not aliases.");
        }
        return full;
    }

    /// <summary>Missing final files are allowed; existing devices/directories/reparse or hardlinks are not.</summary>
    public static void OrdinaryIfPresent(string path)
    {
        LocalPath(path);
        DirectoryPath(Path.GetDirectoryName(Path.GetFullPath(path))!);
        FileAttributes? attributes = Attributes(path);
        if (attributes is null) return;
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("An ordinary single-link file is required.");
        using SafeFileHandle handle = OpenMetadata(path, 0x80, 0x7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        RequireMetadata(handle);
    }

    private static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    /// <summary>Validate the actual opened file handle, including link count, before consuming bytes.</summary>
    private static void RequireMetadata(SafeFileHandle handle)
    {
        if (handle.IsInvalid || !ReadInformation(handle, out var information) || information.NumberOfLinks != 1 ||
            (information.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("A single-link ordinary local file is required.");
    }

    /// <summary>Read one MiB or the explicitly smaller marker budget using a retained descriptor.</summary>
    public static string ReadText(string path, int maximumBytes = 1_048_576)
    {
        if (maximumBytes is < 1 or > 8_388_608) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        OrdinaryIfPresent(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireMetadata(stream.SafeFileHandle);
        if (stream.Length > maximumBytes) throw new InvalidDataException("Input exceeds its supported byte budget.");
        byte[] bytes = new byte[maximumBytes + 1];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = stream.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > maximumBytes) throw new InvalidDataException("Input grew beyond its byte budget.");
        return Utf8.GetString(bytes, 0, count);
    }

    /// <summary>Write a new complete export only; never overwrite a chosen input or an existing output.</summary>
    public static void WriteNew(string path, string text)
    {
        LocalPath(path);
        DirectoryPath(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (text.Length > 8_388_608 || Utf8.GetByteCount(text) > 8_388_608)
            throw new InvalidDataException("Export exceeds eight MiB.");
        byte[] bytes = Utf8.GetBytes(text);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        RequireMetadata(stream.SafeFileHandle);
        stream.Write(bytes);
        stream.Flush(true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME AccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME WriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle OpenMetadata(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadInformation(SafeFileHandle handle, out FileInformation information);
}
