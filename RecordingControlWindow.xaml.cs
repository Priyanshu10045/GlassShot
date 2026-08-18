using System;
using System.Windows;
using System.Windows.Threading;

namespace GlassShot;

public partial class RecordingControlWindow : Window
{
    private DispatcherTimer _timer;
    private DateTime _startTime;

    public Action? OnStopRequested;

    public RecordingControlWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => 
        {
            var elapsed = DateTime.Now - _startTime;
            TimerText.Text = elapsed.ToString(@"mm\:ss");
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _startTime = DateTime.Now;
        _timer.Start();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        OnStopRequested?.Invoke();
    }
}
