namespace HandballBackend.EndpointHelpers;

public static class ServerManagementHelper {
    private static Timer _timer;

    public static void CheckForUpdates() {
        RunGitCommand("fetch --all");
        var localHash = RunGitCommand("rev-parse master");
        var newHash = RunGitCommand("rev-parse origin/master");

        if (localHash == newHash) return;
        Console.WriteLine("Updates on master found; restarting ");
        UpdateServer();
    }

    public static void QuitServer() {
        Environment.Exit(1);
    }

    public static void RestartServer() {
        Environment.Exit(1);
    }

    public static void RebuildServer() {
        Environment.Exit(2);
    }

    public static void UpdateServer() {
        Environment.Exit(3);
    }

    private static string RunGitCommand(string arguments) {
        var process = new System.Diagnostics.Process {
            StartInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    public static void StartCheckingForUpdates(int frequency = 60 * 60) {
        _timer = new Timer(_ => Task.Run(CheckForUpdates), null, TimeSpan.Zero, TimeSpan.FromSeconds(frequency));
    }
}