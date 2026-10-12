using WinActivityTracker.Data;

namespace WinActivityTracker.Ui;

/// <summary>Страница дашборда, которая умеет перезагружаться под выбранный период.</summary>
public interface IRangePage
{
    void Load(RangeKind range);
}
