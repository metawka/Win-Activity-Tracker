using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using WinActivityTracker.Data;
using WinActivityTracker.Tracking;

namespace WinActivityTracker.Ui;

/// <summary>Строка списка сеансов.</summary>
public sealed class SessionRow
{
    public string AppName { get; init; } = "";
    public string WhenText { get; init; } = "";
    public string DurationText { get; init; } = "";
    public string CategoryLabel { get; init; } = "";
    public Brush PillBrush { get; init; } = new SolidColorBrush(Colors.Gray);
    public ImageSource? IconSource { get; init; }

    public static SessionRow From(SessionRecord s)
    {
        var isGame = s.Category == "Game";
        ImageSource? icon = null;
        var iconPath = IconCache.PathFor(s.Process);
        if (File.Exists(iconPath))
        {
            try { icon = new BitmapImage(new Uri(iconPath)); } catch { }
        }

        var today = DateTime.Now.Date;
        var startPart = s.Start.Date == today ? s.Start.ToString("HH:mm") : s.Start.ToString("dd.MM HH:mm");
        var when = $"{startPart} - {s.End:HH:mm}";

        return new SessionRow
        {
            AppName = s.AppName,
            WhenText = when,
            DurationText = TimeFormat.Human(s.ActiveSeconds),
            CategoryLabel = isGame ? "Игра" : "Приложение",
            PillBrush = new SolidColorBrush(isGame ? ActivityRow.GameColor : ActivityRow.AppColor),
            IconSource = icon,
        };
    }
}
