# Forward-Back Hourglass

A simple hourglass timer for Windows. It counts **forward**, counts **backward**, each at its own adjustable speed, and **rings** when backward reaches zero. It runs fully offline as one portable `.exe`, with no install and no .NET runtime needed.

## Features

| Control | What it does |
|---|---|
| **▶ Start** | Counts up from the current value with no upper limit. It also resumes after a pause and flips a backward run to forward. |
| **◀ Backward** | Counts down toward zero. It is disabled when the value is already zero. |
| **❚❚ Pause** | Freezes the value. Start or Backward resumes in that direction. |
| **⟲ Reset** | Returns to `00:00:00.0` and stops everything, including the ring. |
| **Forward / Backward speed** | 0.25×, 0.5×, 1×, 2×, 4× or 8×. A change applies immediately, even mid-run, and is remembered between launches. |
| **Ring** | When backward reaches zero, the timer stops at exactly `00:00:00.0` and a bell loops until you press **🔔 Stop ring**, Start or Reset. |

The hourglass animation turns over when you change direction:

- **Forward:** sand fills the bottom bulb once per 60 seconds of timer time.
- **Backward:** the top bulb shows how much time is left out of the value you started counting down from, and it empties exactly at zero.

Speeds are saved in `%APPDATA%\ForwardBackHourglass\settings.json`.

## Download

1. Go to the repository's [**Releases**](https://github.com/ChinGuang/forward-back-hourglass/releases) page and download `ForwardBackHourglass.exe` from the latest release. There's nothing to unzip or install.
2. Double-click it. The file is unsigned, so Windows SmartScreen may warn on first launch. Choose **More info → Run anyway**.

Every CI run also uploads the same `.exe` as the **ForwardBackHourglass-win-x64** artifact in the **Actions** tab, which is handy for testing a branch before it's released.

### Publishing a release

Push a tag that starts with `v` and CI builds the `.exe` and attaches it to a GitHub Release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

A tag containing a `-` (for example `v1.1.0-preview.1`) is published as a pre-release.

Requires Windows 10 or 11 (x64).

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet test Hourglass.sln                                                    # run the unit tests
dotnet run --project src/Hourglass.App                                       # run the app (Windows)
dotnet publish src/Hourglass.App -c Release -r win-x64 -o artifacts/publish  # build the portable exe
```

The app only runs on Windows. It still compiles and its tests run on Linux and macOS, because the logic lives in a UI-free library.

## Project layout

```
src/Hourglass.Core/          Timer state machine, speeds, formatting, sand levels, settings, view model (no UI dependency)
src/Hourglass.App/           WPF window, hourglass drawing, UI-thread ticker, looping alarm, bundled assets
tests/Hourglass.Core.Tests/  xUnit tests for everything in Core
tools/generate_assets.py     Regenerates Assets/ring.wav and Assets/hourglass.ico (python tools/generate_assets.py)
```
