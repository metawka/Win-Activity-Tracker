using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class ProcessesPage : Page, IRangePage
{
    public ProcessesPage() => this.InitializeComponent();

    public void Load(RangeKind range)
    {
        var stats = App.Stats.Get(range);

        var period = range switch
        {
            RangeKind.Week => "неделю",
            RangeKind.Month => "месяц",
            RangeKind.All => "всё время",
            RangeKind.Custom => "выбранный период",
            _ => "сегодня",
        };
        SummaryLabel.Text = "Время работы экрана за " + period;
        SummaryTotal.Text = TimeFormat.Human(stats.ScreenSeconds);
        SummaryGames.Text = "Игры " + TimeFormat.Human(stats.GameSeconds);
        SummaryApps.Text = "Приложения " + TimeFormat.Human(stats.AppSeconds);

        GridView.SetItems(stats.Processes);
    }
}
