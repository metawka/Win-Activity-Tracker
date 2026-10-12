using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinActivityTracker.Config;

/// <summary>
/// Пользовательские настройки. Лежат в config.json рядом с базой, правятся руками.
/// При первом запуске создаётся файл с разумными значениями по умолчанию.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Как часто опрашивать переднее окно, секунд.</summary>
    public int SampleIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Порог склейки сеанса, секунд. Если вернулись в приложение в пределах этого
    /// времени (альт-таб, короткая отлучка) - сеанс продолжается, а не начинается новый.
    /// </summary>
    public int SessionGapSeconds { get; set; } = 300;

    /// <summary>
    /// Процессы, которые не считаем приложениями: системная оболочка, служебные окна.
    /// Имя exe в нижнем регистре.
    /// </summary>
    public HashSet<string> IgnoredProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "winactivitytracker.exe",
        "watcounter.exe",
        "searchhost.exe",
        "searchapp.exe",
        "startmenuexperiencehost.exe",
        "shellexperiencehost.exe",
        "applicationframehost.exe",
        "textinputhost.exe",
        "lockapp.exe",
        "systemsettings.exe",
        "dwm.exe",
        "sihost.exe",
        "ctfmon.exe",
        "rundll32.exe",
        "widgets.exe",
        "widgetservice.exe",
        "peopleexperiencehost.exe",
    };

    /// <summary>
    /// Процессы, которые принудительно считаются играми (имя exe, нижний регистр).
    /// Дополняет эвристику по папке установки.
    /// </summary>
    public HashSet<string> ForceGameProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2.exe",
        "dota2.exe",
        "gta5.exe",
        "gta5_enhanced.exe",
        "rdr2.exe",
        "cyberpunk2077.exe",
        "eldenring.exe",
        "valorant.exe",
        "valorant-win64-shipping.exe",
        "leagueoflegends.exe",
    };

    /// <summary>
    /// Процессы, которые никогда не игра, даже если открыты в полный экран
    /// (видео, браузеры). Защита эвристики фуллскрина.
    /// </summary>
    public HashSet<string> NeverGameProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe",
        "msedge.exe",
        "firefox.exe",
        "opera.exe",
        "brave.exe",
        "vlc.exe",
        "mpc-hc64.exe",
        "mpc-hc.exe",
        "potplayer.exe",
        "potplayermini64.exe",
        "code.exe",
        "devenv.exe",
    };

    /// <summary>Фрагменты путей, указывающие на игру. Нижний регистр.</summary>
    public List<string> GamePathMarkers { get; set; } = new()
    {
        @"\steamapps\common\",
        @"\steamapps\",
        @"\epic games\",
        @"\gog galaxy\games\",
        @"\gog games\",
        @"\riot games\",
        @"\ea games\",
        @"\electronic arts\",
        @"\ubisoft\",
        @"\battle.net\",
        @"\games\",
    };

    /// <summary>Считать ли полноэкранное окно признаком игры (после проверки NeverGame).
    /// По умолчанию выключено: даёт ложные срабатывания (скриншотеры, плееры, редакторы).</summary>
    public bool TreatFullscreenAsGame { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigFile))
            {
                var json = File.ReadAllText(AppPaths.ConfigFile);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg != null)
                {
                    cfg.Normalize();
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось прочитать config.json, беру значения по умолчанию", ex);
        }

        var fresh = new AppConfig();
        fresh.Save();
        return fresh;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(AppPaths.ConfigFile, json);
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сохранить config.json", ex);
        }
    }

    private void Normalize()
    {
        if (SampleIntervalSeconds < 1) SampleIntervalSeconds = 1;
        if (SampleIntervalSeconds > 60) SampleIntervalSeconds = 60;
        if (SessionGapSeconds < 30) SessionGapSeconds = 30;
        if (SessionGapSeconds > 3600) SessionGapSeconds = 3600;

        // Коллекции из JSON теряют компаратор без учёта регистра, пересобираем.
        IgnoredProcesses = new HashSet<string>(IgnoredProcesses, StringComparer.OrdinalIgnoreCase);
        ForceGameProcesses = new HashSet<string>(ForceGameProcesses, StringComparer.OrdinalIgnoreCase);
        NeverGameProcesses = new HashSet<string>(NeverGameProcesses, StringComparer.OrdinalIgnoreCase);
        GamePathMarkers = GamePathMarkers.Select(m => m.ToLowerInvariant()).ToList();

        // Проводник теперь считаем - убираем из игнора в старых конфигах.
        IgnoredProcesses.Remove("explorer.exe");
        IgnoredProcesses.Add("watcounter.exe"); // свой счётчик не учитываем
    }
}
