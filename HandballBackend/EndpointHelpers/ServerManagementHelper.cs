using System.Web.WebPages;

namespace HandballBackend.EndpointHelpers;

public static class ServerManagementHelper {
    private static Timer _timer;

    public static async Task CheckForUpdates() {
        Task[] tasks = [CheckForProjectUpdates(), CheckForResourceUpdates()];
        await Task.WhenAll(tasks);
    }

    private static async Task CheckForProjectUpdates() {
        await RunGitCommand("fetch --all");
        var localHash = await RunGitCommand("rev-parse master");
        var newHash = await RunGitCommand("rev-parse origin/master");

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

    private static async Task<Tuple<int, string>> RunGitCommand(string arguments, string? workingDirectory = null) {
        var processStartInfo = new System.Diagnostics.ProcessStartInfo {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (workingDirectory != null) {
            processStartInfo.WorkingDirectory = workingDirectory;
        }

        var process = new System.Diagnostics.Process {
            StartInfo = processStartInfo
        };


        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) {
            throw new Exception($"Git returned Code {process.ExitCode}:  {output}");
        }

        return new Tuple<int, string>(process.ExitCode, output.Trim());
    }

    public static async Task SaveResourcesToGit() {
        await CheckForResourceUpdates(); //attempt to get resources from origin before committing - don't create a conflict
        var (_, output) = await RunGitCommand("status --porcelain", Config.RESOURCES_FOLDER);
        if (output.IsEmpty()) {
            return;
        }

        await RunGitCommand("add -A", Config.RESOURCES_FOLDER);
        await RunGitCommand("commit -m \"Automatic Commit from Server\"", Config.RESOURCES_FOLDER);
    }


    public static async Task InitResources() {
        Console.WriteLine("Initializing resources");

        if (Directory.Exists(Config.RESOURCES_FOLDER + "/.git")) {
            Console.WriteLine("Resources already exist; aborting");
            return;
        }

        Console.WriteLine("Cloning from git");
        await RunGitCommand($"clone {Config.RESOURCES_REPOSITORY} {Config.RESOURCES_FOLDER}");
    }

    public static async Task CheckForResourceUpdates() {
        await InitResources();
        await RunGitCommand("fetch --all", Config.RESOURCES_FOLDER);
        var localHash = RunGitCommand("rev-parse master", Config.RESOURCES_FOLDER);
        var newHash = RunGitCommand("rev-parse origin/master", Config.RESOURCES_FOLDER);
        if (localHash == newHash) {
            return;
        }

        Console.WriteLine("Updates for resources found; downloading ");
        await RunGitCommand("pull", Config.RESOURCES_FOLDER);
    }

    public static void StartCheckingForUpdates(int frequency = 60 * 60) {
        _ = CheckForUpdates();
        _timer = new Timer(_ => Task.Run(CheckForUpdates), null, TimeSpan.Zero, TimeSpan.FromSeconds(frequency));
    }
}