namespace HandballBackend;

public static class Config {
    public static bool SAVE_ERRORS = false;
    public static bool CHECKING_GIT = false;
    public static string BACKUP_FOLDER = "./backup";
    public static bool USING_POSTGRES = true;
    public static string RESOURCES_FOLDER = "./resources";
    public const int TimeoutTime = 30;
    public const string MY_ADDRESS = "https://api.squarers.club";
    public static string SECRETS_FOLDER = "./secrets";
    public static bool LOGGING = false;
}