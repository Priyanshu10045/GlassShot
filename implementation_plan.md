# GlassShot Implementation Plan

This document outlines the architectural design and implementation phases for **GlassShot**, a high-performance, lightweight CleanShot X clone for Windows. The application will be built using C# and WPF (.NET 8) to leverage hardware-accelerated UI while utilizing native Win32 APIs for system-level integrations.

## Confirmed Tech Stack

*   **Framework:** C# with WPF (.NET 8/9).
*   **Graphics Library:** SkiaSharp for cross-platform, hardware-accelerated vector rendering.
*   **Video Encoding:** Hybrid approach: MediaFoundation via Windows Graphics Capture for lightweight MP4 recording, and FFmpeg/Magick.NET for high-quality GIF generation.
*   **Third-Party Libraries:** Permitted (e.g., `NHotkey`, `CommunityToolkit.Mvvm`).

## Proposed Architecture

The application will follow a strict MVVM (Model-View-ViewModel) architectural pattern to ensure UI threads remain unblocked by image processing or system API calls.

*   **Core Logic (.NET 8 Class Library):** Platform-agnostic image processing, state management, and plugin logic.
*   **UI Layer (WPF Application):** Hardware-accelerated windows, borderless overlays, and the annotation canvas.
*   **Native Interop (C# PInvoke Wrappers):** Direct integration with `user32.dll`, `gdi32.dll`, and `dwmapi.dll` for hooks, window styles, and DPI awareness.

## Implementation Phases

We will build GlassShot incrementally, ensuring each module is stable and performant before proceeding.

### Phase 1: Project Setup & Module A (The Capture & Overlay Engine)
*   **Initialization:** Setup the .NET 8 WPF project structure (`GlassShot.sln`).
*   **Global Hooks:** Implement low-level keyboard hooks (PrintScreen) to trigger the capture state.
*   **Multi-Monitor Support:** Create a transparent, dimmed, full-screen overlay that spans all active monitors, respecting per-monitor DPI scaling.
*   **Capture Logic:** Implement the cursor-tracking magnifier loupe and native screen region capture using GDI+ or DirectX.
*   *Smart Edge-Snapping & Scrolling Capture will be implemented as a v1.5 feature due to complexity.*

### Phase 2: Module B & C (Quick Access Overlay & Always-On-Top Widgets)
*   **Quick Access Overlay:** Create a borderless, floating thumbnail window in the bottom-right corner that appears post-capture.
*   **Drag & Drop:** Implement Win32 `DoDragDrop` to allow users to drag the thumbnail into Explorer, browsers, or chat applications.
*   **Pinned Widgets:** Allow converting a capture into a floating, always-on-top window.
*   **Click-Through Mode:** Utilize `WS_EX_TRANSPARENT` and `WS_EX_LAYERED` window styles to toggle click-through functionality for pinned widgets.

### Phase 3: Module D (Vector Annotation Canvas)
*   **Canvas Engine:** Integrate `SkiaSharp` (or WPF Canvas) for high-performance rendering.
*   **State Management:** Implement a robust Undo/Redo stack for all canvas actions.
*   **Tools:** Add primitives (arrows, rectangles, text) and utility tools (auto-incrementing badges, spotlight, pixelate/blur filters).
*   **Social Backgrounds:** Implement dynamic backgrounds with gradients and soft drop shadows for premium exports.

### Phase 4: Module E (On-Device System Tools)
*   **OCR Integration:** Integrate `Windows.Media.Ocr` to extract text from captured regions directly to the clipboard.
*   **Desktop Clutter Control:** Implement a utility to find the `SysListView32` handle and toggle its visibility during captures.

### Phase 5: Module F (Screen Recorder & Video Workspace)

Implementing high-performance screen recording and video encoding directly in C# using Windows MediaFoundation (WGC) requires massive COM-interop wrappers and is highly prone to memory leaks and dropped frames. 

To guarantee 60 FPS, robust audio loopback, and zero-leak performance, we will adopt the industry-standard **FFmpeg CLI Engine** approach (similar to how tools like OBS and ShareX operate under the hood).

#### 1. FFmpeg Engine Integration
*   **Auto-Deployment:** On first launch of the recording feature, GlassShot will silently download a lightweight, standalone `ffmpeg.exe` binary to the local `AppData` folder. No global installations or admin privileges required.
*   **Process Wrapper:** Build a `ProcessStartInfo` wrapper to manage the FFmpeg lifecycle, allowing us to gracefully start and stop the recording by sending `q` to the standard input.

#### 2. Recording Pipeline
*   **Video Capture:** Use the `gdigrab` input format to capture the desktop. We will pass the exact physical pixel bounding box (calculated from our `SelectionRect`) to FFmpeg using `-offset_x`, `-offset_y`, and `-video_size`.
*   **Encoding:** Use the lightweight, high-compatibility `libx264` encoder with `ultrafast` preset to ensure zero CPU bottleneck during recording, outputting to a temporary MP4 file.
*   **Audio Capture:** (Optional) Use `dshow` or WASAPI loopback to capture system audio if requested.

#### 3. Post-Processing & GIF Generation
*   **GIF Quantization:** If the user clicks "Save as GIF" in the Quick Access Overlay, we will execute a secondary FFmpeg pass. We will use a complex filtergraph (`split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse`) to generate a custom 256-color palette based on the video's actual frames. This produces incredibly high-quality GIFs with tiny file sizes, exactly like CleanShot X.

## User Review Required

> [!IMPORTANT]  
> **Phase 5 Architectural Shift:**  
> The original plan suggested writing raw MediaFoundation C# code. I am proposing we pivot to using a hidden `FFmpeg.exe` process instead. FFmpeg is infinitely more stable, handles scaling/GIF quantization natively, and provides hardware-accelerated H.264 encoding out of the box without us having to write thousands of lines of fragile COM interop.
> 
> **Are you okay with GlassShot downloading and using a bundled `ffmpeg.exe` for the screen recorder?**

### Phase 6: CleanShot X UI/UX Parity (Video Overlay & Shortcuts)

To ensure GlassShot is a true 1:1 clone of CleanShot X, we need to unify the keyboard shortcuts and replicate the seamless Quick Access Overlay behavior for video recordings.

#### 1. Unified Keyboard Shortcuts (CleanShot X Mappings)
CleanShot X on macOS defaults to `Cmd+Shift+X` number combinations. For Windows, we will map these to `Alt+Shift+X` (as `Ctrl+Shift+Number` is often reserved by browsers):
*   `Alt + Shift + 4`: Capture Area (Image)
*   `Alt + Shift + 5`: Record Screen (Video)
*   `Alt + Shift + 3`: Capture Fullscreen
*   `Alt + Shift + 2`: Capture Text (OCR)

#### 2. Video Quick Access Overlay
In CleanShot X, when you click "Stop" on a video recording, the video does NOT just save silently. It spawns the **exact same Quick Access Thumbnail Overlay** in the bottom corner of your screen as a screenshot does. 
*   **Implementation:** We will modify `QuickAccessWindow.xaml` to accept either a `BitmapSource` (for images) or a `string filePath` (for videos).
*   **Video Thumbnail:** If a video is passed, we will extract the first frame of the `.mp4` using FFmpeg (or MediaElement) and display it in the thumbnail with a small "Play" icon overlay.
*   **Drag and Drop:** Users will be able to drag the video thumbnail directly into Discord/Slack just like they do with screenshots.
*   **Video Actions:** The hover toolbar will include: "Save as GIF", "Trim" (future), and "Copy".

## User Review Required

> [!IMPORTANT]  
> **CleanShot X Parity Plan:**  
> Please review the proposed Windows keyboard shortcut mappings (`Alt+Shift+...`). Are you happy with mimicking the exact macOS CleanShot X numeric combinations, or would you prefer mnemonic shortcuts like `Ctrl+Shift+C` (Capture) and `Ctrl+Shift+V` (Video)?
> 
### Phase 7: Dynamic Layered Screenshot Stack

Currently, taking multiple screenshots spawns multiple independent `QuickAccessWindow` instances that tile vertically. The user requested a sleek, layered "deck of cards" approach where screenshots stack on top of each other, fan out smoothly on hover, and allow bringing specific layers to the front via clicking.

#### Proposed Architecture
To achieve butter-smooth WPF animations (Translate, Scale, and Z-Index) without fighting the Windows OS window manager, we must refactor the Quick Access system into a single, cohesive WPF Window.

1.  **Singleton Manager (`QuickAccessManagerWindow`)**:
    *   Instead of spawning multiple `Window` instances, we will use a single, transparent, full-screen (or large) `Window` with `AllowsTransparency="True"`.
    *   This window will contain a `Canvas` to host the thumbnails.
2.  **`ThumbnailLayer` Control**:
    *   The current `QuickAccessWindow.xaml` UI (the rounded border, play icon, hover action panel) will be converted into a `UserControl` named `ThumbnailLayer.xaml`.
3.  **Stacking Logic & Animations**:
    *   When a new capture is taken, a new `ThumbnailLayer` is instantiated and added to the `Canvas`.
    *   **Default State:** The newest capture is placed at `ZIndex = Max`, positioned at the bottom right. Older captures are animated backwards (scaled down to 95%, 90%, and translated up/left by 10px each) to create a visual "stack".
    *   **Hover State (Fanning):** When the user hovers over the stack, a WPF `Storyboard` triggers, fanning out all layers horizontally or vertically so the user can clearly see every capture.
    *   **Click-to-Front:** Clicking any fanned-out `ThumbnailLayer` dynamically updates its `ZIndex` to the max and animates it to the front, pushing the previous front layer back into the deck.

## User Review Required

> [!IMPORTANT]  
> **Phase 7 Architectural Shift:**  
> The new layered stack means all Quick Access thumbnails will live inside a single unified widget. 
> 1. Do you want the stack to fan out **Horizontally** (to the left) or **Vertically** (upwards) when you hover over it?
> 2. Are you ready for me to proceed with converting the standalone window into this dynamic WPF Canvas architecture?
