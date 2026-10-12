namespace WinActivityTracker.Ui;

public static class TimeFormat
{
    /// <summary>Секунды в человекочитаемую строку: «2 ч 05 мин», «12 мин», «34 сек».</summary>
    public static string Human(long seconds)
    {
        if (seconds < 0) seconds = 0;
        var h = seconds / 3600;
        var m = (seconds % 3600) / 60;
        if (h > 0) return $"{h} ч {m:00} мин";
        if (m > 0) return $"{m} мин";
        return $"{seconds} сек";
    }
}
