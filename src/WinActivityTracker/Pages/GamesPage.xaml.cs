using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class GamesPage : Page, IRangePage
{
    public GamesPage() => this.InitializeComponent();

    public void Load(RangeKind range)
    {
        var stats = App.Stats.Get(range);
        var games = stats.Processes.Where(p => p.Category == "Game").ToList();
        GridView.SetItems(games);
    }
}
