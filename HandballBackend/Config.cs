namespace HandballBackend;

public static class Config {
    public static int BACKUP_TIME => int.Parse(Environment.GetEnvironmentVariable("BACKUP_TIME") ?? "-1");
    public static bool SAVE_ERRORS => Environment.GetEnvironmentVariable("SAVE_ERRORS") != "false";
    public static int GIT_CHECK_TIME => int.Parse(Environment.GetEnvironmentVariable("GIT_CHECK_TIME") ?? "-1");
    public static string BACKUP_FOLDER => Environment.GetEnvironmentVariable("BACKUP_DIRECTORY") ?? "./backup";
    public static bool USING_POSTGRES = true;
    public static string RESOURCES_FOLDER => Environment.GetEnvironmentVariable("RESOURCES_DIRECTORY") ?? "./resources";
    public static string RESOURCES_REPOSITORY => Environment.GetEnvironmentVariable("RESOURCES_REPOSITORY") ?? "https://github.com/jh1236/handball-resources";
    public const int TimeoutTime = 30;
    public static string MY_ADDRESS => Environment.GetEnvironmentVariable("SERVER_ADDRESS") ?? "https://api.squarers.club" ;
    public static bool LOGGING => Environment.GetEnvironmentVariable("LOGGING") == "true";
    public static string? GIT_REVISION  => Environment.GetEnvironmentVariable("GIT_REVISION");
    public static string? GITHUB_TOKEN  => Environment.GetEnvironmentVariable("GITHUB_TOKEN");
}