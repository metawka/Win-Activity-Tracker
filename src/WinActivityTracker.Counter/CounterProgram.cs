using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using WinActivityTracker.Autostart;
using WinActivityTracker.Config;
using WinActivityTracker.Data;
using WinActivityTracker.Tracking;

namespace WinActivityTracker;

/// <summary>
/// Отдельный лёгкий процесс-счётчик (WatCounter.exe). Без окна и трея.
/// В Диспетчере задач виден отдельно от дашборда как «Windows Activity Tracker - подсчёт».
/// </summary>
internal static class CounterProgram
{
    private static void Main()
    {
        using var mutex = new Mutex(true, CollectorControl.MutexName, out var first);
        if (!first)
            return; // счётчик уже работает

        // Выключен в «Автозагрузке» Диспетчера задач - не считаем вообще.
        if (AutostartManager.IsDisabledInStartup())
        {
            Logger.Info("WatCounter: отключён в Автозагрузке, не запускаюсь");
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
            using var watcher = new FileSystemWatcher(AppPaths.DataDirectory, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            watcher.Changed += (_, _) => { Thread.Sleep(150); collector.ReloadConfig(); };

            // Если выключат в «Автозагрузке» - счётчик сам останавливается.
            // Заодно раз в минуту отдаём неиспользуемую память ОС (меньше в Диспетчере).
            var ticks = 0;
            using var stopTimer = new Timer(_ =>
            {
                if (AutostartManager.IsDisabledInStartup())
                {
                    Logger.Info("WatCounter остановлен: выключен в Автозагрузке");
                    Environment.Exit(0);
                }
                if (++ticks % 4 == 0)
                    TrimMemory();
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            GC.Collect();
            TrimMemory();
            Logger.Info("WatCounter запущен (счётчик, без UI)");
            new ManualResetEvent(false).WaitOne();
        }
        catch (Exception ex)
        {
            Logger.Error("WatCounter упал", ex);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>Отдать неиспользуемые страницы ОС - снижает потребление в Диспетчере.</summary>
    private static void TrimMemory()
    {
        try { EmptyWorkingSet(GetCurrentProcess()); } catch { }
    }
}
