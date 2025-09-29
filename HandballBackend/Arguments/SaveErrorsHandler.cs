using HandballBackend.ErrorTypes;

namespace HandballBackend.Arguments;

public class SaveErrorsHandler()
    : AbstractArgumentHandler("s", "saveErrors", "Saves all exceptions into a text file.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        bool output;
        if (index < args.Length && ((string[]) ["true", "false"]).Contains(args[index])) {
            output = args[index++] == "true";
        } else {
            output = true;
        }

        if (!output) return;

        builder.Services.AddProblemDetails();
        Config.SAVE_ERRORS = true;
    }
}