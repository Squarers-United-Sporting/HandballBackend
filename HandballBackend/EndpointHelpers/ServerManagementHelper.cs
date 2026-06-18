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
        var localHash = Config.GIT_REVISION;
        var (_, newHash) =
            await RunGitCommand($"rev-parse --git-path ${Config.RESOURCES_REPOSITORY}");

        if (localHash == null || localHash == newHash) return;
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
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 || error.Contains("fatal")) {
            throw new Exception($"Git returned Code {process.ExitCode}:  {error}");
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
        var authenticatedUrl = Config.RESOURCES_REPOSITORY.Replace("https://", $"https://{Config.GITHUB_TOKEN}@");
        await RunGitCommand($"push {authenticatedUrl}", Config.RESOURCES_FOLDER);
    }


    public static async Task InitResources() {
        if (Directory.Exists(Config.RESOURCES_FOLDER + "/.git")) {
            return;
        }

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