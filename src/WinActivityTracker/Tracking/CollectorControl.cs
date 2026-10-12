using System.Diagnostics;
using System.Threading;
using WinActivityTracker.Config;

namespace WinActivityTracker.Tracking;

/// <summary>Проверка и запуск фонового счётчика из дашборда.</summary>
public static class CollectorControl
{
    public const string MutexName = @"Local\WinActivityTracker.Collector";
    public const string CounterExeName = "WatCounter.exe";

    /// <summary>Полный путь к exe счётчика (рядом с текущим exe).</summary>
    public static string CounterExePath
    {
        get
        {
            var dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            return System.IO.Path.Combine(dir, CounterExeName);
        }
    }

    /// <summary>Запущен ли сейчас процесс-счётчик.</summary>
    public static bool IsRunning()
    {
        try
        {
            using var m = Mutex.OpenExisting(MutexName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Остановить счётчик (закрыть процесс WatCounter.exe).</summary>
    public static void Stop()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/F /IM " + CounterExeName,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(4000);
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось остановить счётчик", ex);
        }
    }

    /// <summary>Запустить счётчик (отдельный WatCounter.exe; дубликат сам выйдет по мьютексу).</summary>
    public static void Start()
    {
        try
        {
            var exe = CounterExePath;
            if (!System.IO.File.Exists(exe))
                exe = Environment.ProcessPath ?? ""; // запасной вариант: тот же exe

            if (string.IsNullOrEmpty(exe))
                return;

            var isCounter = exe.EndsWith(CounterExeName, StringComparison.OrdinalIgnoreCase);
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = isCounter ? "" : "--collector",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось запустить счётчик", ex);
        }
    }
}
