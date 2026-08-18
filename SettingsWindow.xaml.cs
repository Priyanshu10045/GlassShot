using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GlassShot;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;
    private bool _isClosed = false;
    private bool _isLoading = true;

    public static SettingsWindow Instance
    {
        get
        {
            if (_instance == null || _instance._isClosed)
            {
                _instance = new SettingsWindow();
            }
            return _instance;
        }
    }

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettingsIntoUI();
        _isLoading = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        base.OnClosed(e);
    }

    private void LoadSettingsIntoUI()
    {
        _isLoading = true;
        var s = SettingsManager.Current;

        ChkStartup.IsChecked = s.LaunchAtStartup;
        ChkSound.IsChecked = s.PlaySoundEffects;
        ChkHideIcons.IsChecked = s.HideDesktopIconsDuringCapture;
        TxtSaveDir.Text = s.DefaultSaveDirectory;

        TxtShortcutArea.Text = s.CaptureAreaShortcut;
        TxtShortcutFull.Text = s.CaptureFullscreenShortcut;
        TxtShortcutVideo.Text = s.RecordVideoShortcut;
        TxtShortcutOcr.Text = s.CaptureTextShortcut;
        TxtShortcutIcons.Text = s.ToggleDesktopIconsShortcut;

        SliderScale.Value = s.ThumbnailScale;
        LblScaleValue.Text = $"{(int)(s.ThumbnailScale * 100)}%";

        SliderOverlap.Value = s.CardOverlap;
        LblOverlapValue.Text = $"{(int)s.CardOverlap} px";

        ComboAutoClose.SelectedIndex = s.AutoCloseSeconds == 0 ? 0 : (s.AutoCloseSeconds == 5 ? 1 : (s.AutoCloseSeconds == 15 ? 2 : 3));
        ComboFps.SelectedIndex = s.VideoFPS == 60 ? 1 : 0;
        ComboPreset.SelectedIndex = s.VideoPreset == "superfast" ? 1 : (s.VideoPreset == "veryfast" ? 2 : 0);
        ComboGifQuality.SelectedIndex = s.GifQuality == "Standard" ? 1 : 0;

        _isLoading = false;
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelGeneral == null || PanelShortcuts == null || PanelQuickAccess == null || PanelRecording == null || PanelAbout == null) return;

        PanelGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelShortcuts.Visibility = TabShortcuts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelQuickAccess.Visibility = TabQuickAccess.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelRecording.Visibility = TabRecording.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelAbout.Visibility = TabAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        SettingsManager.Current.LaunchAtStartup = ChkStartup.IsChecked == true;
        SettingsManager.Current.PlaySoundEffects = ChkSound.IsChecked == true;
        SettingsManager.Current.HideDesktopIconsDuringCapture = ChkHideIcons.IsChecked == true;

        SettingsManager.Save();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default save folder for GlassShot recordings and exports",
            UseDescriptionForTitle = true,
            SelectedPath = SettingsManager.Current.DefaultSaveDirectory
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            TxtSaveDir.Text = dlg.SelectedPath;
            SettingsManager.Current.DefaultSaveDirectory = dlg.SelectedPath;
            SettingsManager.Save();
        }
    }

    private void Shortcut_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier keys pressed alone
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        ModifierKeys mods = Keyboard.Modifiers;
        string modStr = "";
        if ((mods & ModifierKeys.Control) != 0) modStr += "Ctrl + ";
        if ((mods & ModifierKeys.Shift) != 0) modStr += "Shift + ";
        if ((mods & ModifierKeys.Alt) != 0) modStr += "Alt + ";
        if ((mods & ModifierKeys.Windows) != 0) modStr += "Win + ";

        string shortcutStr = $"{modStr}{key}";

        if (sender is TextBox txt)
        {
            txt.Text = shortcutStr;

            if (txt == TxtShortcutArea) SettingsManager.Current.CaptureAreaShortcut = shortcutStr;
            else if (txt == TxtShortcutFull) SettingsManager.Current.CaptureFullscreenShortcut = shortcutStr;
            else if (txt == TxtShortcutVideo) SettingsManager.Current.RecordVideoShortcut = shortcutStr;
            else if (txt == TxtShortcutOcr) SettingsManager.Current.CaptureTextShortcut = shortcutStr;
            else if (txt == TxtShortcutIcons) SettingsManager.Current.ToggleDesktopIconsShortcut = shortcutStr;

            SettingsManager.Save();
        }
    }

    private void ResetShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsManager.Current;
        s.CaptureAreaShortcut = "Ctrl + Shift + C";
        s.CaptureFullscreenShortcut = "Ctrl + Shift + F";
        s.RecordVideoShortcut = "Ctrl + Shift + V";
        s.CaptureTextShortcut = "Ctrl + Shift + T";
        s.ToggleDesktopIconsShortcut = "Ctrl + Shift + H";

        LoadSettingsIntoUI();
        SettingsManager.Save();
    }

    private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || LblScaleValue == null) return;

        double val = Math.Round(SliderScale.Value, 2);
        LblScaleValue.Text = $"{(int)(val * 100)}%";
        SettingsManager.Current.ThumbnailScale = val;
        SettingsManager.Save();
    }

    private void SliderOverlap_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || LblOverlapValue == null) return;

        double val = Math.Round(SliderOverlap.Value, 1);
        LblOverlapValue.Text = $"{(int)val} px";
        SettingsManager.Current.CardOverlap = val;
        SettingsManager.Save();
    }

    private void ComboAutoClose_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ComboAutoClose == null) return;

        int idx = ComboAutoClose.SelectedIndex;
        SettingsManager.Current.AutoCloseSeconds = idx == 0 ? 0 : (idx == 1 ? 5 : (idx == 2 ? 15 : 30));
        SettingsManager.Save();
    }

    private void ComboFps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ComboFps == null) return;

        SettingsManager.Current.VideoFPS = ComboFps.SelectedIndex == 1 ? 60 : 30;
        SettingsManager.Save();
    }

    private void ComboPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ComboPreset == null) return;

        int idx = ComboPreset.SelectedIndex;
        SettingsManager.Current.VideoPreset = idx == 1 ? "superfast" : (idx == 2 ? "veryfast" : "ultrafast");
        SettingsManager.Save();
    }

    private void ComboGifQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ComboGifQuality == null) return;

        SettingsManager.Current.GifQuality = ComboGifQuality.SelectedIndex == 1 ? "Standard" : "High";
        SettingsManager.Save();
    }
}
