using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using WinActivityTracker.Config;

namespace WinActivityTracker.Tracking;

/// <summary>
/// Кэш иконок приложений: раз извлекает иконку из exe и кладёт PNG в папку icons,
/// чтобы список процессов выглядел как в Диспетчере задач. Дальше берётся из файла.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IconCache
{
    private static readonly HashSet<string> Attempted = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    /// <summary>Путь к PNG-иконке процесса (файла может ещё не быть).</summary>
    public static string PathFor(string processName)
        => Path.Combine(AppPaths.IconsDirectory, Sanitize(processName) + ".png");

    /// <summary>Извлечь иконку из exe, если ещё не пробовали. Тихо, без исключений наружу.</summary>
    public static void Ensure(string processName, string exePath)
    {
        if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(exePath))
            return;

        lock (Gate)
        {
            if (!Attempted.Add(processName))
                return;
        }

        var pngPath = PathFor(processName);
        if (File.Exists(pngPath))
            return;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return;
            using var bmp = icon.ToBitmap();
            bmp.Save(pngPath, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            Logger.Error($"Иконку {processName} извлечь не удалось", ex);
        }
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
