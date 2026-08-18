# GlassShot Initial Progress

I have established the core foundation for **GlassShot**, focusing on the architecture and the core engine for capturing the screen. Here is what has been accomplished so far in Phase 1:

## 1. Project Setup
The WPF application project has been created targeting `.NET 8.0`, integrating the requested libraries:
- `NHotkey.Wpf` for registering the global hotkey.
- `CommunityToolkit.Mvvm` for future state management.
- `System.Drawing.Common` to facilitate native GDI+ screen captures.

[GlassShot.csproj](GlassShot.csproj)

## 2. Global System Hooks
The application runs in the background and sets a global keyboard hook on the `PrintScreen` key. This allows the user to summon the GlassShot capture interface seamlessly from anywhere in Windows without stealing focus prematurely.

[MainWindow.xaml.cs](MainWindow.xaml.cs)

## 3. High-Performance Screen Capture
To ensure we capture all pixels seamlessly across multi-monitor setups, I implemented a native `ScreenCapturer` module that leverages Win32 GDI+ `CopyFromScreen` via interop.

[ScreenCapturer.cs](ScreenCapturer.cs)

## 4. Dimmed Full-Screen Overlay
When the hotkey is triggered, GlassShot instantly captures the desktop, renders it on a canvas that perfectly spans all connected monitors (`SystemParameters.VirtualScreenWidth/Height`), and applies a dimmed overlay to simulate the clean capture UX.

> [!TIP]
> The WPF `Image` and `Border` controls are set up in a `Grid` to allow building the intelligent edge-snapping and cursor loupe directly over this frozen bitmap context.

## 5. Cursor Tracking Magnifier Loupe
A `120x120` circular loupe now tracks the user's cursor across the screen. It uses a high-performance `ImageBrush` mapped to the exact pixels underneath the cursor, delivering a 5x magnified view to ensure pixel-perfect selection targeting.

## 6. Region Selection & Cropping
The user can left-click and drag to define a rectangular region.
- The `System.Windows.Media.CombinedGeometry` actively punches a transparent hole in the dimmed overlay to reveal the selected desktop area in full brightness.
- Upon releasing the mouse, the engine uses `CroppedBitmap` to extract exactly that region from the source image and places it directly into the Windows Clipboard as a proof of concept.

> [!TIP]
> The source code has been successfully migrated to `C:\Users\vpriy\Downloads\Projects\GlassShot`.

## Phase 2: Quick Access Overlay & Widgets

### 1. Floating Thumbnail Window
Instead of just copying to the clipboard silently, the engine now spawns a beautiful, borderless `QuickAccessWindow` that floats in the bottom right corner of your primary screen. It features a soft drop shadow, rounded corners, and a fluid hover overlay.

### 2. Universal Drag and Drop
You can left-click the thumbnail and physically drag the image directly into any Windows application (Discord, Slack, Explorer, Browser) using a robust Win32 `DoDragDrop` implementation with `DataFormats.FileDrop`.

### 3. Pinned Click-Through Widgets
The floating thumbnail also serves as a pinned widget.
- **Scroll Wheel Opacity:** Scroll your mouse wheel over the widget to dynamically adjust its transparency (from 100% down to 20%).
- **Click-Through Mode:** Clicking "Pin" from the hover menu modifies the window's Win32 extended style flags (`WS_EX_TRANSPARENT`). The widget visually remains on screen, but all mouse interactions pass directly through to the apps behind it. Press `Alt+Shift+U` to unpin!

## Phase 3: Vector Annotation Canvas
We have begun integrating **Module D**!
1. **SkiaSharp Engine:** A high-performance hardware-accelerated Skia element now serves as the annotation canvas.
2. **Editor UI:** Clicking the new "Edit" button on the Quick Access thumbnail opens the `AnnotationWindow` featuring a sleek dark-mode toolbar.
3. **Drawing & State:** You can currently draw lines (arrows) and rectangles over your capture. The tool features an infinite Undo stack, and clicking "Save" automatically copies the flattened composition right back to your clipboard.

## Next Steps
We will expand the Annotation Canvas with:
1. True Arrowheads for the line tool.
2. A Pixelate/Blur tool to hide sensitive info.
3. A beautiful "Social Background" wrapper with soft dropshadows.

## Phase 4: On-Device System Tools

We have successfully integrated the **WinRT native OCR Engine** into GlassShot.
Unlike other screenshot tools that rely on massive Python backends, slow cloud API calls, or giant Tesseract binaries, GlassShot hooks directly into Windows 10/11's built-in `Windows.Media.Ocr` engine.

1. **Lightning Fast:** Because it's hardware-accelerated and natively built into the OS, the text extraction happens in milliseconds.
2. **Offline Privacy:** Your screenshots never leave your machine; the OCR runs completely offline.
3. **Usage:** Simply capture a region containing text, hover over your Quick Access thumbnail, and click the new **"Text"** button. The app will instantly copy all transcribed text to your clipboard!

## Phase 5: Screen Recording (FFmpeg Engine)

GlassShot now features a robust, zero-overhead Screen Recorder built entirely on top of an auto-deployed **FFmpeg Engine**. 

1. **Auto-Deployment:** When you first trigger video recording, GlassShot silently downloads a standalone `ffmpeg.exe` binary in the background. No installations required!
2. **Video Capture:** Press **`Ctrl + Shift + V`**. Your screen will dim. Click and drag a region (the selection border will be **red** to indicate Video Mode).
3. **Recording Control:** Once you release your mouse, the recording instantly starts. A beautiful, floating **Recording Toolbar** will appear with a pulsing red animation and a live timer.
4. **Encoding:** Click **Stop**, and FFmpeg will gracefully finalize the recording into a highly optimized, high-fps `.mp4` file via `libx264`.

