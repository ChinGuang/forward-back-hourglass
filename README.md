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

**The timer is remembered when you close the app.** Reopening puts it back exactly where it was, and time does not pass while the app is closed:

- A running timer carries on in the same direction.
- A paused timer stays paused.
- A countdown that had already finished reopens drained and silent. Press Reset to refill the glass.

The timer and the speeds are saved in `%APPDATA%\ForwardBackHourglass\settings.json` when the window closes, and also when Windows shuts down or logs off. If the app crashes or is ended from Task Manager, it reopens with a fresh timer. Your speeds are kept.

## Auto mode: follow apps and websites

Turn on **Auto: follow apps & sites** and the timer is driven by whatever is in front of you, instead of the buttons:

- **Apps & sites…** keeps your rules. Each app or website is set to **Forward**, **Backward** or **Pause**, e.g. `Code.exe` → Forward (earn time) and `youtube.com` → Backward (spend it).
  - **Apps:** pick one from the apps currently open, or browse for any `.exe`. Apps are matched by program file name, so they keep matching after an update moves the install folder.
  - **Websites:** type a domain or paste a page address. `youtube.com` also covers `www.youtube.com`, `m.youtube.com` and every page on YouTube. A more specific rule wins, so `music.youtube.com` beats `youtube.com`.
- **Speed per app or website:** each rule has a **Speed** box. **Default** uses the main Forward/Backward speed. You can pick a preset (0.25×–8×) or type any positive number, decimals included, such as `1.5`, `3` or `0.1`. For example, `Code.exe` could be Forward at 2× and `youtube.com` Backward at 4×. Unclassified websites use the main Backward speed, and Pause rules ignore speed. The status line shows the speed in use, e.g. "Auto · youtube.com: counting backward (4×)".
- **What the timer does:**
  - An app or website with a rule: the timer follows it.
  - Anything else, including the hourglass window itself: the timer pauses.
  - A countdown you come back to resumes where it was, rather than refilling the glass.
- **Browsers:** in **Brave, Opera and Vivaldi** the website in the active tab decides. **Chrome and Edge** use the same engine and should work, but are untested.
  - **A site without a rule** counts **backward**. The first time you visit one, a small popup in the bottom-right corner asks whether it should count Forward, Pause or Backward. The popup never takes focus, so your video and typing aren't interrupted. It also has a speed dropdown (Default or a preset). Because it can't take keyboard focus, it can't accept a typed speed; set custom speeds in **Apps & sites…**. Sites you haven't classified are also listed in **Apps & sites…** so you can classify them later.
  - **Browser pages** such as a new tab, settings or local files pause the timer and never prompt.
  - **When the address can't be read**, for example a full-screen video or F11, the timer keeps following the last site read from that window.
- **When a countdown reaches zero** in auto mode, the ring plays and the hourglass window jumps to the front until you stop it. Games running in *exclusive* full-screen can't be covered by any window, but you'll still hear the ring.
- **Buttons:** Start, Backward and Pause are greyed out while auto mode is on. Reset and the speeds still work.

**How the website is read:** the app reads the browser's address bar through Windows UI Automation, the accessibility interface screen readers use. It only looks at the browser's own toolbar, never at page content. It needs no browser extension, and nothing leaves your PC.

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
