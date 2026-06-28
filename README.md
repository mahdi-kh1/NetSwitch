# NetSwitch

A lightweight Windows **system-tray app** that switches your active network between
**Wi-Fi** and **Ethernet** — manually with one click, or automatically when you plug
in / unplug a LAN cable.

Built with **.NET 9 + WPF**, with a translucent **glassmorphism** UI (acrylic blur,
graphite palette).

[![Latest release](https://img.shields.io/github/v/release/mahdi-kh1/NetSwitch?label=download&color=2ea043)](https://github.com/mahdi-kh1/NetSwitch/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078d6)](https://github.com/mahdi-kh1/NetSwitch/releases/latest)

> Developed by **mikhodaee@gmail.com**

---

## Features

- 🔌 **One-click switch** between Wi-Fi and Ethernet from the tray
- 🤖 **Auto mode** — prefer Ethernet while a cable is connected, fall back to Wi-Fi when unplugged
- 🪟 **Glassmorphism UI** — frosted glass panels with real Windows acrylic blur
- ⚡ **Live status** — the panel updates itself (like *Network Connections*) plus a manual refresh button
- ⚙️ **Settings** — pick which adapters are your Wi-Fi / Ethernet, toggle behaviors
- 🚀 **Start with Windows** — runs silently at sign-in (elevated Scheduled Task, no UAC at boot)
- 🧠 **Smart adapter detection** — only physical adapters are listed (virtual VMware / Hyper-V / VPN adapters are filtered out)

## Requirements

- Windows 10 / 11 (x64)
- **Administrator rights** — enabling/disabling network adapters requires elevation, so the app
  requests it via UAC on launch.
- To build from source: [.NET 9 SDK](https://dotnet.microsoft.com/download)

## Download

Get the standalone `NetSwitch.exe` from the **Releases** page — it is
**self-contained** (no .NET installation required).

| | |
|---|---|
| ⬇️ **Latest release** | **https://github.com/mahdi-kh1/NetSwitch/releases/latest** |
| 📦 Direct link (v0.1.2) | https://github.com/mahdi-kh1/NetSwitch/releases/download/v0.1.2/NetSwitch-v0.1.2.exe |

Run it and approve the UAC prompt; the NetSwitch icon appears in the hidden-icons
(system tray) area.

> The self-contained exe is ~160 MB because it bundles the .NET runtime. If you already
> have the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
> installed, a much smaller framework-dependent build can be produced instead.

## Usage

- **Left-click** the tray icon → opens the glass panel.
- Click the **Wi-Fi** or **Ethernet** tile to switch. The active connection is highlighted.
- **Right-click** the tray icon for a quick menu (switch, settings, quit).
- **Settings** → choose which physical adapter is your Wi-Fi and which is your Ethernet,
  and toggle:
  - *Disable the other adapter when switching*
  - *Auto switch* (prefer Ethernet on cable)
  - *Start with Windows*

### Auto mode

When auto mode is on, the Ethernet adapter is kept **enabled** so it can act as a cable
sensor. Plugging a cable in turns Wi-Fi off; unplugging it turns Wi-Fi back on.

## Build from source

A bash build script (`build.sh`) handles versioning and output:

```bash
./build.sh             # bump patch version, build (Debug) to dist/v<version>/
./build.sh --run       # ...and launch it (UAC prompt)
./build.sh --release   # Release configuration
./build.sh --publish   # single-file, self-contained exe -> dist/NetSwitch-v<version>.exe
./build.sh --no-bump   # build without incrementing the version
```

Every build goes to its own `dist/v<version>/` folder, so a running (elevated) instance
never locks the output — you don't have to close the app between builds.

### Releasing

`release.sh` does the whole release in one shot — builds the exe, commits the version
bump, tags, pushes, and publishes a **GitHub Release** with the exe attached:

```bash
./release.sh             # bump patch, build, tag, push, publish the release
./release.sh --no-bump   # release the current version
./release.sh --notes "…" # custom release notes
./release.sh --draft     # publish as a draft
```

The GitHub Release step uses the [`gh` CLI](https://cli.github.com) if it's installed
and authenticated, otherwise a `GITHUB_TOKEN` environment variable (API), otherwise it
prints the manual web steps (the tag and exe are still prepared for you).

Or with the SDK directly:

```bash
dotnet build src/NetSwitch/NetSwitch.csproj -c Debug
```

## How it works

- Adapters are read via WMI (`root\StandardCimv2` → `MSFT_NetAdapter`), filtered to physical
  ones (`ConnectorPresent = true`). Adapter identity is stored by its stable `InterfaceGuid`.
- Enable/disable calls the `Enable` / `Disable` WMI methods on the adapter.
- Settings are saved as JSON in `%AppData%\NetSwitch\settings.json`.
- Acrylic blur is applied via `SetWindowCompositionAttribute` (`Interop/AcrylicHelper.cs`).

## Project structure

```
src/NetSwitch/
├─ App.xaml(.cs)            tray icon, context menu, service wiring
├─ MainWindow.xaml(.cs)     the glass panel (status + switch tiles)
├─ SettingsWindow.xaml(.cs) settings UI
├─ Themes/Glass.xaml        glassmorphism styles & palette
├─ Interop/AcrylicHelper.cs Windows acrylic blur
├─ Models/                  AdapterInfo, AppSettings
└─ Services/
   ├─ NetworkService.cs     WMI list / enable / disable
   ├─ SwitchController.cs   switch logic + status snapshot
   ├─ AutoSwitchMonitor.cs  cable-plug auto switching
   ├─ SettingsService.cs    JSON load/save
   └─ StartupService.cs     start-with-Windows (Scheduled Task)
```

## License

Personal project. © Developed by mikhodaee@gmail.com
