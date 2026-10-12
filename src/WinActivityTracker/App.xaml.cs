using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinActivityTracker.Autostart;
using WinActivityTracker.Config;
using WinActivityTracker.Data;
using WinActivityTracker.Tracking;

namespace WinActivityTracker;

public partial class App : Application
{
    public static AppConfig Config { get; private set; } = null!;
    public static Database Db { get; private set; } = null!;
    public static StatsService Stats { get; private set; } = null!;
    public static RangeKind CurrentRange { get; set; } = RangeKind.Today;

    private TaskbarIcon? _tray;
    private MainWindow? _window;
    private DispatcherQueue? _dispatcher;
    private ToggleMenuFlyoutItem? _autostartItem;

    public App() => this.InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Дашборд только читает базу. Счёт ведёт отдельный лёгкий процесс (--collector).
        Config = AppConfig.Load();
        Db = new Database();
        Db.Initialize();
        Stats = new StatsService(Db);

        BuildTray();
        Logger.Info("Дашборд запущен");
        ShowMainWindow();
    }

    /// <summary>Обновить текущую страницу окна (вызывается из контекстных действий).</summary>
    public void RequestRefresh()
    {
        void Refresh() => _window?.RefreshCurrent();
        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
            _dispatcher.TryEnqueue(Refresh);
        else
            Refresh();
    }

    private void BuildTray()
    {
        var menu = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Открыть дашборд" };
        open.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(open);

        menu.Items.Add(new MenuFlyoutSeparator());

        _autostartItem = new ToggleMenuFlyoutItem
        {
            Text = "Запускать со входом в Windows",
            IsChecked = AutostartManager.IsEnabled(),
        };
        _autostartItem.Click += OnToggleAutostart;
        menu.Items.Add(_autostartItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var exit = new MenuFlyoutItem { Text = "Выход" };
        exit.Click += (_, _) => ExitApp();
        menu.Items.Add(exit);

        _tray = new TaskbarIcon
        {
            ToolTipText = "Трекер активности",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            IconSource = TryLoadAppIcon(),
        };
        _tray.LeftClickCommand = new RelayCommand(ShowMainWindow);
        _tray.ForceCreate();
    }

    public void ShowMainWindow()
    {
        void Show()
        {
            if (_window == null)
            {
                _window = new MainWindow();
                _window.Closed += (_, _) => _window = null;
            }
            _window.Activate();
        }

        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
            _dispatcher.TryEnqueue(Show);
        else
            Show();
    }

    private void OnToggleAutostart(object sender, RoutedEventArgs e)
    {
        var enabled = AutostartManager.IsEnabled();
        var ok = enabled ? AutostartManager.Disable() : AutostartManager.Enable();
        if (_autostartItem != null)
            _autostartItem.IsChecked = ok ? !enabled : enabled;
    }

    public static string AppIconPath => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? TryLoadAppIcon()
    {
        try
        {
            if (System.IO.File.Exists(AppIconPath))
                return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(AppIconPath));
        }
        catch (Exception ex)
        {
            Logger.Error("Иконку трея загрузить не удалось", ex);
        }
        return null;
    }

    private void ExitApp()
    {
        // Закрываем только дашборд. Счётчик - отдельный процесс, продолжает считать.
        Logger.Info("Выход из дашборда");
        _tray?.Dispose();
        _window?.Close();
        Environment.Exit(0);
    }
}

/// <summary>Простейшая ICommand для клика по иконке трея.</summary>
public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _action;
    public RelayCommand(Action action) => _action = action;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
