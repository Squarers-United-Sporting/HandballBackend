namespace HandballBackend.Arguments;

public class WorkingDirectoryArgHandler() : AbstractArgumentHandler("wd", "working-directory", "Sets the working directory for resources.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        Directory.SetCurrentDirectory(args[index++]);
    }
}