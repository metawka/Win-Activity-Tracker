using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class SessionsPage : Page, IRangePage
{
    public SessionsPage() => this.InitializeComponent();

    public void Load(RangeKind range)
    {
        var (from, to, _) = StatsService.Resolve(range);
        var sessions = App.Db.GetSessions(from, to);

        SummaryLabel.Text = "Сеансов за " + range switch
        {
            RangeKind.Week => "неделю",
            RangeKind.Month => "месяц",
            RangeKind.All => "всё время",
            RangeKind.Custom => "выбранный период",
            _ => "сегодня",
        };
        SummaryCount.Text = sessions.Count.ToString();

        var rows = sessions.Select(SessionRow.From).ToList();
        List.ItemsSource = rows;

        var empty = rows.Count == 0;
        EmptyLabel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }
}
