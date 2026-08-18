using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using NHotkey;
using NHotkey.Wpf;

namespace GlassShot;

public partial class QuickAccessWindow : Window
{
    private BitmapSource? _capture;
    private string _tempFilePath;
    private bool _isClickThrough = false;
    private string? _videoFilePath;
    
    private static int _activeWindows = 0;
    private int _windowIndex;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020L;
    private const long WS_EX_LAYERED = 0x00080000L;

    public QuickAccessWindow(BitmapSource capture)
    {
        InitializeComponent();
        _capture = capture;
        ThumbnailImage.Source = _capture;

        _windowIndex = _activeWindows++;
        
        this.Left = SystemParameters.WorkArea.Right - this.Width - 20;
        
        double topPos = SystemParameters.WorkArea.Bottom - this.Height - 20 - (_windowIndex * (this.Height + 10));
        if (topPos < SystemParameters.WorkArea.Top)
        {
            _activeWindows = 1;
            _windowIndex = 0;
            topPos = SystemParameters.WorkArea.Bottom - this.Height - 20;
        }
        this.Top = topPos;

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"GlassShot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        SaveBitmapSourceToFile(_capture, _tempFilePath);

        RegisterHotkeys();
    }

    public QuickAccessWindow(string videoPath)
    {
        InitializeComponent();
        _videoFilePath = videoPath;
        _tempFilePath = videoPath; // The dragged file is the actual video

        _windowIndex = _activeWindows++;
        
        this.Left = SystemParameters.WorkArea.Right - this.Width - 20;
        
        double topPos = SystemParameters.WorkArea.Bottom - this.Height - 20 - (_windowIndex * (this.Height + 10));
        if (topPos < SystemParameters.WorkArea.Top)
        {
            _activeWindows = 1;
            _windowIndex = 0;
            topPos = SystemParameters.WorkArea.Bottom - this.Height - 20;
        }
        this.Top = topPos;

        LoadVideoThumbnailAsync(videoPath);
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        try
        {
            HotkeyManager.Current.AddOrReplace("Unpin", Key.U, ModifierKeys.Alt | ModifierKeys.Shift, (s, e) => {
                if (_isClickThrough) ToggleClickThrough();
            });
        }
        catch { }
    }

    private async void LoadVideoThumbnailAsync(string path)
    {
        var bmp = await FFmpegManager.GetVideoThumbnailAsync(path);
        if (bmp != null)
        {
            ThumbnailImage.Source = bmp;
            _capture = bmp;
            
            PlayIcon.Visibility = Visibility.Visible;
            PlayTriangle.Visibility = Visibility.Visible;
            GifButton.Visibility = Visibility.Visible;
            
            EditButton.Visibility = Visibility.Collapsed;
            TextButton.Visibility = Visibility.Collapsed;
            PinButton.Visibility = Visibility.Collapsed;
        }
    }

    private void SaveBitmapSourceToFile(BitmapSource source, string filePath)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(fileStream);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && !_isClickThrough)
        {
            // Initiate Drag Drop
            var dataObject = new DataObject(DataFormats.FileDrop, new string[] { _tempFilePath });
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
        }
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Adjust Opacity
        if (e.Delta > 0)
            this.Opacity = Math.Min(1.0, this.Opacity + 0.1);
        else
            this.Opacity = Math.Max(0.2, this.Opacity - 0.1);
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double click to minimize/close widget
        this.Close();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isClickThrough)
        {
            ActionPanel.Visibility = Visibility.Visible;
            DragHint.Visibility = Visibility.Visible;
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        ActionPanel.Visibility = Visibility.Hidden;
        DragHint.Visibility = Visibility.Hidden;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_videoFilePath != null)
        {
            var fileDropList = new System.Collections.Specialized.StringCollection { _videoFilePath };
            Clipboard.SetFileDropList(fileDropList);
            MessageBox.Show("Video file copied to clipboard!", "GlassShot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (_capture != null)
        {
            Clipboard.SetImage(_capture);
            MessageBox.Show("Copied to clipboard!", "GlassShot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void Gif_Click(object sender, RoutedEventArgs e)
    {
        if (_videoFilePath == null) return;
        
        GifButton.Content = "Processing...";
        GifButton.IsEnabled = false;

        try
        {
            string gifPath = await FFmpegManager.CreateGifAsync(_videoFilePath);
            
            _videoFilePath = gifPath;
            _tempFilePath = gifPath;
            
            GifButton.Visibility = Visibility.Collapsed;
            PlayIcon.Visibility = Visibility.Collapsed;
            PlayTriangle.Visibility = Visibility.Collapsed;
            
            MessageBox.Show("GIF created successfully! You can drag and drop it now.", "GlassShot");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create GIF: {ex.Message}");
            GifButton.Content = "GIF";
            GifButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = Path.GetFileName(_tempFilePath),
            DefaultExt = ".png",
            Filter = "PNG Image (.png)|*.png"
        };

        if (dlg.ShowDialog() == true)
        {
            File.Copy(_tempFilePath, dlg.FileName, true);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_capture == null) return;
        var annotator = new AnnotationWindow(_capture);
        
        annotator.OnSaveCompleted += (annotatedImage) =>
        {
            _capture = annotatedImage;
            ThumbnailImage.Source = _capture;
            
            // Re-save the temp file so drag-and-drop uses the newly annotated image
            using (var fileStream = new FileStream(_tempFilePath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_capture));
                encoder.Save(fileStream);
            }

            this.Show();
        };

        annotator.Closed += (s, args) => 
        {
            // If the user closes the annotator without saving, just restore the original thumbnail
            if (this.Visibility != Visibility.Visible)
            {
                this.Show();
            }
        };

        annotator.Show();
        this.Hide();
    }

    public void ExecuteOcrAndClose()
    {
        Text_Click(this, new RoutedEventArgs());
    }

    private async void Text_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null)
            {
                MessageBox.Show("OCR is not supported for the current user language on this Windows installation.", "GlassShot");
                return;
            }

            using (var ms = new MemoryStream())
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_capture));
                encoder.Save(ms);
                
                var randomAccessStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var dataWriter = new Windows.Storage.Streams.DataWriter(randomAccessStream.GetOutputStreamAt(0));
                dataWriter.WriteBytes(ms.ToArray());
                await dataWriter.StoreAsync();
                
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                
                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    Clipboard.SetText(ocrResult.Text);
                    MessageBox.Show("Text extracted and copied to clipboard!", "GlassShot OCR");
                    this.Close();
                }
                else
                {
                    MessageBox.Show("No text found in the image.", "GlassShot OCR");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to extract text: {ex.Message}", "GlassShot OCR");
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        ToggleClickThrough();
    }

    private void ToggleClickThrough()
    {
        _isClickThrough = !_isClickThrough;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        long extendedStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);

        if (_isClickThrough)
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED));
            ActionPanel.Visibility = Visibility.Hidden;
            DragHint.Visibility = Visibility.Hidden;
            this.Opacity = 0.8;
            MessageBox.Show("Click-through mode enabled. The widget is now locked and unclickable.\nPress Alt+Shift+U to unpin.", "GlassShot");
        }
        else
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(extendedStyle & ~WS_EX_TRANSPARENT));
            this.Opacity = 1.0;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _activeWindows--;
        if (_activeWindows < 0) _activeWindows = 0;
        try
        {
            if (_videoFilePath == null && File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }
        catch { }
    }
}
