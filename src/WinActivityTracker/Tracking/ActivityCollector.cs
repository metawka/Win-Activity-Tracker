using System.Diagnostics;
using Microsoft.Win32;
using WinActivityTracker.Config;
using WinActivityTracker.Data;

namespace WinActivityTracker.Tracking;

/// <summary>
/// Фоновый сбор: раз в несколько секунд смотрит переднее окно и начисляет
/// прошедшее время текущему приложению. Время в фокусе считается целиком,
/// idle не вычитается. Блокировка экрана и сон останавливают счёт.
/// </summary>
public sealed class ActivityCollector : IDisposable
{
    private AppConfig _config;
    private readonly WindowInspector _inspector;
    private ProcessClassifier _classifier;
    private readonly Database _db;

    private Thread? _thread;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _paused;
    private volatile bool _resetTiming;

    public string CurrentStatus { get; private set; } = "Запуск...";

    public ActivityCollector(AppConfig config, Database db)
    {
        _config = config;
        _db = db;
        _inspector = new WindowInspector();
        _classifier = new ProcessClassifier(config);
    }

    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "ActivityCollector",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
        Logger.Info("Коллектор запущен");
    }

    /// <summary>Перечитать конфиг (правки из дашборда: категории, игнор-лист).</summary>
    public void ReloadConfig()
    {
        try
        {
            _config = AppConfig.Load();
            _classifier = new ProcessClassifier(_config);
            Logger.Info("Конфиг перечитан");
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось перечитать конфиг", ex);
        }
    }

    private void Loop()
    {
        var intervalMs = _config.SampleIntervalSeconds * 1000;
        var sw = Stopwatch.StartNew();
        var lastElapsedMs = 0L;

        while (!_cts.IsCancellationRequested)
        {
            if (_cts.Token.WaitHandle.WaitOne(intervalMs))
                break; // отменили

            var nowElapsedMs = sw.ElapsedMilliseconds;
            var deltaMs = nowElapsedMs - lastElapsedMs;
            lastElapsedMs = nowElapsedMs;

            if (_paused)
            {
                CurrentStatus = "На паузе (блокировка/сон)";
                _resetTiming = true;
                continue;
            }

            if (_resetTiming)
            {
                // Первый тик после паузы: не начисляем накопившийся разрыв.
                _resetTiming = false;
                continue;
            }

            // Защита от скачков (разбудили, таймер проспал): не больше двух интервалов.
            var deltaSec = (int)Math.Round(deltaMs / 1000.0);
            deltaSec = Math.Clamp(deltaSec, 0, _config.SampleIntervalSeconds * 2);
            if (deltaSec <= 0)
                continue;

            try
            {
                // Экран активен (не блокировка/сон) - считаем всё время, любое окно.
                _db.AddScreenSeconds(deltaSec, DateTime.Now);
                Sample(deltaSec);
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка в цикле сбора", ex);
            }
        }
    }

    private void Sample(int seconds)
    {
        var info = _inspector.GetForeground();
        if (info == null)
        {
            CurrentStatus = "Нет активного окна";
            return;
        }

        if (!_classifier.ShouldTrack(info))
        {
            CurrentStatus = $"Пропуск: {info.ProcessName}";
            return;
        }

        var category = _classifier.Classify(info);
        IconCache.Ensure(info.ProcessName, info.ExecutablePath);
        var now = DateTime.Now;
        _db.AddSeconds(info.ProcessName, info.AppName, category.ToString(), seconds, now);
        _db.RecordSession(info.ProcessName, info.AppName, category.ToString(), seconds, now, _config.SessionGapSeconds);
        CurrentStatus = $"{info.AppName} ({category})";
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.SessionLogoff:
                _paused = true;
                Logger.Info("Пауза: сессия заблокирована");
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.SessionLogon:
                _paused = false;
                _resetTiming = true;
                Logger.Info("Возобновление: сессия разблокирована");
                break;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _paused = true;
                Logger.Info("Пауза: переход в сон");
                break;
            case PowerModes.Resume:
                _paused = false;
                _resetTiming = true;
                Logger.Info("Возобновление: выход из сна");
                break;
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _cts.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
