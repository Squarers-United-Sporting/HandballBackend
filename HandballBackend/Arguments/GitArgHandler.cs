using System.Web.WebPages;
using HandballBackend.EndpointHelpers;

namespace HandballBackend.Arguments;

public class GitArgHandler() : AbstractArgumentHandler("u", "update", "Automatically updates the program.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        var frequency = 60 * 60;
        if (index < args.Length) {
            if (args[index].IsInt()) {
                frequency = int.Parse(args[index++]);
            }
        }

        Environment.SetEnvironmentVariable("GIT_CHECK_TIME", frequency.ToString());
    }
}