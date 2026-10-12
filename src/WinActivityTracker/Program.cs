using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinActivityTracker.Autostart;
using WinActivityTracker.Config;
using WinActivityTracker.Data;
using WinActivityTracker.Tracking;

namespace WinActivityTracker;

public static class Program
{
    // Дашборд (WinUI-окно) - один экземпляр, повторный запуск разворачивает окно.
    private const string ViewerMutex = @"Local\WinActivityTracker.Viewer";
    private const string ShowEvent = @"Local\WinActivityTracker.ShowViewer";

    private static App? _app;

    [STAThread]
    private static void Main(string[] args)
    {
        if (HasArg(args, "--install-autostart")) { AutostartManager.Enable(); return; }
        if (HasArg(args, "--uninstall-autostart")) { AutostartManager.Disable(); return; }
        if (HasArg(args, "--collector")) { RunCollector(); return; }

        RunViewer();
    }

    /// <summary>
    /// Лёгкий счётчик: без WinUI, без окна, без трея. Только опрос переднего окна
    /// и запись в базу. Этот режим кидается в автозапуск при старте системы.
    /// </summary>
    private static void RunCollector()
    {
        using var mutex = new Mutex(true, CollectorControl.MutexName, out var first);
        if (!first)
            return; // счётчик уже работает

        // Выключен в «Автозагрузке» - не считаем вообще.
        if (AutostartManager.IsDisabledInStartup())
        {
            Logger.Info("Счётчик: отключён в Автозагрузке, не запускаюсь");
            return;
        }

        try
        {
            var config = AppConfig.Load();
            var db = new Database();
            db.Initialize();
            var collector = new ActivityCollector(config, db);
            collector.Start();

            // Правки из дашборда (категории, игнор) применяем на лету.
            using var watcher = new System.IO.FileSystemWatcher(AppPaths.DataDirectory, "config.json")
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            watcher.Changed += (_, _) => { Thread.Sleep(150); collector.ReloadConfig(); };

            // Если выключат в «Автозагрузке» - счётчик сам останавливается.
            using var stopTimer = new Timer(_ =>
            {
                if (AutostartManager.IsDisabledInStartup())
                {
                    Logger.Info("Счётчик остановлен: выключен в Автозагрузке");
                    Environment.Exit(0);
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            Logger.Info("Счётчик запущен (лёгкий фон, без UI)");
            // Держим процесс живым до завершения сессии/убийства.
            new ManualResetEvent(false).WaitOne();
        }
        catch (Exception ex)
        {
            Logger.Error("Счётчик упал", ex);
        }
    }

    /// <summary>Дашборд: WinUI-окно. Сам не считает, только показывает базу.</summary>
    private static void RunViewer()
    {
        using var mutex = new Mutex(true, ViewerMutex, out var first);
        if (!first)
        {
            // Дашборд уже открыт - просим развернуть окно и выходим (без второго процесса).
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEvent, out var ev))
                {
                    ev.Set();
                    ev.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Сигнал существующему дашборду не прошёл", ex);
            }
            return;
        }

        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent);
        var listening = true;
        var listener = new Thread(() =>
        {
            while (listening)
                if (showEvent.WaitOne(1000) && listening)
                    _app?.ShowMainWindow();
        })
        { IsBackground = true, Name = "ShowViewerListener" };
        listener.Start();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _app = new App();
        });

        listening = false;
        showEvent.Set();
        showEvent.Dispose();
    }

    private static bool HasArg(string[] args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}
