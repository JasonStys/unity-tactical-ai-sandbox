// File: Program.cs
// Purpose: Launch the actual offline designer with a durable external OS-local profile and normal lifetime.
// Public API: One optional --profile ABSOLUTE_DIRECTORY argument; no secret or server bootstrap.
// Variables: owned profile is disposed after the visible application actually closes.
// Declaration index: docs/CODE_INDEX.md. Author: Jason Stys.

namespace TacticalAI.Designer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length == 4 && args[0] == "--acceptance-root" && args[2] == "--phase" && int.TryParse(args[3], out int phase))
            {
                try { return NativeAcceptance.Run(args[1], phase); }
                catch (Exception error) { Console.Error.WriteLine(error); return 1; }
            }
            string path = args.Length == 0 ? DesignerProfile.DefaultDirectory()
                : args.Length == 2 && args[0] == "--profile" ? args[1]
                : throw new ArgumentException("Use the application normally, or --profile ABSOLUTE_DIRECTORY for a separate workspace.");
            using var profile = new DesignerProfile(path);
            using var window = new DesignerWindow(profile);
            Application.Run(window);
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Workspace was not reset. " + error.Message, "Unity Tactical AI Designer could not open", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
