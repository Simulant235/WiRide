namespace WiRide.Services;

// Zugriff auf DI-Services aus Pages, die mit "new" erstellt werden
public static class ServiceHelper
{
    public static T Get<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();
}
