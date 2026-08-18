using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassShot;

public class AppSettings
{
    public bool LaunchAtStartup { get; set; } = false;
    public bool PlaySoundEffects { get; set; } = true;
    public bool HideDesktopIconsDuringCapture { get; set; } = false;
    public string DefaultSaveDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "GlassShot");

    public double ThumbnailScale { get; set; } = 0.8;
    public int AutoCloseSeconds { get; set; } = 0; // 0 = Never
    public double CardOverlap { get; set; } = 20.0;

    public int VideoFPS { get; set; } = 30;
    public string VideoPreset { get; set; } = "ultrafast";
    public string GifQuality { get; set; } = "High";

    public string CaptureAreaShortcut { get; set; } = "Ctrl + Shift + C";
    public string CaptureFullscreenShortcut { get; set; } = "Ctrl + Shift + F";
    public string RecordVideoShortcut { get; set; } = "Ctrl + Shift + V";
    public string CaptureTextShortcut { get; set; } = "Ctrl + Shift + T";
    public string ToggleDesktopIconsShortcut { get; set; } = "Ctrl + Shift + H";
}

public static class SettingsManager
{
    private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassShot");
    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");
    
    private static AppSettings? _current;
    
    public static AppSettings Current
    {
        get
        {
            if (_current == null)
            {
                Load();
            }
            return _current!;
        }
    }

    public static event Action? OnSettingsChanged;

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _current = new AppSettings();
                Save();
            }
        }
        catch
        {
            _current = new AppSettings();
        }
        
        EnsureSaveDirectoryExists();
    }

    public static void Save()
    {
        try
        {
            if (_current == null) _current = new AppSettings();
            Directory.CreateDirectory(SettingsDir);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_current, options);
            File.WriteAllText(SettingsFilePath, json);
            
            EnsureSaveDirectoryExists();
            
            OnSettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private static void EnsureSaveDirectoryExists()
    {
        try
        {
            if (_current != null && !string.IsNullOrWhiteSpace(_current.DefaultSaveDirectory))
            {
                Directory.CreateDirectory(_current.DefaultSaveDirectory);
            }
        }
        catch { }
    }
}
