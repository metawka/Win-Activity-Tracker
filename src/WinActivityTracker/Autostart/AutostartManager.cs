using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using WinActivityTracker.Config;

namespace WinActivityTracker.Autostart;

/// <summary>
/// Автозапуск счётчика при входе пользователя через ключ реестра HKCU\...\Run.
/// Именно такие записи видны в Диспетчере задач в разделе «Автозагрузка приложений»
/// и включаются/выключаются прямо там. (Планировщик задач туда не попадает.)
/// </summary>
public static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Сюда Диспетчер задач пишет флаг вкл/выкл для записи автозагрузки.
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string RunValueName = "Windows Activity Tracker - Counter";
    private static readonly string[] LegacyRunValueNames =
        { "WinActivityTracker", "Windows Activity Tracker", "Windows Activity Tracker - подсчёт" };
    private const string LegacyTaskName = "WinActivityTracker";

    private static string ExePath => Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? "";

    private static string CounterExePath
    {
        get
        {
            var dir = Path.GetDirectoryName(ExePath) ?? "";
            return Path.Combine(dir, "WatCounter.exe");
        }
    }

    // В автозапуск идёт отдельный лёгкий счётчик WatCounter.exe (без окна и трея).
    private static string LaunchCommand => File.Exists(CounterExePath)
        ? $"\"{CounterExePath}\""
        : $"\"{ExePath}\" --collector";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static bool Enable()
    {
        if (string.IsNullOrEmpty(ExePath))
        {
            Logger.Error("Автозапуск: не удалось определить путь к exe");
            return false;
        }

        RemoveLegacyTask();      // на случай, если раньше стоял в Планировщике
        foreach (var n in LegacyRunValueNames) RemoveValue(n);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(RunValueName, LaunchCommand, RegistryValueKind.String);
            MarkEnabledInStartup(); // снять флаг «отключено», если стоял из Диспетчера
            Logger.Info("Автозапуск включён (реестр HKCU\\...\\Run, виден в Автозагрузке)");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Автозапуск: запись в реестр не удалась", ex);
            return false;
        }
    }

    /// <summary>Выключен ли счётчик в разделе «Автозагрузка» Диспетчера задач.</summary>
    public static bool IsDisabledInStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: false);
            if (key?.GetValue(RunValueName) is byte[] b && b.Length > 0)
                return (b[0] & 1) == 1; // нечётный первый байт = отключено
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkEnabledInStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ApprovedKeyPath, writable: true);
            key.SetValue(RunValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }
        catch (Exception ex)
        {
            Logger.Error("StartupApproved: отметить включённым не удалось", ex);
        }
    }

    public static bool Disable()
    {
        RemoveLegacyTask();
        var ok = RemoveValue(RunValueName);
        foreach (var n in LegacyRunValueNames) RemoveValue(n);
        Logger.Info("Автозапуск выключен");
        return ok;
    }

    private static bool RemoveValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(name) != null)
                key.DeleteValue(name, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Автозапуск: удаление из реестра", ex);
            return false;
        }
    }

    private static void RemoveLegacyTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{LegacyTaskName}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch
        {
            // Не критично.
        }
    }
}
