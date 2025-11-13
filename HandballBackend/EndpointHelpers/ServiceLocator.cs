namespace HandballBackend.EndpointHelpers;

public static class ServiceLocator {
    public static IServiceProvider Instance { get; private set; } = null!;

    public static void Init(IServiceProvider provider) {
        Instance = provider;
    }

    public static T Get<T>() where T : notnull => Instance.GetRequiredService<T>();
}