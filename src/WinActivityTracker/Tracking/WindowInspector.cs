using System.Diagnostics;
using System.IO;
using System.Text;
using WinActivityTracker.Config;
using WinActivityTracker.Interop;

namespace WinActivityTracker.Tracking;

/// <summary>
/// Достаёт данные о переднем окне: процесс, путь, дружелюбное имя, фуллскрин.
/// Кэширует дружелюбные имена по пути, чтобы не дёргать версию файла каждые 3 секунды.
/// </summary>
public sealed class WindowInspector
{
    private readonly Dictionary<string, string> _friendlyNameCache = new(StringComparer.OrdinalIgnoreCase);

    public ForegroundWindowInfo? GetForeground()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        var path = GetProcessPath(pid);
        var exeName = string.IsNullOrEmpty(path)
            ? SafeProcessName(pid)
            : Path.GetFileName(path).ToLowerInvariant();

        if (string.IsNullOrEmpty(exeName))
            return null;

        var title = GetWindowTitle(hwnd);
        var appName = GetFriendlyName(path, exeName);
        var fullscreen = IsFullscreen(hwnd);

        return new ForegroundWindowInfo(exeName, path, appName, title, fullscreen);
    }

    private static string SafeProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            var name = p.ProcessName;
            return string.IsNullOrEmpty(name) ? "" : name.ToLowerInvariant() + ".exe";
        }
        catch
        {
            return "";
        }
    }

    private static string GetProcessPath(uint pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return "";

        try
        {
            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            if (NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size))
                return buffer.ToString(0, (int)size);
            return "";
        }
        catch
        {
            return "";
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var len = NativeMethods.GetWindowTextLength(hwnd);
        if (len <= 0)
            return "";
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private string GetFriendlyName(string path, string exeName)
    {
        if (string.IsNullOrEmpty(path))
            return PrettifyExe(exeName);

        if (_friendlyNameCache.TryGetValue(path, out var cached))
            return cached;

        string result;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var name = Clean(info.FileDescription);
            if (name == null)
                name = Clean(info.ProductName);
            result = name ?? PrettifyExe(exeName);
        }
        catch
        {
            result = PrettifyExe(exeName);
        }

        _friendlyNameCache[path] = result;
        return result;
    }

    // Отбрасываем бесполезные значения версии файла: пустые и совпадающие с именем exe.
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (v.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;
        return v;
    }

    private static readonly string[] JunkSuffixes =
    {
        "-win64-shipping", "-win32-shipping", "-wingdk-shipping", "-shipping",
        "-win64", "-win32", "-x64", "-x86", "-64", "-launcher",
    };

    private static string PrettifyExe(string exeName)
    {
        var baseName = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName[..^4]
            : exeName;

        // Отрезаем типовые технические суффиксы движков (напр. Valorant-Win64-Shipping).
        bool trimmed;
        do
        {
            trimmed = false;
            foreach (var s in JunkSuffixes)
            {
                if (baseName.Length > s.Length && baseName.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName[..^s.Length];
                    trimmed = true;
                }
            }
        } while (trimmed);

        var words = baseName.Split(new[] { '-', '_', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return exeName;

        return string.Join(' ', words.Select(w =>
            w.Length == 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>
    /// Окно считается полноэкранным, если его прямоугольник покрывает весь монитор.
    /// </summary>
    private static bool IsFullscreen(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return false;

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return false;

        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfoW(monitor, ref mi))
            return false;

        var m = mi.rcMonitor;
        // Небольшой допуск на рамки borderless-окон.
        return rect.Left <= m.Left + 1 && rect.Top <= m.Top + 1 &&
               rect.Right >= m.Right - 1 && rect.Bottom >= m.Bottom - 1;
    }
}
