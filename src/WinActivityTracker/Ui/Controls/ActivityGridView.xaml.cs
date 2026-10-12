using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinActivityTracker.Data;

namespace WinActivityTracker.Ui.Controls;

public sealed partial class ActivityGridView : UserControl
{
    private IReadOnlyList<ProcessTotal> _items = Array.Empty<ProcessTotal>();
    private bool _descending = true;

    public ActivityGridView() => this.InitializeComponent();

    /// <summary>Заполнить список готовыми итогами (уже отфильтрованными по категории).</summary>
    public void SetItems(IReadOnlyList<ProcessTotal> items)
    {
        _items = items;
        Render();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        _descending = !_descending;
        SortCaret.Glyph = _descending ? "" : ""; // вниз / вверх
        Render();
    }

    private void Render()
    {
        var sorted = _descending
            ? _items.OrderByDescending(i => i.Seconds).ToList()
            : _items.OrderBy(i => i.Seconds).ToList();

        long max = sorted.Count > 0 ? sorted.Max(i => i.Seconds) : 0;
        var rows = sorted.Select(i => ActivityRow.From(i, max)).ToList();
        List.ItemsSource = rows;

        var empty = rows.Count == 0;
        EmptyLabel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // Правый клик по строке: полезные действия над процессом.
    private void List_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not ActivityRow row)
            return;

        var menu = new MenuFlyout();

        var copy = new MenuFlyoutItem { Text = "Копировать имя процесса" };
        copy.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(row.Process);
            Clipboard.SetContent(dp);
        };
        menu.Items.Add(copy);

        menu.Items.Add(new MenuFlyoutSeparator());

        if (row.IsGame)
        {
            var asApp = new MenuFlyoutItem { Text = "Считать приложением" };
            asApp.Click += (_, _) => Reclassify(row.Process, toGame: false);
            menu.Items.Add(asApp);
        }
        else
        {
            var asGame = new MenuFlyoutItem { Text = "Считать игрой" };
            asGame.Click += (_, _) => Reclassify(row.Process, toGame: true);
            menu.Items.Add(asGame);
        }

        var ignore = new MenuFlyoutItem { Text = "Не учитывать это приложение" };
        ignore.Click += (_, _) => IgnoreProcess(row.Process);
        menu.Items.Add(ignore);

        menu.ShowAt(List, new FlyoutShowOptions { Position = e.GetPosition(List) });
    }

    private static void Reclassify(string process, bool toGame)
    {
        var cfg = App.Config;
        if (toGame)
        {
            cfg.ForceGameProcesses.Add(process);
            cfg.NeverGameProcesses.Remove(process);
        }
        else
        {
            cfg.NeverGameProcesses.Add(process);
            cfg.ForceGameProcesses.Remove(process);
        }
        cfg.Save();
        App.Db.SetCategory(process, toGame ? "Game" : "Application");
        (App.Current as App)?.RequestRefresh();
    }

    private static void IgnoreProcess(string process)
    {
        App.Config.IgnoredProcesses.Add(process);
        App.Config.Save();
        App.Db.DeleteProcess(process);
        (App.Current as App)?.RequestRefresh();
    }
}
