# Throughput

A lightweight Windows utility that displays real-time network download and upload speed as an always-on-top overlay, similar to how phones display network speed in the status bar.

<table align="center">
  <tr>
    <td align="center"><img src="docs/screenshot-before.png" alt="Before Speed Test" width="180"/><br/><em>Before Speed Test</em></td>
    <td align="center"><img src="docs/screenshot-after.png" alt="After Speed Test" width="180"/><br/><em>After Speed Test</em></td>
  </tr>
</table>

## Features

- 📊 **Real-time network speed** - Shows download (↓) and upload (↑) speed
- ⚡ **Speed test** - One-click bandwidth test to measure your internet speed
- 🔝 **Always on top** - Overlay stays visible above all windows
- 🎨 **Minimal UI** - Small, clean, transparent dark design
- 🖱️ **Draggable** - Move the widget anywhere on screen
- 🔄 **Auto-detect adapter** - Automatically selects active network interface
- 🖥️ **System tray** - Right-click tray icon to exit
- 📦 **Portable** - Single executable, no installation required

## System Requirements

- **OS:** Windows 10 or Windows 11
- **Architecture:** x64 (64-bit)

## Download & Install

1. Download the latest `Throughput.exe` from [Releases](https://github.com/HakkanShah/Throughput/releases)
2. Double-click to run — no installation needed
3. The overlay appears at the bottom-right of your screen

## Usage

- **Speed Test:** Click on "⚡ Test Speed" to measure your bandwidth
- **Move window:** Drag the widget to reposition
- **Close:** Click ✕ or right-click the system tray icon → Exit

📖 **For detailed documentation, see [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)**

## Build from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build Commands

```powershell
# Clone the repository
git clone https://github.com/HakkanShah/Throughput.git
cd Throughput

# Build debug version
dotnet build

# Run directly
dotnet run

# Publish self-contained single-file executable (recommended)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

# The executable will be at: ./publish/Throughput.exe
```

### Build for 32-bit Windows

```powershell
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish-x86
```

## Add to Windows Startup (Optional)

To run Throughput automatically when Windows starts:

1. Press `Win + R`, type `shell:startup`, press Enter
2. Create a shortcut to `Throughput.exe` in the opened folder

Or via Registry:
```powershell
# Run in PowerShell (adjust path as needed)
$path = "C:\Path\To\Throughput.exe"
New-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Throughput" -Value $path -PropertyType String -Force
```

## How It Works

Throughput reads network statistics using **Windows Performance Counters**:
- `Network Interface → Bytes Received/sec`
- `Network Interface → Bytes Sent/sec`

No internet connection is required. All data stays local.

## License

[MIT License](LICENSE)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

<p align="center">
  Crafted with by <a href="https://hakkan.is-a.dev">Hakkan</a>
</p>
