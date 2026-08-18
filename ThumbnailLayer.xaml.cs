using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace GlassShot;

public partial class ThumbnailLayer : UserControl
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(ref Win32Point pt);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32Point
    {
        public Int32 X;
        public Int32 Y;
    }

    private BitmapSource? _capture;
    private string _tempFilePath;
    private string? _videoFilePath;

    public event Action<ThumbnailLayer>? OnLayerClosed;
    public event Action<ThumbnailLayer>? OnLayerPinned;
    
    public ThumbnailLayer(BitmapSource capture)
    {
        InitializeComponent();
        _capture = capture;
        ThumbnailImage.Source = _capture;

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"GlassShot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        SaveBitmapSourceToFile(_capture, _tempFilePath);
    }

    public ThumbnailLayer(string videoPath)
    {
        InitializeComponent();
        _videoFilePath = videoPath;
        _tempFilePath = videoPath;

        LoadVideoThumbnailAsync(videoPath);
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

    private Point GetLogicalMousePosition()
    {
        Win32Point pt = new Win32Point();
        GetCursorPos(ref pt);
        
        var source = PresentationSource.FromVisual(this);
        if (source != null && source.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
        }
        return new Point(pt.X, pt.Y);
    }

    private Window? _dragWindow;

    private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            // Dim the original thumbnail in the stack
            this.Opacity = 0.3;

            // Create a custom Drag Visual Window that is scaled down
            _dragWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                IsHitTestVisible = false,
                Width = 150, // 50% scale
                Height = 100,
                Content = new Border 
                { 
                    CornerRadius = new CornerRadius(8), 
                    ClipToBounds = true,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 17, 17)),
                    Child = new Image { Source = _capture, Stretch = System.Windows.Media.Stretch.Uniform }
                }
            };
            
            // Add slight drop shadow to the drag visual
            ((Border)_dragWindow.Content).Effect = new System.Windows.Media.Effects.DropShadowEffect 
            { 
                BlurRadius = 15, ShadowDepth = 3, Opacity = 0.5 
            };

            // Set initial position offset from the cursor so we don't block the OS drop target!
            Point pt = GetLogicalMousePosition();
            _dragWindow.Left = pt.X + 15;
            _dragWindow.Top = pt.Y + 15;

            _dragWindow.Show();

            var dataObject = new DataObject(DataFormats.FileDrop, new string[] { _tempFilePath });
            
            this.GiveFeedback += DragSource_GiveFeedback;
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
            this.GiveFeedback -= DragSource_GiveFeedback;

            _dragWindow.Close();
            _dragWindow = null;
            this.Opacity = 1.0;
        }
    }

    private void DragSource_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (_dragWindow != null)
        {
            Point pt = GetLogicalMousePosition();
            
            // Smoothly follow the mouse cursor with DPI scaling fixed
            // Offset by 15px to keep the OS mouse hotspot free
            _dragWindow.Left = pt.X + 15;
            _dragWindow.Top = pt.Y + 15;
        }
    }

    private void UserControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OnLayerClosed?.Invoke(this);
    }

    private void UserControl_MouseEnter(object sender, MouseEventArgs e)
    {
        ActionPanel.Visibility = Visibility.Visible;
    }

    private void UserControl_MouseLeave(object sender, MouseEventArgs e)
    {
        ActionPanel.Visibility = Visibility.Hidden;
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
            
            using (var fileStream = new FileStream(_tempFilePath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_capture));
                encoder.Save(fileStream);
            }
        };

        annotator.Show();
        ActionPanel.Visibility = Visibility.Hidden;
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
                    OnLayerClosed?.Invoke(this);
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
        if (_capture != null)
        {
            new QuickAccessWindow(_capture).Show();
        }
        OnLayerPinned?.Invoke(this);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow.Instance.Show();
        SettingsWindow.Instance.Activate();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        OnLayerClosed?.Invoke(this);
    }
    
    public void Cleanup()
    {
        try
        {
            if (_videoFilePath == null && File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }
        catch { }
    }
}
