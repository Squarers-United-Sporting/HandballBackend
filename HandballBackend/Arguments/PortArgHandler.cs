namespace HandballBackend.Arguments;

public class PortArgHandler() : AbstractArgumentHandler("p", "port", "Assigns the port that the server will run on.") {
    protected override void ParseIfMatched(string[] args, ref int index, WebApplicationBuilder builder) {
        Environment.SetEnvironmentVariable("ASPNETCORE_HTTP_PORTS", args[index++]);
    }
}