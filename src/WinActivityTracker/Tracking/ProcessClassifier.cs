using System.IO;
using WinActivityTracker.Config;

namespace WinActivityTracker.Tracking;

public enum ActivityCategory
{
    Game,
    Application,
}

/// <summary>
/// Решает, стоит ли учитывать окно и к какой категории его отнести.
/// Порядок: сам трекер -> игнор-лист -> принудительные игры -> папка установки -> фуллскрин.
/// </summary>
public sealed class ProcessClassifier
{
    private readonly AppConfig _config;
    private readonly string _selfProcess;

    public ProcessClassifier(AppConfig config)
    {
        _config = config;
        // Свой собственный процесс не учитываем никогда, даже если открыт дашборд.
        _selfProcess = (Path.GetFileName(Environment.ProcessPath) ?? "").ToLowerInvariant();
    }

    public bool ShouldTrack(ForegroundWindowInfo info)
    {
        if (string.IsNullOrEmpty(info.ProcessName))
            return false;
        if (!string.IsNullOrEmpty(_selfProcess) && info.ProcessName == _selfProcess)
            return false;
        if (_config.IgnoredProcesses.Contains(info.ProcessName))
            return false;
        // Окно без заголовка и без пути - почти всегда служебное, пропускаем.
        if (string.IsNullOrEmpty(info.ExecutablePath) && string.IsNullOrEmpty(info.WindowTitle))
            return false;
        return true;
    }

    public ActivityCategory Classify(ForegroundWindowInfo info)
    {
        if (_config.ForceGameProcesses.Contains(info.ProcessName))
            return ActivityCategory.Game;

        if (_config.NeverGameProcesses.Contains(info.ProcessName))
            return ActivityCategory.Application;

        var path = info.ExecutablePath.ToLowerInvariant();
        if (!string.IsNullOrEmpty(path) && _config.GamePathMarkers.Any(marker => path.Contains(marker)))
            return ActivityCategory.Game;

        if (_config.TreatFullscreenAsGame && info.IsFullscreen)
            return ActivityCategory.Game;

        return ActivityCategory.Application;
    }
}
