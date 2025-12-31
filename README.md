# Throughput

A lightweight Windows utility that displays **real-time network speed** as an always-on-top overlay, plus an **on-demand bandwidth speed test** — all without any external services or data collection.

## ✨ Features

### Live Network Throughput (Always Running)
- 📊 **Real-time speeds** — Shows current download (↓) and upload (↑) rates
- 🔄 **Auto-detect adapter** — Automatically selects active network interface  
- ⚡ **Low resource usage** — Polls once per second with minimal CPU impact
- 🔝 **Always on top** — Compact floating overlay stays visible
- 🖱️ **Draggable** — Position anywhere on screen

### On-Demand Speed Test
- 📈 **Bandwidth measurement** — Tests actual internet speed, not just local network
- ↓↑ **Download & Upload** — Measures both directions with MB/s equivalents
- ⏱️ **Latency (Ping)** — Measures network response time
- 🔗 **Multi-connection** — Uses parallel connections for accurate results
- 🎯 **Warm-up exclusion** — Ignores initial TCP ramp-up for accuracy

### Dual-Window Design
- **Overlay Mode** — Small, minimal, transparent, always visible
- **Dashboard Mode** — Full window with speed test controls and detailed results

## 📥 Download & Install

### Option 1: Installer (Recommended)
1. Download `Throughput-Setup-2.1.0.exe` from [Releases](https://github.com/HakkanShah/Throughput/releases)
2. Run the installer
3. Choose options:
   - ✅ Create desktop shortcut
   - ✅ Pin to taskbar
   - ✅ Start with Windows
4. Find "Throughput" in your Start Menu

### Option 2: Portable
1. Download `Throughput.exe` from [Releases](https://github.com/HakkanShah/Throughput/releases)
2. Double-click to run — no installation needed
3. The overlay appears at the bottom-right of your screen

## 🖥️ System Requirements

| Requirement | Value |
|-------------|-------|
| **OS** | Windows 10 (1809+) or Windows 11 |
| **Architecture** | x64 (64-bit) |
| **RAM** | ~50 MB |
| **Storage** | ~100 MB |

## 📖 Usage

### Overlay Window
- **More Details** — Click to open the full dashboard
- **Move** — Drag anywhere on screen
- **Close** — Click ✕ or right-click tray icon → Exit

### Dashboard Window
- **Live Throughput** — View current network activity in real-time
- **Speed Test** — Click button to measure your internet bandwidth
- **Results** — Download (Mbps + MB/s), Upload (Mbps + MB/s), and Latency (ms)

### System Tray
- **Double-click** — Open Dashboard
- **Right-click** — Menu with Show Overlay, Open Dashboard, Exit

## 🔬 Live Throughput vs Speed Test

| Feature | Live Throughput | Speed Test |
|---------|-----------------|------------|
| **What it measures** | Current network activity | Maximum bandwidth capacity |
| **Unit displayed** | MB/s or KB/s | Mbps (+ MB/s equivalent) |
| **Data source** | Windows Performance Counters | HTTP downloads/uploads |
| **Update frequency** | Every 1 second | On-demand (~10s test) |
| **Internet required** | No | Yes |
| **Use case** | Monitor real usage | Check internet speed |

> **MB/s vs Mbps**: ISPs advertise in Mbps (megabits). File downloads show MB/s (megabytes). Divide Mbps by 8 to get MB/s (e.g., 100 Mbps ≈ 12.5 MB/s).

## 🔒 Privacy

**No telemetry. No data collection. No accounts.**

- All measurements happen locally
- Speed test uses public CDN endpoints (Cloudflare)
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
- [Inno Setup](https://jrsoftware.org/isinfo.php) (for installer)

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
dotnet publish -c Release -o publish

# Build installer (requires Inno Setup)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\setup.iss
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
├── Installer/                  # Inno Setup configuration
├── App.xaml                    # Application entry
└── Throughput.csproj          # Project configuration
```

## 📄 License

[MIT License](LICENSE)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

<p align="center">
  Crafted with by <a href="https://hakkan.is-a.dev">Hakkan</a>
</p>
