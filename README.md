<div align="center">
  <img src="Assets/icon_256.png" width="128" height="128" alt="GlassShot Logo" />
  <h1>GlassShot</h1>
  <p><strong>The High-Performance CleanShot X Alternative for Windows</strong></p>

  [![Release](https://img.shields.io/badge/Release-v1.0.0-3A82F7?style=flat-square)](https://github.com/Priyanshu10045/GlassShot/releases)
  [![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://microsoft.com/windows)
  [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

  <p>Zero-latency screenshots, real-time screen recordings, 2-pass GIF export, Windows Native OCR, SkiaSharp vector annotations, and interactive floating thumbnail stacks.</p>
</div>

---

## 📥 Download & Quick Start

### Direct Download (Latest Release)
1. Download the latest binary from **[GitHub Releases (v1.0.0)](https://github.com/Priyanshu10045/GlassShot/releases/latest)**.
2. Extract the archive and run `GlassShot.exe`.
3. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> to take your first screenshot!

---

## ✨ Features

- 🎯 **Pixel-Perfect Screen Capture**: Zero-latency capture across mixed-DPI multi-monitor environments with a real-time pixel magnifier loupe.
- 🎥 **Screen Recording & 2-Pass GIF Generation**: Record any screen region with configurable FPS and presets; export crisp, compact GIFs using custom 2-pass Bayer dithering.
- 🔍 **Native Windows OCR**: Extract text instantly from screen crops straight to your clipboard with <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd>.
- 🗂️ **Interactive Quick Access Floating Stack**: Floating screenshot cards with macOS-inspired deck animations, 120Hz smooth scrolling, auto-dismiss timers, and instant drag-and-drop into apps (Slack, Discord, browsers, Outlook).
- 🎨 **SkiaSharp Annotation Studio**: Annotate with directional arrows, rectangles, blur/redact sensitive information, and apply stylish social gradient backgrounds with drop shadows.
- 🧹 **Auto-Hide Desktop Icons**: Automatically hides cluttered desktop icons during capture for clean, distraction-free screenshots (<kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>H</kbd>).
- ⚙️ **Modern Dark-Mode Preferences**: Full control over keyboard shortcuts, video framerates, encoding speed, and thumbnail sizes.

---

## ⌨️ Default Keyboard Shortcuts

| Action | Default Shortcut | Description |
| :--- | :--- | :--- |
| **Capture Area** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Interactive screen crop with real-time loupe |
| **Capture Fullscreen** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>F</kbd> | Instantly captures entire virtual desktop |
| **Record Screen** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>V</kbd> | Crop an area to record video / generate GIFs |
| **Capture Text (OCR)** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Recognizes and copies on-screen text |
| **Toggle Desktop Icons** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>H</kbd> | Shows/hides all desktop icons |

*(All shortcuts can be customized in the Preferences menu)*

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- Windows 10 (Build 19041+) / Windows 11

### Clone and Run
```powershell
# Clone the repository
git clone https://github.com/Priyanshu10045/GlassShot.git
cd GlassShot

# Run development instance
dotnet run
```

### Publish Release Build
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o ./dist
```

---

## 🏗️ Architecture & Technology Stack

- **UI & Controls**: C# 12 / WPF (.NET 8) with PerMonitorV2 High-DPI context
- **Graphics Engine**: SkiaSharp (Hardware accelerated 2D vector graphics)
- **Video & GIF Processing**: FFmpeg automation
- **System Level Hooks**: Win32 API Interop (GDI+, NHotkey global shortcut routing)
- **Text Recognition**: `Windows.Media.Ocr` runtime

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.
