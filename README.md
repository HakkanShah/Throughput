# Throughput

A lightweight Windows utility that displays **real-time network speed** as an always-on-top overlay, plus an **on-demand bandwidth speed test** — all without any external services or data collection.

<table align="center">
  <tr>
    <td align="center"><img src="docs/screenshot-overlay.png" alt="Overlay Window" width="180"/><br/><em>Overlay (Always-on-top)</em></td>
    <td align="center"><img src="docs/screenshot-dashboard.png" alt="Dashboard Window" width="280"/><br/><em>Dashboard</em></td>
  </tr>
</table>

## ✨ Features

### Live Network Throughput (Always Running)
- 📊 **Real-time speeds** — Shows current download (↓) and upload (↑) rates
- 🔄 **Auto-detect adapter** — Automatically selects active network interface  
- ⚡ **Low resource usage** — Polls once per second with minimal CPU impact
- 🔝 **Always on top** — Compact floating overlay stays visible
- 🖱️ **Draggable** — Position anywhere on screen

### On-Demand Speed Test
- 📈 **Bandwidth measurement** — Tests actual internet speed, not just local network
- ↓↑ **Download & Upload** — Measures both directions
- ⏱️ **Latency (Ping)** — Measures network response time
- 🔗 **Multi-connection** — Uses parallel connections for accurate results
- 🎯 **Warm-up exclusion** — Ignores initial TCP ramp-up for accuracy
- ⏳ **~10 second test** — Quick but reliable results

### Dual-Window Design
- **Overlay Mode** — Small, minimal, always visible
- **Dashboard Mode** — Full window with speed test controls and detailed results

## 📥 Download & Install

### Option 1: Portable (Recommended)
1. Download `Throughput.exe` from [Releases](https://github.com/HakkanShah/Throughput/releases)
2. Double-click to run — no installation needed
3. The overlay appears at the bottom-right of your screen

### Option 2: MSIX Installer (Windows 10/11)
1. Download `Throughput.msix` from [Releases](https://github.com/HakkanShah/Throughput/releases)
2. Double-click to install
3. Find "Throughput" in your Start Menu

## 🖥️ System Requirements

| Requirement | Value |
|-------------|-------|
| **OS** | Windows 10 (1809+) or Windows 11 |
| **Architecture** | x64 (64-bit) |
| **RAM** | ~50 MB |
| **Storage** | ~100 MB (portable) |

## 📖 Usage

### Overlay Window
- **Speed Test**: Click "⚡ Test Speed" to run bandwidth test
- **Dashboard**: Click "Open Dashboard" for full controls
- **Move**: Drag anywhere on screen
- **Close**: Click ✕ or right-click tray icon → Exit

### Dashboard Window
- **Live Throughput**: View current network activity
- **Speed Test**: Click button, wait ~30 seconds for full results
- **Results**: Download speed, upload speed, and latency

### System Tray
- **Double-click**: Open Dashboard
- **Right-click**: Menu with Show Overlay, Open Dashboard, Exit

## 🔬 Live Throughput vs Speed Test

| Feature | Live Throughput | Speed Test |
|---------|-----------------|------------|
| **What it measures** | Current network activity | Maximum bandwidth capacity |
| **Data source** | Windows Performance Counters | HTTP downloads/uploads |
| **Update frequency** | Every 1 second | On-demand (~30s test) |
| **Internet required** | No (any network traffic) | Yes |
| **Accuracy** | Exact (local measurement) | Estimation (varies by server) |
| **Use case** | Monitor real usage | Check internet speed |

> **Note**: Speed test results are labeled as "Quick bandwidth estimation — results may vary" because actual speeds depend on many factors including server load, time of day, and network conditions.

## 🔒 Privacy

**No telemetry. No data collection. No accounts.**

- All measurements happen locally
- Speed test uses standard public CDN endpoints (Cloudflare)
- No data is sent anywhere except the speed test servers
- No analytics, tracking, or phone-home features
- Open source — verify the code yourself

## 🔧 How It Works

### Live Throughput
Reads network statistics directly from Windows Performance Counters:
- `Network Interface → Bytes Received/sec`
- `Network Interface → Bytes Sent/sec`

### Speed Test
1. **Latency**: Multiple HTTP HEAD requests to measure round-trip time
2. **Download**: 4 parallel HTTP connections downloading test data
3. **Upload**: 4 parallel HTTP POST requests with random data
4. **Warm-up**: First 2 seconds excluded for TCP ramp-up accuracy

Test endpoints: `speed.cloudflare.com` (global CDN, reliable, no API key needed)

## 🛠️ Build from Source

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

# Publish portable executable
.\publish-portable.ps1
# Output: ./publish/portable/Throughput.exe

# Prepare for MSIX packaging
.\publish-msix.ps1
# Output: ./publish/msix-layout/
```

### Project Structure

```
Throughput/
├── Windows/                    # WPF Windows
│   ├── OverlayWindow.xaml     # Compact overlay
│   └── MainAppWindow.xaml     # Full dashboard
├── Services/
│   ├── NetworkSpeedMonitor.cs # Performance counter readings
│   └── SpeedTestService.cs    # Speed test engine
├── Models/
│   ├── SpeedTestResult.cs     # Test result data
│   └── SpeedTestProgress.cs   # Progress reporting
├── Helpers/
│   └── SpeedFormatter.cs      # Speed formatting utilities
├── Assets/                     # Icons and resources
├── Packaging/                  # MSIX configuration
├── App.xaml                    # Application entry
└── Throughput.csproj          # Project configuration
```

## 🚀 Add to Windows Startup

To run Throughput automatically when Windows starts:

### Method 1: Startup Folder
1. Press `Win + R`, type `shell:startup`, press Enter
2. Create a shortcut to `Throughput.exe` in the opened folder

### Method 2: Registry (PowerShell)
```powershell
$path = "C:\Path\To\Throughput.exe"
New-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "Throughput" -Value $path -PropertyType String -Force
```

## 📄 License

[MIT License](LICENSE)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

<p align="center">
  Crafted with ❤️ by <a href="https://hakkan.is-a.dev">Hakkan</a>
</p>
