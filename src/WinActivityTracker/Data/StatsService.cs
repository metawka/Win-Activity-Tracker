namespace WinActivityTracker.Data;

public enum RangeKind
{
    Today,
    Week,
    Month,
    All,
    Custom,
}

/// <summary>Готовая сводка за период для страниц дашборда.</summary>
public sealed class RangeStats
{
    public required RangeKind Range { get; init; }
    public required string Label { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<ProcessTotal> Processes { get; init; }
    public required IReadOnlyList<HourlyBucket> Hourly { get; init; }
    public long GameSeconds { get; init; }
    public long AppSeconds { get; init; }
    public long TotalSeconds => GameSeconds + AppSeconds;

    /// <summary>Время работы экрана (любое окно, кроме блокировки/сна).</summary>
    public long ScreenSeconds { get; init; }
}

/// <summary>Считает сводки из базы. Один экземпляр на приложение.</summary>
public sealed class StatsService
{
    private readonly Database _db;

    public StatsService(Database db) => _db = db;

    /// <summary>Границы своего периода (from-to из календаря).</summary>
    public static DateOnly CustomFrom { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public static DateOnly CustomTo { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    public static (DateOnly from, DateOnly to, string label) Resolve(RangeKind range)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return range switch
        {
            RangeKind.Week => (today.AddDays(-6), today, "Неделя"),
            RangeKind.Month => (today.AddDays(-29), today, "Месяц"),
            RangeKind.All => (new DateOnly(2000, 1, 1), today, "Всё время"),
            RangeKind.Custom => (
                CustomFrom <= CustomTo ? CustomFrom : CustomTo,
                CustomFrom <= CustomTo ? CustomTo : CustomFrom,
                "Период"),
            _ => (today, today, "Сегодня"),
        };
    }

    public RangeStats Get(RangeKind range)
    {
        var (from, to, label) = Resolve(range);
        var processes = _db.GetTotals(from, to);
        var hourly = _db.GetHourly(DateOnly.FromDateTime(DateTime.Now));

        return new RangeStats
        {
            Range = range,
            Label = label,
            From = from,
            To = to,
            Processes = processes,
            Hourly = hourly,
            GameSeconds = processes.Where(p => p.Category == "Game").Sum(p => p.Seconds),
            AppSeconds = processes.Where(p => p.Category != "Game").Sum(p => p.Seconds),
            ScreenSeconds = _db.GetScreenSeconds(from, to),
        };
    }
}
