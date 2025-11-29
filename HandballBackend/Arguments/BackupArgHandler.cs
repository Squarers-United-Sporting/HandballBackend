using System.Web.WebPages;
using HandballBackend.EndpointHelpers;

namespace HandballBackend.Arguments;

public class BackupArgHandler()
    : AbstractArgumentHandler("b", "backup", "Sets the automatic database backup up.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        var backupTime = 24 * 7;
        if (index < args.Length && args[index].IsInt()) {
            backupTime = args[index++].AsInt();
        }
        Config.BACKUP_TIME = backupTime;
        // PostgresBackup.PeriodicBackups(backupTime);
    }
}