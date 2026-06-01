# PowerPlan Switcher 🔋💎

[![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/framework-.NET%204.8-green.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.0.0-orange.svg)](https://github.com/nipunyatawara-dev/windows-powerplan-switcher/releases)

A zero-dependency, single-file, highly portable Windows utility inspired by Apple macOS visual aesthetics to instantly swap active Windows power plans. PowerPlan Switcher features modern Fluent-style glassmorphism, native system-tray quick access, automatic OS theme detection, and dynamic taskbar icon-swapping using gorgeous high-resolution 3D crystal reactors.

<p align="center">
  <img src="Assets/screenshot.png" alt="PowerPlan Switcher Interface" width="450" style="border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.3);" />
</p>

---

## 🌟 Key Features

* **📦 100% Zero-Dependency Standalone EXE**: Compiled with resources fully packed inside. There is no installer, no extra DLLs, and no accessory folder or configuration file needed. The executable is completely portable.
* **💎 Premium Glassmorphic UI**: High-fidelity dark and light theme styles meticulously crafted using custom double-buffered GDI+ rendering. Features modern anti-aliased cards, tinted checkmarks, and macOS-style pill switches.
* **🔄 Dynamic Real-time Taskbar & System Tray Swapping**: When you switch power modes, the application dynamically re-scales, crops, and swaps both your taskbar application icon and your system tray icon in real-time to match the active plan's reactor.
* **🎨 Immersive Windows 11 Native Integration**: Auto-detects your Windows system theme (Dark or Light mode) and instantly adjusts title bars, canvas frames, button shapes, and tray menus utilizing Microsoft's Desktop Window Manager (DWM) API.
* **⚡ Native win32 powercfg API Integration**: Direct interfacing with Windows `powrprof.dll` and `powercfg` shell sub-processes.
* **🛡️ Self-Healing Ultimate Performance Plan**: Detects if Windows' hidden "Ultimate Performance" power scheme is missing and automatically self-heals by duplicating the standard system schema to provide absolute response speeds.
* **🔔 Smart Tray Minimize & Autorun**: Easily minimize to the system tray with a right-click quick switcher, and toggle Windows auto-startup on login with standard User privileges (requires no elevated administrator/UAC prompts).
* **🔒 Single-Instance Protection**: Integrated kernel-level named Mutexes to guarantee only a single instances runs in your system tray at a time.
* **🚀 Zero CPU Overhead**: Events and timers are engineered to use virtually 0% background CPU resources and less than 15MB of memory.

---

## 🛠️ Tech Stack & Architecture

PowerPlan Switcher is engineered to run out-of-the-box on virtually any modern Windows installation (Windows 10, 11, and Windows Server) without requiring large runtimes or SDK installs.

* **Language**: C# 5.0 (highly portable compiler target)
* **Framework**: .NET Framework 4.8 (pre-installed on almost all Windows PCs)
* **Graphics**: System.Drawing & GDI+ Double-Buffered Canvas
* **API Hooks**: Win32 Native Desktop Window Manager (`dwmapi.dll`), Window Messaging (`user32.dll`), and Power Schemes (`powrprof.dll`)

---

## 🚀 Quick Start (How to Run)

1. Head over to the **[Releases](https://github.com/nipunyatawara-dev/windows-powerplan-switcher/releases)** tab.
2. Download `PowerPlan Switcher.exe`.
3. Double-click the file to launch it!

*Note: Since the app is self-contained, you can place this `.exe` file anywhere on your computer (e.g., your Desktop, Documents, or local startup folder).*

---

## ⌨️ How to Build (Compiling from Source)

You don't need a heavy installation of Visual Studio or MSBuild to compile this project! It builds using the built-in C# compiler (`csc.exe`) included by default in your Windows operating system.

### Compiling via PowerShell:

1. Clone this repository:
   ```bash
   git clone https://github.com/nipunyatawara-dev/windows-powerplan-switcher.git
   cd windows-powerplan-switcher
   ```
2. Run the automated build script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File build.ps1
   ```
3. Your newly compiled, high-resolution binary will be created in the root folder as `PowerPlan Switcher.exe`!

---

## 📁 Repository Structure

```
├── Assets/
│   └── screenshot.png          # App interface screenshot for README
├── icons/                      # Custom transparent PNG power mode reactors
│   ├── balanced.png
│   ├── high-performance.png
│   ├── power-saver.png
│   └── ultimate-performance.png
├── app.ico                     # Customized 3D crystal application logo
├── app.manifest                # High-DPI scaling manifest
├── build.ps1                   # Automated csc.exe compiler pipeline script
├── Program.cs                  # Complete standalone C# source code
└── README.md                   # Repository documentation
```

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Contributions are welcome! If you'd like to improve the visual aesthetics, implement new power plan adapters, or optimize Win32 integrations:

1. Fork the Repository.
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the Branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.
