using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GlassShot;

public static class FFmpegManager
{
    private static Process? _ffmpegProcess;
    private static string? _currentOutputMp4;
    private static readonly HttpClient _httpClient = new();

    public static string FFmpegPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "GlassShot", 
        "ffmpeg.exe");

    public static async Task EnsureReadyAsync()
    {
        if (File.Exists(FFmpegPath)) return;

        string dir = Path.GetDirectoryName(FFmpegPath)!;
        Directory.CreateDirectory(dir);
        string zipPath = Path.Combine(dir, "ffmpeg.zip");

        string url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        var bytes = await _httpClient.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(zipPath, bytes);

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                entry.ExtractToFile(FFmpegPath, true);
            }
        }
        
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch { }
    }

    public static void StartRecording(int x, int y, int width, int height)
    {
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) return;

        string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassShot", "Recordings");
        Directory.CreateDirectory(outputDir);
        _currentOutputMp4 = Path.Combine(outputDir, $"ScreenRecording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++;

        int fps = SettingsManager.Current.VideoFPS > 0 ? SettingsManager.Current.VideoFPS : 30;
        string preset = string.IsNullOrWhiteSpace(SettingsManager.Current.VideoPreset) ? "ultrafast" : SettingsManager.Current.VideoPreset;

        string args = $"-f gdigrab -framerate {fps} -offset_x {x} -offset_y {y} -video_size {width}x{height} -i desktop -c:v libx264 -preset {preset} -pix_fmt yuv420p \"{_currentOutputMp4}\"";

        var psi = new ProcessStartInfo
        {
            FileName = FFmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        _ffmpegProcess = Process.Start(psi);
    }

    public static async Task<string?> StopRecordingAsync()
    {
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            _ffmpegProcess.StandardInput.WriteLine("q");
            await _ffmpegProcess.WaitForExitAsync();
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }

        return _currentOutputMp4;
    }

    public static async Task<System.Windows.Media.Imaging.BitmapImage?> GetVideoThumbnailAsync(string mp4Path)
    {
        string args = $"-i \"{mp4Path}\" -vframes 1 -f image2pipe -vcodec mjpeg -";
        
        var psi = new ProcessStartInfo
        {
            FileName = FFmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return null;

        var memoryStream = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
        await process.WaitForExitAsync();

        if (memoryStream.Length == 0) return null;

        memoryStream.Position = 0;
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = memoryStream;
        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public static async Task<string> CreateGifAsync(string mp4Path)
    {
        string gifPath = Path.ChangeExtension(mp4Path, ".gif");
        
        string filterGraph = SettingsManager.Current.GifQuality == "Standard"
            ? "fps=15,scale=-1:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse"
            : "fps=15,split[s0][s1];[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";

        string args = $"-i \"{mp4Path}\" -vf \"{filterGraph}\" -c:v gif -y \"{gifPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = FFmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }

        return gifPath;
    }
}
