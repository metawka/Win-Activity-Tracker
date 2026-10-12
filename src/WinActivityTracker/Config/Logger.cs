using System.IO;

namespace WinActivityTracker.Config;

/// <summary>
/// Простейший файловый лог. Консоли у фонового процесса нет, поэтому всё в файл.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        try
        {
            lock (Gate)
            {
                // Держим лог компактным: обрезаем, если разросся за 1 МБ.
                var file = AppPaths.LogFile;
                if (File.Exists(file) && new FileInfo(file).Length > 1_000_000)
                {
                    var tail = File.ReadAllLines(file);
                    File.WriteAllLines(file, tail.Skip(Math.Max(0, tail.Length - 500)));
                }
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // Лог не должен ронять приложение.
        }
    }
}
