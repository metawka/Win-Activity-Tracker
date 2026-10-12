using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class HourlyPage : Page, IRangePage
{
    private static readonly Brush GameBrush = new SolidColorBrush(Color.FromArgb(255, 160, 130, 245));
    private static readonly Brush AppBrush = new SolidColorBrush(Color.FromArgb(255, 76, 194, 255));
    private const double BarAreaHeight = 190;

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Now);
    private bool _syncing;

    public HourlyPage() => this.InitializeComponent();

    // Глобальный период тут не используем - страница листает дни своим навигатором.
    public void Load(RangeKind range) => LoadDay();

    private void SetDate(DateOnly d)
    {
        _date = d;
        LoadDay();
    }

    private void Today_Click(object s, RoutedEventArgs e) => SetDate(DateOnly.FromDateTime(DateTime.Now));
    private void PrevDay_Click(object s, RoutedEventArgs e) => SetDate(_date.AddDays(-1));
    private void NextDay_Click(object s, RoutedEventArgs e) => SetDate(_date.AddDays(1));
    private void PrevWeek_Click(object s, RoutedEventArgs e) => SetDate(_date.AddDays(-7));
    private void NextWeek_Click(object s, RoutedEventArgs e) => SetDate(_date.AddDays(7));
    private void PrevMonth_Click(object s, RoutedEventArgs e) => SetDate(_date.AddMonths(-1));
    private void NextMonth_Click(object s, RoutedEventArgs e) => SetDate(_date.AddMonths(1));

    private void DayPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_syncing) return;
        if (sender.Date is DateTimeOffset d)
            SetDate(DateOnly.FromDateTime(d.Date));
    }

    private void LoadDay()
    {
        _syncing = true;
        DayPicker.Date = new DateTimeOffset(_date.ToDateTime(TimeOnly.MinValue));
        _syncing = false;

        var isToday = _date == DateOnly.FromDateTime(DateTime.Now);
        DayTitle.Text = isToday ? "Активность по часам (сегодня)"
                                : $"Активность по часам - {_date:dd.MM.yyyy}";

        var hourly = App.Db.GetHourly(_date);

        Plot.Children.Clear();
        Plot.ColumnDefinitions.Clear();
        Labels.Children.Clear();
        Labels.ColumnDefinitions.Clear();

        var maxMin = Math.Max(1.0, hourly.Max(h => (h.GameSeconds + h.AppSeconds) / 60.0));
        YMax.Text = $"макс {Math.Ceiling(maxMin)} мин/час";

        var totalSec = hourly.Sum(h => h.GameSeconds + h.AppSeconds);
        var active = hourly.Count(h => (h.GameSeconds + h.AppSeconds) > 0);
        StatTotal.Text = TimeFormat.Human(totalSec);
        StatActive.Text = $"{active} из 24";
        if (totalSec > 0)
        {
            var pk = hourly.OrderByDescending(h => h.GameSeconds + h.AppSeconds).First();
            StatPeak.Text = $"{pk.Hour:00}:00 · {TimeFormat.Human(pk.GameSeconds + pk.AppSeconds)}";
            var firstH = hourly.First(h => (h.GameSeconds + h.AppSeconds) > 0).Hour;
            var lastH = hourly.Last(h => (h.GameSeconds + h.AppSeconds) > 0).Hour;
            StatRange.Text = firstH == lastH ? $"{firstH:00}:00" : $"{firstH:00}-{lastH:00}";
        }
        else
        {
            StatPeak.Text = "-";
            StatRange.Text = "-";
        }

        foreach (var h in hourly)
        {
            Plot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bars = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Bottom };
            var gameMin = h.GameSeconds / 60.0;
            var appMin = h.AppSeconds / 60.0;

            double Scale(double m) => m <= 0 ? 0 : Math.Max(3, BarAreaHeight * m / maxMin);

            if (gameMin > 0)
                bars.Children.Add(new Rectangle { Height = Scale(gameMin), Fill = GameBrush, RadiusX = 3, RadiusY = 3, Margin = new Thickness(0, 0, 0, appMin > 0 ? 1 : 0) });
            if (appMin > 0)
                bars.Children.Add(new Rectangle { Height = Scale(appMin), Fill = AppBrush, RadiusX = 3, RadiusY = 3 });

            // Кликабельная область на весь столбец + подсказка при наведении.
            var cell = new Border { Background = new SolidColorBrush(Colors.Transparent), Child = bars, VerticalAlignment = VerticalAlignment.Stretch };
            var totalM = (h.GameSeconds + h.AppSeconds) / 60.0;
            var tip = totalM <= 0
                ? $"{h.Hour:00}:00 - нет активности"
                : $"{h.Hour:00}:00\nВсего: {TimeFormat.Human(h.GameSeconds + h.AppSeconds)}\nИгры: {TimeFormat.Human(h.GameSeconds)}\nПриложения: {TimeFormat.Human(h.AppSeconds)}";
            ToolTipService.SetToolTip(cell, tip);

            Grid.SetColumn(cell, h.Hour);
            Plot.Children.Add(cell);

            if (h.Hour % 2 == 0)
            {
                var label = new TextBlock
                {
                    Text = h.Hour.ToString("00"),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                };
                Grid.SetColumn(label, h.Hour);
                Labels.Children.Add(label);
            }
        }
    }
}
