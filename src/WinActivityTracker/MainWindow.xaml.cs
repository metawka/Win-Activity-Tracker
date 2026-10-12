using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinActivityTracker.Data;
using WinActivityTracker.Pages;
using WinActivityTracker.Ui;

namespace WinActivityTracker;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Трекер активности";

        // Mica на всё окно: заголовок, рельс и контент - единый фон.
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            // Размер окна не трогаем: пусть открывается в своём размере без рывка.
            if (System.IO.File.Exists(App.AppIconPath))
                AppWindow.SetIcon(App.AppIconPath);
        }
        catch { }

        // Стартовая страница.
        Navigate("overview");

        // Живое обновление, как в Диспетчере задач.
        Activated += (_, _) => { RefreshCurrent(); UpdateCollectorBanner(); };
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => { RefreshCurrent(); UpdateCollectorBanner(); };
        timer.Start();

        UpdateCollectorBanner();
    }

    private void UpdateCollectorBanner()
    {
        CollectorBar.IsOpen = !WinActivityTracker.Tracking.CollectorControl.IsRunning();
    }

    private void EnableCollector_Click(object sender, RoutedEventArgs e)
    {
        WinActivityTracker.Tracking.CollectorControl.Start();
        WinActivityTracker.Autostart.AutostartManager.Enable();
        CollectorBar.IsOpen = false;
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            Navigate("settings");
            return;
        }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            Navigate(tag);
    }

    private void Range_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var tag = sender.SelectedItem?.Tag as string;
        App.CurrentRange = tag switch
        {
            "week" => RangeKind.Week,
            "month" => RangeKind.Month,
            "all" => RangeKind.All,
            "custom" => RangeKind.Custom,
            _ => RangeKind.Today,
        };

        var isCustom = tag == "custom";
        CustomRangePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        if (isCustom)
        {
            // Инициализируем календари при первом открытии.
            var today = DateTimeOffset.Now;
            FromPicker.Date ??= today.AddDays(-6);
            ToPicker.Date ??= today;
            ApplyCustomRange();
        }
        RefreshCurrent();
    }

    private void CustomDate_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        ApplyCustomRange();
        if (App.CurrentRange == RangeKind.Custom)
            RefreshCurrent();
    }

    private void ApplyCustomRange()
    {
        if (FromPicker.Date is DateTimeOffset f)
            StatsService.CustomFrom = DateOnly.FromDateTime(f.Date);
        if (ToPicker.Date is DateTimeOffset t)
            StatsService.CustomTo = DateOnly.FromDateTime(t.Date);
    }

    private void Navigate(string tag)
    {
        var (type, title) = tag switch
        {
            "apps" => (typeof(AppsPage), "Приложения"),
            "games" => (typeof(GamesPage), "Игры"),
            "hourly" => (typeof(HourlyPage), "По часам"),
            "sessions" => (typeof(SessionsPage), "Сеансы"),
            "overview" => (typeof(OverviewPage), "Обзор"),
            "settings" => (typeof(SettingsPage), "Настройки"),
            _ => (typeof(ProcessesPage), "Процессы"),
        };

        if (HeaderTitle != null)
            HeaderTitle.Text = title;

        // На «Настройках» и «По часам» глобальный период не нужен: у настроек его нет,
        // а «По часам» листает дни своим навигатором.
        var hideRange = tag is "settings" or "hourly";
        if (RangeBar != null)
            RangeBar.Visibility = hideRange ? Visibility.Collapsed : Visibility.Visible;
        if (CustomRangePanel != null && hideRange)
            CustomRangePanel.Visibility = Visibility.Collapsed;

        if (ContentFrame.Content?.GetType() != type)
            ContentFrame.Navigate(type);

        RefreshCurrent();
    }

    public void RefreshCurrent()
    {
        if (ContentFrame.Content is IRangePage page)
            page.Load(App.CurrentRange);
    }
}
