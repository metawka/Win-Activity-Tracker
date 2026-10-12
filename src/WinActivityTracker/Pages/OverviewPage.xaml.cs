using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class OverviewPage : Page, IRangePage
{
    public OverviewPage() => this.InitializeComponent();

    public void Load(RangeKind range)
    {
        var s = App.Stats.Get(range);

        var period = range switch
        {
            RangeKind.Week => "неделю",
            RangeKind.Month => "месяц",
            RangeKind.All => "всё время",
            RangeKind.Custom => "выбранный период",
            _ => "сегодня",
        };
        ScreenLabel.Text = "Время работы экрана за " + period;
        ScreenText.Text = TimeFormat.Human(s.ScreenSeconds);
        TotalText.Text = TimeFormat.Human(s.TotalSeconds);
        GameText.Text = TimeFormat.Human(s.GameSeconds);
        AppText.Text = TimeFormat.Human(s.AppSeconds);

        // Полоса соотношения.
        if (s.TotalSeconds > 0)
        {
            ColGame.Width = new GridLength(s.GameSeconds, GridUnitType.Star);
            ColApp.Width = new GridLength(s.AppSeconds, GridUnitType.Star);
            var gamePct = (int)Math.Round(100.0 * s.GameSeconds / s.TotalSeconds);
            SplitText.Text = $"Игры {gamePct}%  ·  Приложения {100 - gamePct}%";
        }
        else
        {
            ColGame.Width = new GridLength(0, GridUnitType.Star);
            ColApp.Width = new GridLength(1, GridUnitType.Star);
            SplitText.Text = "Пока нет данных за этот период";
        }

        // Топ по времени (все категории).
        TopGrid.SetItems(s.Processes.Take(8).ToList());
    }
}
