using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using WinActivityTracker.Data;
using WinActivityTracker.Tracking;

namespace WinActivityTracker.Ui;

/// <summary>Строка для списка приложений/игр в стиле Диспетчера задач.</summary>
public sealed class ActivityRow
{
    public string AppName { get; init; } = "";
    public string Process { get; init; } = "";
    public string CategoryLabel { get; init; } = "";
    public string TimeText { get; init; } = "";
    public bool IsGame { get; init; }

    /// <summary>Насыщенность «тепловой» заливки ячейки времени, 0..1.</summary>
    public double HeatOpacity { get; init; }

    /// <summary>Цвет точки категории.</summary>
    public Brush PillBrush { get; init; } = new SolidColorBrush(Colors.Gray);

    /// <summary>Иконка приложения (может быть null, если не извлеклась).</summary>
    public ImageSource? IconSource { get; init; }

    public static readonly Color GameColor = Color.FromArgb(255, 160, 130, 245);
    public static readonly Color AppColor = Color.FromArgb(255, 76, 194, 255);

    public static ActivityRow From(ProcessTotal p, long maxSeconds)
    {
        var isGame = p.Category == "Game";
        var frac = maxSeconds > 0 ? (double)p.Seconds / maxSeconds : 0;

        ImageSource? icon = null;
        var iconPath = IconCache.PathFor(p.Process);
        if (File.Exists(iconPath))
        {
            try { icon = new BitmapImage(new Uri(iconPath)); } catch { }
        }

        return new ActivityRow
        {
            AppName = p.AppName,
            Process = p.Process,
            CategoryLabel = isGame ? "Игра" : "Приложение",
            IsGame = isGame,
            TimeText = TimeFormat.Human(p.Seconds),
            HeatOpacity = 0.10 + 0.42 * frac,
            PillBrush = new SolidColorBrush(isGame ? GameColor : AppColor),
            IconSource = icon,
        };
    }
}