> [!IMPORTANT]
> **Phase 5 MP4 Recording is ready to test!** 
> Run `dotnet run` in `C:\Users\vpriy\Downloads\Projects\GlassShot`. Press `Alt+Shift+5` to trigger a video capture, select a region, and try recording a short clip!

## Phase 6: CleanShot X UI/UX Parity

GlassShot now behaves exactly like CleanShot X on macOS, providing a unified workflow and shortcut map!

### New Keyboard Shortcuts
We use an intuitive mnemonic system for Windows so it doesn't conflict with OS-level input switching (`Alt+Shift`):
*   **`Ctrl + Shift + F`**: Capture Fullscreen (Instant Quick Access thumbnail)
*   **`Ctrl + Shift + C`**: Capture Area (Image capture with white border)
*   **`Ctrl + Shift + V`**: Record Screen (Video capture with red border)
*   **`Ctrl + Shift + T`**: Capture Text (OCR capture with orange border)
*   **`Ctrl + Shift + H`**: Hide Desktop Icons (Instantly toggle desktop icons)

### Dynamic Layered Screenshot Stack
When you take multiple screenshots rapidly, they no longer overlap or clutter your screen!
Instead, GlassShot gathers them into a sleek **layered "deck of cards" stack**.
* **Hover to Fan:** Simply hover your mouse over the stack, and it will elegantly fan out vertically so you can see all your recent captures.
* **Click to Front:** Need to interact with an older screenshot? Just click it while the stack is fanned out, and it will smoothly animate to the very front of the deck!
* **Pin Standalone Layer:** Clicking the "Pin" button on any layer pops it entirely out of the stack, creating an independent floating click-through reference window.

### Unified Video Workflow
When you stop a video recording, it no longer just drops a file in a folder. It spawns the **Quick Access Overlay** in the bottom corner with a video play icon! 
*   You can instantly drag-and-drop the video thumbnail into Slack or Discord to upload the `.mp4`.
*   Hover over the video thumbnail and click **"GIF"** to invoke the custom FFmpeg bayer-dither palette generator, instantly converting the video into an incredibly optimized `.gif` file right in place!

## Phase 7 Enhancements: 120Hz Fluid Physics & Stack Geometry
To guarantee true CleanShot X visual polish on modern high refresh rate monitors:
1. **Unlocking 60 FPS Cap:** WPF animations explicitly unlock `Timeline.DesiredFrameRate`, syncing natively with 120Hz/144Hz/240Hz monitors.
2. **GPU Surface Caching:** Added `BitmapCache` and `RenderingBias="Performance"` to complex drop shadows so DirectX rasterizes layers into GPU textures, ensuring zero CPU overhead during animations.
3. **Left-Biased Stack Geometry:** Cards stack `-6px` to the left and up into open desktop space so deep decks never get cut off by the right edge of the monitor.
4. **CompositionTarget Physics Scrolling:** Mouse wheel scrolling runs on a custom 120Hz exponential decay loop (`diff * 0.25`) with elastic **rubber-band overscroll (`±45px`)**, giving immediate, snappy tactile bounce feedback when scrolling any stack.

## Phase 8: CleanShot X Preferences UI & System Tray Integration

GlassShot now features a comprehensive preferences suite and system tray integration, delivering complete administrative parity with CleanShot X!

### 1. System Tray NotifyIcon
* When GlassShot starts up, it silently docks into the Windows System Tray with the GlassShot logo.
* **Context Menu:** Right-clicking the tray icon opens a sleek menu with quick triggers for *Capture Area*, *Record Screen*, *Capture Text (OCR)*, *Preferences*, and *Quit*.
* **Double-Click:** Double-clicking the tray icon instantly brings up the Preferences window.

### 2. CleanShot X Styled Preferences Window
* **Dark Acrylic Theme:** Styled with a deep `#141414` charcoal sidebar and `#252525` grouped setting cards with subtle borders.
* **Tabbed Navigation:** Switch instantly between *General*, *Shortcuts*, *Quick Access*, *Recording*, and *About* tabs.
* **Live Configuration Binding:** Powered by `SettingsManager` and `settings.json` stored in `%AppData%/GlassShot`. Any change made in the UI is immediately saved and applied across running modules without restarting!
  * **Customizable Shortcuts:** Click inside any shortcut box and press your preferred key combination (e.g., `Ctrl + Shift + C`, `Alt + Shift + 4`, etc.). Hotkeys dynamically re-bind on the fly!
  * **Live Quick Access Tuning:** Adjust the Thumbnail Scale slider (from 50% to 100%) or Card Overlap Spacing (10px to 40px) and watch the floating stack re-layout in real time!
  * **Auto-Dismiss Timer:** Set the floating stack to automatically close after 5, 15, or 30 seconds of inactivity.
  * **Video & GIF Controls:** Toggle between 30 FPS / 60 FPS video recording, FFmpeg speed presets (`ultrafast`, `superfast`, `veryfast`), and GIF quantization quality (High 2-Pass Bayer Dither vs Standard).
* **Hover Menu Access:** Added a convenient **⚙️ Preferences** button directly inside the Quick Access thumbnail hover toolbar!
