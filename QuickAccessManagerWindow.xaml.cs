using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;

namespace GlassShot;

public partial class QuickAccessManagerWindow : Window
{
    private static QuickAccessManagerWindow? _instance;
    private readonly List<ThumbnailLayer> _layers = new();
    private bool _isHovered = false;
    private double _targetScrollOffset = 0;
    private double _currentScrollOffset = 0;
    private bool _isSmoothScrolling = false;
    private CancellationTokenSource? _hoverCancelToken;
    private CancellationTokenSource? _autoCloseToken;

    public static QuickAccessManagerWindow Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new QuickAccessManagerWindow();
                _instance.Show();
            }
            return _instance;
        }
    }

    public QuickAccessManagerWindow()
    {
        InitializeComponent();
        
        // Cover the primary work area exactly
        this.Left = SystemParameters.WorkArea.Left;
        this.Top = SystemParameters.WorkArea.Top;
        this.Width = SystemParameters.WorkArea.Width;
        this.Height = SystemParameters.WorkArea.Height;

        SettingsManager.OnSettingsChanged += () => 
        {
            if (_layers.Count > 0)
            {
                UpdateStackLayout();
                ResetAutoCloseTimer();
            }
        };
    }

    public void AddCapture(BitmapSource capture)
    {
        var layer = new ThumbnailLayer(capture);
        AddNewLayer(layer);
    }

    public void AddVideo(string videoPath)
    {
        var layer = new ThumbnailLayer(videoPath);
        AddNewLayer(layer);
    }

    private void AddNewLayer(ThumbnailLayer layer)
    {
        layer.OnLayerClosed += Layer_OnLayerClosed;
        layer.OnLayerPinned += Layer_OnLayerPinned;
        
        layer.MouseEnter += Layer_MouseEnter;
        layer.MouseLeave += Layer_MouseLeave;
        layer.MouseLeftButtonDown += Layer_MouseLeftButtonDown;

        _layers.Add(layer);
        LayerCanvas.Children.Add(layer);

        UpdateStackLayout();
        ResetAutoCloseTimer();
    }

    private async void ResetAutoCloseTimer()
    {
        _autoCloseToken?.Cancel();
        int seconds = SettingsManager.Current.AutoCloseSeconds;
        if (seconds <= 0 || _layers.Count == 0) return;

        _autoCloseToken = new CancellationTokenSource();
        var token = _autoCloseToken.Token;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            if (!token.IsCancellationRequested && !_isHovered && _layers.Count > 0)
            {
                while (_layers.Count > 0)
                {
                    RemoveLayer(_layers[0]);
                }
            }
        }
        catch (TaskCanceledException) { }
    }

    private void Layer_OnLayerPinned(ThumbnailLayer layer)
    {
        // ThumbnailLayer handles spawning the floating QuickAccessWindow internally.
        // We just need to remove it from the stack.
        RemoveLayer(layer);
    }

    private void Layer_OnLayerClosed(ThumbnailLayer layer)
    {
        RemoveLayer(layer);
    }

    private void RemoveLayer(ThumbnailLayer layer)
    {
        _layers.Remove(layer);
        LayerCanvas.Children.Remove(layer);
        layer.Cleanup();

        if (_layers.Count == 0)
        {
            _autoCloseToken?.Cancel();
            this.Close();
            _instance = null;
        }
        else
        {
            UpdateStackLayout();
            ResetAutoCloseTimer();
        }
    }

    private void Layer_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverCancelToken?.Cancel();
        _autoCloseToken?.Cancel();
        if (!_isHovered)
        {
            _isHovered = true;
            UpdateStackLayout();
        }
    }

    private async void Layer_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverCancelToken?.Cancel();
        _hoverCancelToken = new CancellationTokenSource();
        var token = _hoverCancelToken.Token;

        try
        {
            await Task.Delay(150, token); // 150ms debounce to prevent flickering over gaps
            if (!token.IsCancellationRequested)
            {
                bool currentlyHovered = _layers.Any(l => l.IsMouseOver);
                if (_isHovered != currentlyHovered)
                {
                    _isHovered = currentlyHovered;
                    if (!_isHovered)
                    {
                        StopSmoothScroll();
                        _targetScrollOffset = 0;
                        _currentScrollOffset = 0;
                        ResetAutoCloseTimer();
                    }
                    UpdateStackLayout();
                }
            }
        }
        catch (TaskCanceledException) { }
    }

    private void Layer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ThumbnailLayer layer)
        {
            // Move layer to the front (end of the list)
            _layers.Remove(layer);
            _layers.Add(layer);
            UpdateStackLayout();
        }
    }

    private void UpdateStackLayout(bool isScrolling = false)
    {
        if (_layers.Count == 0) return;

        if (!isScrolling)
        {
            StopSmoothScroll();
        }

        double scale = SettingsManager.Current.ThumbnailScale;
        double cardWidth = 240 * scale;
        double cardHeight = 160 * scale;
        double stepSize = cardHeight - SettingsManager.Current.CardOverlap;
        if (stepSize < 20) stepSize = 20;

        double baseLeft = this.Width - cardWidth - 25; 
        double baseTop = this.Height - cardHeight - 25; 

        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            
            // Index from top (front). i = _layers.Count - 1 is the front
            int indexFromFront = _layers.Count - 1 - i; 

            Panel.SetZIndex(layer, i);

            double targetX = 0;
            double targetY = 0;
            double targetScale = scale;

            if (_isHovered)
            {
                // Fan out vertically upwards with customizable overlap spacing
                targetX = baseLeft;
                targetY = baseTop - (indexFromFront * stepSize) + _currentScrollOffset; 
                targetScale = scale;
            }
            else
            {
                // Deck of cards stack effect: shift UP and LEFT into open desktop space so older cards NEVER get cut off by the right screen edge!
                targetX = baseLeft - (indexFromFront * 6);
                targetY = baseTop - (indexFromFront * 12); 
                targetScale = Math.Max(0.5, scale - (indexFromFront * 0.05));
            }

            AnimateLayer(layer, targetX, targetY, targetScale, isScrolling);
        }
    }

    private void AnimateLayer(ThumbnailLayer layer, double targetX, double targetY, double targetScale, bool isScrolling)
    {
        // When actively wheel scrolling, do NOT create new DoubleAnimations because creating 
        // storyboards on every mouse tick causes lag and stutter at 120Hz.
        if (isScrolling)
        {
            layer.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(layer, targetX);
            Canvas.SetTop(layer, targetY);
            return;
        }

        var duration = TimeSpan.FromMilliseconds(260);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Initialize coordinates if they aren't set yet to avoid jumping from (0,0)
        if (double.IsNaN(Canvas.GetLeft(layer)))
        {
            Canvas.SetLeft(layer, targetX);
            Canvas.SetTop(layer, targetY);
        }

        var leftAnim = new DoubleAnimation(targetX, duration) { EasingFunction = easing };
        var topAnim = new DoubleAnimation(targetY, duration) { EasingFunction = easing };

        // CRITICAL: Unlock WPF's default 60 FPS animation limit so it runs natively at 120Hz/144Hz/240Hz!
        Timeline.SetDesiredFrameRate(leftAnim, null);
        Timeline.SetDesiredFrameRate(topAnim, null);

        layer.BeginAnimation(Canvas.LeftProperty, leftAnim);
        layer.BeginAnimation(Canvas.TopProperty, topAnim);

        var transformGroup = layer.RenderTransform as TransformGroup;
        if (transformGroup != null)
        {
            var scaleTrans = (ScaleTransform)transformGroup.Children[0];
            var scaleAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = easing };
            Timeline.SetDesiredFrameRate(scaleAnim, null);
            
            scaleTrans.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTrans.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        
        if (!_isHovered || _layers.Count == 0) return;

        // Scale the delta by 0.5 for responsive scrolling
        _targetScrollOffset += e.Delta * 0.5;

        double scale = SettingsManager.Current.ThumbnailScale;
        double cardHeight = 160 * scale;
        double stepSize = cardHeight - SettingsManager.Current.CardOverlap;
        if (stepSize < 20) stepSize = 20;

        double baseTop = this.Height - cardHeight - 25; 
        double highestY = baseTop - ((_layers.Count - 1) * stepSize);

        // Max offset is how much we need to shift the highest item down to make it visible, plus 25px padding
        double maxOffset = highestY < 20 ? Math.Abs(highestY) + 25 : 0;

        // Allow rubber-band overscroll up to ±45px beyond boundaries for snappy tactile feedback
        if (_targetScrollOffset < -45) _targetScrollOffset = -45;
        if (_targetScrollOffset > maxOffset + 45) _targetScrollOffset = maxOffset + 45;

        StartSmoothScroll();
    }

    private void StartSmoothScroll()
    {
        if (!_isSmoothScrolling)
        {
            _isSmoothScrolling = true;
            CompositionTarget.Rendering += OnSmoothScrollRender;
        }
    }

    private void StopSmoothScroll()
    {
        if (_isSmoothScrolling)
        {
            _isSmoothScrolling = false;
            CompositionTarget.Rendering -= OnSmoothScrollRender;
        }
    }

    private void OnSmoothScrollRender(object? sender, EventArgs e)
    {
        double scale = SettingsManager.Current.ThumbnailScale;
        double cardHeight = 160 * scale;
        double stepSize = cardHeight - SettingsManager.Current.CardOverlap;
        if (stepSize < 20) stepSize = 20;

        double baseTop = this.Height - cardHeight - 25;
        double highestY = baseTop - ((_layers.Count - 1) * stepSize);
        double maxOffset = highestY < 20 ? Math.Abs(highestY) + 25 : 0;

        // Rubber-band spring physics: automatically snap back to boundaries when overscrolled
        if (_targetScrollOffset < 0)
        {
            _targetScrollOffset += (0 - _targetScrollOffset) * 0.2;
            if (Math.Abs(_targetScrollOffset) < 0.5) _targetScrollOffset = 0;
        }
        else if (_targetScrollOffset > maxOffset)
        {
            _targetScrollOffset += (maxOffset - _targetScrollOffset) * 0.2;
            if (Math.Abs(_targetScrollOffset - maxOffset) < 0.5) _targetScrollOffset = maxOffset;
        }

        double diff = _targetScrollOffset - _currentScrollOffset;
        if (Math.Abs(diff) < 0.5 && _targetScrollOffset >= 0 && _targetScrollOffset <= maxOffset)
        {
            _currentScrollOffset = _targetScrollOffset;
            StopSmoothScroll();
        }
        else
        {
            // Snappy 0.3 decay factor at 120Hz
            _currentScrollOffset += diff * 0.3;
        }

        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            int indexFromFront = _layers.Count - 1 - i;
            
            layer.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetTop(layer, baseTop - (indexFromFront * stepSize) + _currentScrollOffset);
        }
    }
}
