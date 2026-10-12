namespace WinActivityTracker.Tracking;

/// <summary>Снимок переднего окна на момент опроса.</summary>
public sealed record ForegroundWindowInfo(
    string ProcessName,   // имя exe в нижнем регистре, напр. "cs2.exe"
    string ExecutablePath, // полный путь, может быть пустым
    string AppName,       // человекочитаемое имя (из версии файла или exe)
    string WindowTitle,
    bool IsFullscreen);
