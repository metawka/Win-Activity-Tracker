using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Data;
using WinActivityTracker.Ui;

namespace WinActivityTracker.Pages;

public sealed partial class AppsPage : Page, IRangePage
{
    public AppsPage() => this.InitializeComponent();

    public void Load(RangeKind range)
    {
        var stats = App.Stats.Get(range);
        var apps = stats.Processes.Where(p => p.Category != "Game").ToList();
        GridView.SetItems(apps);
    }
}
