using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using SkiaSharp.Views.Desktop;

namespace GlassShot;

public class AnnotationAction
{
    public string ToolType { get; set; } = "Arrow";
    public SKPath Path { get; set; } = new SKPath();
    public SKRect Bounds { get; set; }
}

public partial class AnnotationWindow : Window
{
    private SKBitmap _originalImage;
    private string _currentTool = "Arrow";
    private bool _applySocialBackground = false;
    
    private List<AnnotationAction> _actions = new List<AnnotationAction>();
    private AnnotationAction? _currentAction;
    private bool _isDrawing = false;
    private SKPoint _startPoint;

    // Viewport calculation
    private float _scale;
    private float _left;
    private float _top;

    public Action<BitmapSource>? OnSaveCompleted { get; set; }

    public AnnotationWindow(BitmapSource sourceImage)
    {
        InitializeComponent();
        _originalImage = BitmapSourceToSKBitmap(sourceImage);
    }

    private static SKBitmap BitmapSourceToSKBitmap(BitmapSource bitmapSource)
    {
        var formatted = new FormatConvertedBitmap();
        formatted.BeginInit();
        formatted.Source = bitmapSource;
        formatted.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
        formatted.EndInit();

        int width = formatted.PixelWidth;
        int height = formatted.PixelHeight;
        int stride = width * 4;

        var skBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        formatted.CopyPixels(new Int32Rect(0, 0, width, height), skBitmap.GetPixels(), height * stride, stride);
        return skBitmap;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _originalImage?.Dispose();
        foreach (var action in _actions)
        {
            action.Path?.Dispose();
        }
        _actions.Clear();
    }

    private void UpdateViewport(SKImageInfo info)
    {
        if (_originalImage == null) return;

        float logicalW = _originalImage.Width + (_applySocialBackground ? 160 : 0);
        float logicalH = _originalImage.Height + (_applySocialBackground ? 160 : 0);

        _scale = Math.Min((float)info.Width / logicalW, (float)info.Height / logicalH);
        if (_scale > 1.0f) _scale = 1.0f; // Prevent blurry upscaling if the window is larger than the capture!
        
        float newWidth = logicalW * _scale;
        float newHeight = logicalH * _scale;
        
        _left = (info.Width - newWidth) / 2;
        _top = (info.Height - newHeight) / 2;
    }

    private SKPoint GetImageCoordinates(Point wpfPos)
    {
        float canvasX = (float)(wpfPos.X * (SkiaCanvas.CanvasSize.Width / SkiaCanvas.ActualWidth));
        float canvasY = (float)(wpfPos.Y * (SkiaCanvas.CanvasSize.Height / SkiaCanvas.ActualHeight));

        float padding = _applySocialBackground ? 80f * _scale : 0f;

        float imageX = (canvasX - (_left + padding)) / _scale;
        float imageY = (canvasY - (_top + padding)) / _scale;
        return new SKPoint(imageX, imageY);
    }

