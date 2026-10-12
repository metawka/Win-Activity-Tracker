namespace WinActivityTracker.Data;

/// <summary>Одна строка сводки по процессу за период.</summary>
public sealed record ProcessTotal(
    string Process,
    string AppName,
    string Category,
    long Seconds);

/// <summary>Сумма секунд за один час суток, разбитая по категориям.</summary>
public sealed record HourlyBucket(
    int Hour,
    long GameSeconds,
    long AppSeconds);

/// <summary>Один сеанс работы в приложении (с учётом склейки коротких отлучек).</summary>
public sealed record SessionRecord(
    string Process,
    string AppName,
    string Category,
    DateTime Start,
    DateTime End,
    long ActiveSeconds);
