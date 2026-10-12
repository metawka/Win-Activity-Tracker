using System.IO;

namespace WinActivityTracker.Config;

/// <summary>
/// Пути к данным приложения. Всё держим в %LOCALAPPDATA%, а не в папке проекта:
/// папка проекта лежит в OneDrive и синхронизировалась бы на каждую запись в базу.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "WinActivityTracker";

    // Берём реальный профиль (%USERPROFILE%\AppData\Local), а не SpecialFolder.LocalApplicationData:
    // последний может быть подменён (напр. запуск из песочницы упакованного приложения),
    // и тогда данные разъезжаются по разным базам. Реальный профиль стабилен.
    private static string LocalAppData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");

    public static string DataDirectory { get; } = CreateDir(Path.Combine(LocalAppData, AppFolderName));

    public static string IconsDirectory { get; } = CreateDir(Path.Combine(LocalAppData, AppFolderName, "icons"));

    public static string DatabaseFile => Path.Combine(DataDirectory, "activity.db");

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");

    public static string LogFile => Path.Combine(DataDirectory, "tracker.log");

    private static string CreateDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
