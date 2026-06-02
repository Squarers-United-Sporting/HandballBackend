namespace HandballBackend.Arguments;

public class LoggingArgHandler()
    : AbstractArgumentHandler("l", "logRequest", "Logs Requests that the server receives.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        if (index < args.Length && ((string[]) ["true", "false"]).Contains(args[index])) {
            Environment.SetEnvironmentVariable("LOGGING", (args[index++] == "true").ToString());
        } else {
            Environment.SetEnvironmentVariable("LOGGING", "true");
        }
    }
}