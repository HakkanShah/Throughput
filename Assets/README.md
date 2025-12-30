# App Icon

This folder should contain the application icon file `app.ico`.

## Required Icon Sizes (for Windows)

Include these sizes in the multi-resolution ICO file:
- 16x16 (taskbar, tray)
- 32x32 (window title)
- 48x48 (Explorer)
- 256x256 (high DPI)

## Recommended Design

- Circular dark blue/purple background (#1a1a2e)
- Green down arrow (↓) for download
- Pink up arrow (↑) for upload
- Clean, flat, modern design

## Creating the Icon

You can use tools like:
1. **GIMP** - Free, supports ICO export
2. **IcoFX** - Dedicated ICO editor
3. **Online converters** - Convert PNG to ICO

Example using PowerShell with ImageMagick:
```powershell
magick convert icon.png -define icon:auto-resize=256,48,32,16 app.ico
```
