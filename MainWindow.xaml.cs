using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NHotkey;
using NHotkey.Wpf;

namespace GlassShot;

public partial class MainWindow : Window
{
    private Point? _dragStart;
    private Rect _currentSelection;
    private bool _isDragging;

    private enum CaptureMode { Image, Video, Text }
    private CaptureMode _currentMode = CaptureMode.Image;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();
        
        SettingsManager.Load();
        RegisterHotkeys();
        SettingsManager.OnSettingsChanged += RegisterHotkeys;

        InitializeTrayIcon();

        this.Loaded += (s, e) => this.Hide();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "GlassShot - CleanShot X for Windows",
                Visible = true
            };

            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (File.Exists(exePath))
                    {
                        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
                    }
                    else
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            var menu = new System.Windows.Forms.ContextMenuStrip();
            
            var captureItem = new System.Windows.Forms.ToolStripMenuItem("Capture Area\t(Ctrl+Shift+C)", null, (s, e) => InitializeCaptureOverlay(CaptureMode.Image));
            var recordItem = new System.Windows.Forms.ToolStripMenuItem("Record Screen\t(Ctrl+Shift+V)", null, (s, e) => InitializeCaptureOverlay(CaptureMode.Video));
            var ocrItem = new System.Windows.Forms.ToolStripMenuItem("Capture Text\t(Ctrl+Shift+T)", null, (s, e) => InitializeCaptureOverlay(CaptureMode.Text));
            var prefsItem = new System.Windows.Forms.ToolStripMenuItem("Preferences...", null, (s, e) => SettingsWindow.Instance.Show());
            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Quit GlassShot", null, (s, e) => 
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                Application.Current.Shutdown();
            });

            menu.Items.Add(captureItem);
            menu.Items.Add(recordItem);
            menu.Items.Add(ocrItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(prefsItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => SettingsWindow.Instance.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize tray icon: {ex.Message}");
        }
    }

    private void RegisterHotkeys()
    {
        try
        {
            RegisterSingleHotkey("CaptureArea", SettingsManager.Current.CaptureAreaShortcut, OnCaptureHotkey);
            RegisterSingleHotkey("CaptureFullscreen", SettingsManager.Current.CaptureFullscreenShortcut, OnFullscreenHotkey);
            RegisterSingleHotkey("RecordVideo", SettingsManager.Current.RecordVideoShortcut, OnRecordHotkey);
            RegisterSingleHotkey("CaptureText", SettingsManager.Current.CaptureTextShortcut, OnTextHotkey);
            RegisterSingleHotkey("ToggleDesktopIcons", SettingsManager.Current.ToggleDesktopIconsShortcut, OnToggleIconsHotkey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkeys: {ex.Message}");
        }
    }

    private void RegisterSingleHotkey(string name, string shortcut, EventHandler<HotkeyEventArgs> handler)
    {
        try
        {
            var (key, modifiers) = ParseShortcut(shortcut);
            if (key != Key.None)
            {
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, handler);
            }
        }
        catch { }
    }

    private static (Key key, ModifierKeys modifiers) ParseShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return (Key.None, ModifierKeys.None);

        ModifierKeys mods = ModifierKeys.None;
        Key key = Key.None;

        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Alt;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Windows;
            else
            {
                if (Enum.TryParse<Key>(part, true, out var parsedKey))
                {
                    key = parsedKey;
                }
            }
        }
        return (key, mods);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.OnClosed(e);
    }

    private void OnCaptureHotkey(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        InitializeCaptureOverlay(CaptureMode.Image);
    }

    private void OnFullscreenHotkey(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        var capturedScreen = ScreenCapturer.CaptureVirtualScreen();
        QuickAccessManagerWindow.Instance.AddCapture(capturedScreen);
    }

    private void OnTextHotkey(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        InitializeCaptureOverlay(CaptureMode.Text);
    }

    private void OnToggleIconsHotkey(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        DesktopIconManager.ToggleDesktopIcons();
    }

    private async void OnRecordHotkey(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        if (!System.IO.File.Exists(FFmpegManager.FFmpegPath))
        {
            MessageBox.Show("GlassShot is downloading the FFmpeg recording engine... This will only happen once.", "GlassShot Setup");
            await FFmpegManager.EnsureReadyAsync();
        }
        InitializeCaptureOverlay(CaptureMode.Video);
    }

    private void InitializeCaptureOverlay(CaptureMode mode)
    {
        if (this.Visibility == Visibility.Visible) return;

        _currentMode = mode;

        var capturedScreen = ScreenCapturer.CaptureVirtualScreen();
        ScreenBackground.Source = capturedScreen;
        MagnifierBrush.ImageSource = capturedScreen;

        this.Left = SystemParameters.VirtualScreenLeft;
        this.Top = SystemParameters.VirtualScreenTop;
        this.Width = SystemParameters.VirtualScreenWidth;
        this.Height = SystemParameters.VirtualScreenHeight;
        
        UpdateOverlayGeometry(new Rect(0,0,0,0));
        SelectionBorder.Visibility = Visibility.Hidden;

        SelectionBorder.Stroke = _currentMode == CaptureMode.Video 
            ? System.Windows.Media.Brushes.Red 
            : _currentMode == CaptureMode.Text 
                ? System.Windows.Media.Brushes.Orange 
                : System.Windows.Media.Brushes.White;

        this.Show();
        this.Activate();
        this.Focus();
        Keyboard.Focus(this);
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelCapture();
    }

    private void CancelCapture()
    {
        this.Hide();
        _isDragging = false;
        SelectionBorder.Visibility = Visibility.Hidden;
        Magnifier.Visibility = Visibility.Hidden;
        ScreenBackground.Source = null;
        MagnifierBrush.ImageSource = null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        
        // Update Magnifier
        Magnifier.Visibility = Visibility.Visible;
        // Position the magnifier slightly offset from cursor
        Magnifier.Margin = new Thickness(pos.X + 15, pos.Y + 15, 0, 0); 
        
        var source = ScreenBackground.Source as BitmapSource;
        if (source != null && this.ActualWidth > 0 && this.ActualHeight > 0)
        {
            double scaleX = source.Width / this.ActualWidth;
            double scaleY = source.Height / this.ActualHeight;

            double imageX = pos.X * scaleX;
            double imageY = pos.Y * scaleY;

            double sampleWidth = 20 * scaleX;
            double sampleHeight = 20 * scaleY;

            MagnifierBrush.Viewbox = new Rect(imageX - (sampleWidth / 2), imageY - (sampleHeight / 2), sampleWidth, sampleHeight);
        }

        if (_isDragging && _dragStart.HasValue)
        {
            var x = Math.Min(pos.X, _dragStart.Value.X);
            var y = Math.Min(pos.Y, _dragStart.Value.Y);
            var w = Math.Abs(pos.X - _dragStart.Value.X);
            var h = Math.Abs(pos.Y - _dragStart.Value.Y);
            _currentSelection = new Rect(x, y, w, h);
            
            UpdateOverlayGeometry(_currentSelection);
            
            SelectionBorder.Visibility = Visibility.Visible;
            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            SelectionBorder.Visibility = Visibility.Visible;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.LeftButton == MouseButtonState.Released && _isDragging)
        {
            _isDragging = false;
            Magnifier.Visibility = Visibility.Hidden;
            
            if (_currentSelection.Width > 10 && _currentSelection.Height > 10)
            {
                // Capture finalized!
                FinalizeCapture(_currentSelection);
            }
            else
            {
                // Clicked without dragging, cancel selection
                SelectionBorder.Visibility = Visibility.Hidden;
                UpdateOverlayGeometry(new Rect(0,0,0,0));
            }
        }
    }

    private void UpdateOverlayGeometry(Rect excludeRegion)
    {
        var screenGeo = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, this.Width > 0 ? this.Width : SystemParameters.VirtualScreenWidth, this.Height > 0 ? this.Height : SystemParameters.VirtualScreenHeight));
        if (excludeRegion.Width > 0 && excludeRegion.Height > 0)
        {
            var excludeGeo = new System.Windows.Media.RectangleGeometry(excludeRegion);
            OverlayPath.Data = new System.Windows.Media.CombinedGeometry(System.Windows.Media.GeometryCombineMode.Exclude, screenGeo, excludeGeo);
        }
        else
        {
            OverlayPath.Data = screenGeo;
        }
    }

    private void FinalizeCapture(Rect region)
    {
        var source = (BitmapSource)ScreenBackground.Source;
        if (source != null && this.ActualWidth > 0 && this.ActualHeight > 0)
        {
            try
            {
                double scaleX = source.PixelWidth / this.ActualWidth;
                double scaleY = source.PixelHeight / this.ActualHeight;

                int cropX = (int)Math.Max(0, region.X * scaleX);
                int cropY = (int)Math.Max(0, region.Y * scaleY);
                int cropW = (int)Math.Min(source.PixelWidth - cropX, region.Width * scaleX);
                int cropH = (int)Math.Min(source.PixelHeight - cropY, region.Height * scaleY);

                if (cropW > 0 && cropH > 0)
                {
                    if (_currentMode == CaptureMode.Video)
                    {
                        // Pass absolute physical coordinates to FFmpeg
                        FFmpegManager.StartRecording(cropX, cropY, cropW, cropH);

                        var controlWindow = new RecordingControlWindow();
                        controlWindow.Left = SystemParameters.VirtualScreenLeft + region.X + (region.Width / 2) - 90;
                        controlWindow.Top = SystemParameters.VirtualScreenTop + region.Y + region.Height + 10;
                        
                        controlWindow.OnStopRequested = async () =>
                        {
                            controlWindow.Close();
                            string? mp4Path = await FFmpegManager.StopRecordingAsync();
                            if (mp4Path != null)
                            {
                                QuickAccessManagerWindow.Instance.AddVideo(mp4Path);
                            }
                        };
                        controlWindow.Show();
                    }
                    else if (_currentMode == CaptureMode.Text)
                    {
                        var crop = new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropW, cropH));
                        var layer = new ThumbnailLayer(crop);
                        layer.ExecuteOcrAndClose();
                    }
                    else
                    {
                        var crop = new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropW, cropH));
                        QuickAccessManagerWindow.Instance.AddCapture(crop);
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Failed to crop image: {ex.Message}");
            }
        }

        this.Hide();
        ScreenBackground.Source = null;
        MagnifierBrush.ImageSource = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            CancelCapture();
        }
    }
}