    private void SkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_originalImage == null) return;

        UpdateViewport(e.Info);

        float padding = _applySocialBackground ? 80f * _scale : 0f;
        float imageDrawWidth = _originalImage.Width * _scale;
        float imageDrawHeight = _originalImage.Height * _scale;
        var destRect = new SKRect(_left + padding, _top + padding, _left + padding + imageDrawWidth, _top + padding + imageDrawHeight);

        if (_applySocialBackground)
        {
            var canvasRect = new SKRect(_left, _top, _left + imageDrawWidth + padding * 2, _top + imageDrawHeight + padding * 2);

            // Draw Gradient Background
            using (var paint = new SKPaint())
            {
                var colors = new SKColor[] { new SKColor(131, 58, 180), new SKColor(253, 29, 29), new SKColor(252, 176, 69) };
                paint.Shader = SKShader.CreateLinearGradient(new SKPoint(canvasRect.Left, canvasRect.Top), new SKPoint(canvasRect.Right, canvasRect.Bottom), colors, null, SKShaderTileMode.Clamp);
                canvas.DrawRect(canvasRect, paint);
            }

            // Draw Drop Shadow
            using (var shadowPaint = new SKPaint())
            {
                shadowPaint.ImageFilter = SKImageFilter.CreateDropShadow(0, 15, 20, 20, new SKColor(0, 0, 0, 150));
                canvas.DrawRoundRect(destRect, 10 * _scale, 10 * _scale, shadowPaint);
            }

            // Clip Image to Rounded Corners
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(destRect, 10 * _scale, 10 * _scale), SKClipOperation.Intersect, true);
            using (var hqPaint = new SKPaint { FilterQuality = SKFilterQuality.High })
            {
                canvas.DrawBitmap(_originalImage, destRect, hqPaint);
            }
            canvas.Restore();
        }
        else
        {
            using (var hqPaint = new SKPaint { FilterQuality = SKFilterQuality.High })
            {
                canvas.DrawBitmap(_originalImage, destRect, hqPaint);
            }
        }

        canvas.Save();
        canvas.Translate(_left + padding, _top + padding);
        canvas.Scale(_scale);

        using (var strokePaint = new SKPaint { Color = SKColors.Red, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 })
        using (var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(8, 8) })
        using (var previewBlurPaint = new SKPaint { Color = new SKColor(0,0,0,80), Style = SKPaintStyle.Fill })
        {
            Action<AnnotationAction> drawAction = (action) =>
            {
                if (action.ToolType == "Blur")
                {
                    canvas.Save();
                    canvas.ClipRect(action.Bounds);
                    canvas.DrawBitmap(_originalImage, 0, 0, blurPaint);
                    canvas.Restore();
                }
                else
                {
                    canvas.DrawPath(action.Path, strokePaint);
                }
            };

            foreach (var action in _actions) drawAction(action);

            if (_isDrawing && _currentAction != null)
            {
                if (_currentAction.ToolType == "Blur")
                {
                    canvas.DrawRect(_currentAction.Bounds, previewBlurPaint);
                    canvas.DrawRect(_currentAction.Bounds, strokePaint);
                }
                else
                {
                    canvas.DrawPath(_currentAction.Path, strokePaint);
                }
            }
        }

        canvas.Restore();
    }

    private void SkiaCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SkiaCanvas);
        _startPoint = GetImageCoordinates(pos);
        _isDrawing = true;
        _currentAction = new AnnotationAction { ToolType = _currentTool, Path = new SKPath() };
    }

    private void SkiaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _currentAction == null) return;

        var pos = e.GetPosition(SkiaCanvas);
        var currentPoint = GetImageCoordinates(pos);
        
        _currentAction.Path.Reset();

        if (_currentTool == "Arrow")
        {
            _currentAction.Path.MoveTo(_startPoint);
            _currentAction.Path.LineTo(currentPoint);

            float dx = currentPoint.X - _startPoint.X;
            float dy = currentPoint.Y - _startPoint.Y;
            double angle = Math.Atan2(dy, dx);
            float arrowLength = 20f;
            float arrowAngle = (float)(Math.PI / 6);

            float x1 = currentPoint.X - arrowLength * (float)Math.Cos(angle - arrowAngle);
            float y1 = currentPoint.Y - arrowLength * (float)Math.Sin(angle - arrowAngle);
            float x2 = currentPoint.X - arrowLength * (float)Math.Cos(angle + arrowAngle);
            float y2 = currentPoint.Y - arrowLength * (float)Math.Sin(angle + arrowAngle);

            _currentAction.Path.MoveTo(currentPoint);
            _currentAction.Path.LineTo(x1, y1);
            _currentAction.Path.MoveTo(currentPoint);
            _currentAction.Path.LineTo(x2, y2);
        }
        else if (_currentTool == "Rect" || _currentTool == "Blur")
        {
            var rect = new SKRect(
                Math.Min(_startPoint.X, currentPoint.X),
                Math.Min(_startPoint.Y, currentPoint.Y),
                Math.Max(_startPoint.X, currentPoint.X),
                Math.Max(_startPoint.Y, currentPoint.Y));
            
            _currentAction.Bounds = rect;
            if (_currentTool == "Rect") _currentAction.Path.AddRect(rect);
        }

        SkiaCanvas.InvalidateVisual();
    }

    private void SkiaCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        
        if (_currentAction != null)
        {
            if (_currentTool == "Blur" && _currentAction.Bounds.Width > 5 && _currentAction.Bounds.Height > 5)
            {
                _actions.Add(_currentAction);
            }
            else if (_currentTool != "Blur" && !_currentAction.Path.IsEmpty)
            {
                _actions.Add(_currentAction);
            }
        }
        _currentAction = null;
        SkiaCanvas.InvalidateVisual();
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            _currentTool = btn.Tag.ToString() ?? "Arrow";
        }
    }

    private void Background_Click(object sender, RoutedEventArgs e)
    {
        _applySocialBackground = !_applySocialBackground;
        SkiaCanvas.InvalidateVisual();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_actions.Count > 0)
        {
            _actions.RemoveAt(_actions.Count - 1);
            SkiaCanvas.InvalidateVisual();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        int logicalW = _originalImage.Width + (_applySocialBackground ? 160 : 0);
        int logicalH = _originalImage.Height + (_applySocialBackground ? 160 : 0);

        var imageInfo = new SKImageInfo(logicalW, logicalH);
        using (var surface = SKSurface.Create(imageInfo))
        {
            var canvas = surface.Canvas;

            if (_applySocialBackground)
            {
                using (var paint = new SKPaint())
                {
                    var colors = new SKColor[] { new SKColor(131, 58, 180), new SKColor(253, 29, 29), new SKColor(252, 176, 69) };
                    paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(logicalW, logicalH), colors, null, SKShaderTileMode.Clamp);
                    canvas.DrawRect(new SKRect(0, 0, logicalW, logicalH), paint);
                }

                using (var shadowPaint = new SKPaint())
                {
                    shadowPaint.ImageFilter = SKImageFilter.CreateDropShadow(0, 15, 20, 20, new SKColor(0, 0, 0, 150));
                    var imageRect = new SKRect(80, 80, 80 + _originalImage.Width, 80 + _originalImage.Height);
                    canvas.DrawRoundRect(imageRect, 10, 10, shadowPaint);
                }

                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(new SKRect(80, 80, 80 + _originalImage.Width, 80 + _originalImage.Height), 10, 10), SKClipOperation.Intersect, true);
                canvas.DrawBitmap(_originalImage, 80, 80);
                canvas.Restore();
                
                canvas.Translate(80, 80);
            }
            else
            {
                canvas.DrawBitmap(_originalImage, 0, 0);
            }
            
            using (var strokePaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true })
            using (var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(8, 8) })
            {
                foreach (var action in _actions)
                {
                    if (action.ToolType == "Blur")
                    {
                        canvas.Save();
                        canvas.ClipRect(action.Bounds);
                        canvas.DrawBitmap(_originalImage, 0, 0, blurPaint);
                        canvas.Restore();
                    }
                    else
                    {
                        canvas.DrawPath(action.Path, strokePaint);
                    }
                }
            }

            using (var image = surface.Snapshot())
            {
                var dpiInfo = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                double dpiX = 96.0 * dpiInfo.DpiScaleX;
                double dpiY = 96.0 * dpiInfo.DpiScaleY;
                
                var wpfBitmap = new WriteableBitmap(
                    imageInfo.Width, imageInfo.Height, 
                    dpiX, dpiY, 
                    System.Windows.Media.PixelFormats.Bgra32, null);
                
                using (var pixmap = image.PeekPixels())
                {
                    wpfBitmap.WritePixels(
                        new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height),
                        pixmap.GetPixels(),
                        imageInfo.Width * imageInfo.Height * 4,
                        imageInfo.Width * 4);
                }

                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(wpfBitmap));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    
                    var dataObject = new DataObject();
                    dataObject.SetImage(bmp);
                    var pngStream = new MemoryStream(ms.ToArray());
                    dataObject.SetData("PNG", pngStream, false);
                    Clipboard.SetDataObject(dataObject, true);
                    
                    OnSaveCompleted?.Invoke(bmp);
                    this.Close();
                }
            }
        }
    }
}
