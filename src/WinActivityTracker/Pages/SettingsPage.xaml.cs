using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinActivityTracker.Autostart;
using WinActivityTracker.Config;
using WinActivityTracker.Tracking;

namespace WinActivityTracker.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loading;

    public SettingsPage()
    {
        this.InitializeComponent();
        LoadState();
    }

    private void LoadState()
    {
        _loading = true;
        AutostartToggle.IsOn = AutostartManager.IsEnabled() && CollectorControl.IsRunning();
        IntervalBox.Value = App.Config.SampleIntervalSeconds;
        FullscreenToggle.IsOn = App.Config.TreatFullscreenAsGame;
        DataPathText.Text = AppPaths.DataDirectory;
        var v = typeof(App).Assembly.GetName().Version;
        VersionText.Text = v != null ? $"Версия {v}" : "";
        _loading = false;
    }

    private void Autostart_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (AutostartToggle.IsOn)
        {
            AutostartManager.Enable();
            CollectorControl.Start();
        }
        else
        {
            AutostartManager.Disable();
            CollectorControl.Stop();
        }
    }

    private void Interval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        var v = double.IsNaN(sender.Value) ? App.Config.SampleIntervalSeconds : (int)sender.Value;
        v = Math.Clamp(v, 1, 60);
        App.Config.SampleIntervalSeconds = v;
        App.Config.Save();
    }

    private void Fullscreen_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Config.TreatFullscreenAsGame = FullscreenToggle.IsOn;
        App.Config.Save();
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppPaths.DataDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось открыть папку данных", ex);
        }
    }
}
