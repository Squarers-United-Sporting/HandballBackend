using Microsoft.EntityFrameworkCore;

namespace HandballBackend.EndpointHelpers;

public static class PostgresBackup {
    private static Timer? _timer;
    private static bool Enabled = false;

    public static async Task MakeTimestampedBackup(string backupTitle = "auto", bool force = false) {
        if (!await HasDatabaseChanged() && !force) {
            Console.WriteLine("Backup not necessary; files are the same");
            return;
        }
        Console.WriteLine($"Making Backup '{backupTitle}'");

        await MakeBackup($"{DateTime.Now:yyyy-MM-dd HH-mm-ss} {backupTitle}");
        await SaveLengthToFile();
    }

    public static async Task MakeBackup(string filename = "latest") {
        if (!Enabled || !Config.USING_POSTGRES) return;
        var process = new System.Diagnostics.Process();
        var connArgs = ParseConnectionString(HandballContext.ConnectionString);

        var startInfo = new System.Diagnostics.ProcessStartInfo {
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            FileName = "pg_dump",
            Arguments = $"-U \"{connArgs["User ID"]}\" -h localhost -p 5432 {connArgs["Database"]}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = {
                ["PGPASSWORD"] = connArgs["Password"]
            }
        };

        process.StartInfo = startInfo;
        process.Start();
        var backupFile = $"{Config.BACKUP_FOLDER}/{filename}.txt";
        using (var output = process.StandardOutput) {
            await using (var file = File.CreateText(backupFile)) {
                await file.WriteAsync(await output.ReadToEndAsync());
            }
        }



        await process.WaitForExitAsync();
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString) {
        var split = connectionString.Split(';');
        var dict = new Dictionary<string, string>();
        foreach (var s in split) {
            if (!s.Contains('=')) continue;
            var temp = s.Split('=');
            var key = temp[0];
            var value = temp[1];
            dict.Add(key, value);
        }

        return dict;
    }

    public static void PeriodicBackups(int backupTime) {
        Enabled = true;
        _ = Task.Run(() => MakeBackup());
        _timer ??= new Timer(_ => Task.Run(() => MakeBackup()), null, TimeSpan.Zero, TimeSpan.FromHours(backupTime));
    }

    private static async Task<Dictionary<string, long>> LoadLengthFromFile() {
        await File.AppendAllTextAsync($"{Config.BACKUP_FOLDER}/dbTableLengths.txt", "");
        var lines = await File.ReadAllLinesAsync($"{Config.BACKUP_FOLDER}/dbTableLengths.txt");
        var dict = new Dictionary<string, long>();
        foreach (var line in lines) {
            if (!line.Contains(':')) continue;
            var split = line.Split(':');
            var table = split[0];
            var length = long.Parse(split[1]);
            dict.Add(table, length);
        }

        return dict;
    }

    private static async Task SaveLengthToFile() {
        var lines = new List<string>();
        foreach (var (table, length) in await GetLengthFromDatabase()) {
            lines.Add($"{table}:{length}");
        }

        await File.WriteAllLinesAsync($"{Config.BACKUP_FOLDER}/dbTableLengths.txt", lines);
    }

    private static async Task<Dictionary<string, long>> GetLengthFromDatabase() {
        var dict = new Dictionary<string, long>();
        var db = new HandballContext();
        dict[nameof(db.GameEvents)] = await db.GameEvents.LongCountAsync();
        dict[nameof(db.Games)] = await db.Games.LongCountAsync();
        dict[nameof(db.Officials)] = await db.Officials.LongCountAsync();
        dict[nameof(db.People)] = await db.People.LongCountAsync();
        dict[nameof(db.PlayerGameStats)] = await db.PlayerGameStats.LongCountAsync();
        dict[nameof(db.QuotesOfTheDay)] = await db.QuotesOfTheDay.LongCountAsync();
        dict[nameof(db.Teams)] = await db.Teams.LongCountAsync();
        dict[nameof(db.TournamentOfficials)] = await db.TournamentOfficials.LongCountAsync();
        dict[nameof(db.TournamentTeams)] = await db.TournamentTeams.LongCountAsync();
        dict[nameof(db.Tournaments)] = await db.Tournaments.LongCountAsync();
        return dict;
    }

    private static async Task<bool> HasDatabaseChanged() {
        var prevDict = await LoadLengthFromFile();
        var nowDict = await GetLengthFromDatabase();
        foreach (var (table, length) in nowDict) {
            if (prevDict.GetValueOrDefault(table, 0) != length) {
                return true;
            }
        }

        return false;
    }
}