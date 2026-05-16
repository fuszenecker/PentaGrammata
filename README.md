# PentaGrammata

A cross-platform Morse code copying practice application built with .NET 10 and Avalonia UI.

PentaGrammata plays random five-character groups as Morse code audio and lets you type what you hear. After each session it scores your accuracy using Levenshtein distance and shows a side-by-side diff of sent vs. received groups.

## Features

- Generates random 5-character groups from configurable character sets (letters, digits, punctuation, prosigns)
- Plays groups as synthesized Morse code audio using the Farnsworth timing method (separate character WPM and average WPM)
- Timed practice sessions with a live countdown
- Accuracy scoring with per-group diff output and a configurable error-rate threshold
- Fully configurable: tone frequency, volume, sample rate, WPM, session duration, and character set
- Settings are persisted across sessions
- Cross-platform: Windows, Linux (x64/arm64), and macOS

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Building the Windows installer

- [NSIS](https://nsis.sourceforge.io/) (Nullsoft Scriptable Install System)

### Building the Linux packages

- `dpkg-deb` (Debian/Ubuntu) — for `.deb` packages
- `rpmbuild` (Fedora/RHEL) — for `.rpm` packages

## Building

```powershell
dotnet build src/PentaGrammata.csproj
```

## Running tests

```powershell
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj
```

## Packaging

### Windows installer (NSIS)

```powershell
.\scripts\Build-Installer.ps1
# or with an explicit version
.\scripts\Build-Installer.ps1 -Version "1.1.0"
```

### Debian package

```bash
./scripts/Build-Deb-Installer.sh
# or for arm64
./scripts/Build-Deb-Installer.sh --runtime linux-arm64
```

### RPM package

```bash
./scripts/Build-Rpm-Installer.sh
# or with an explicit version and release number
./scripts/Build-Rpm-Installer.sh --version 1.1.0.0 --release 2
```

All packaging scripts read the version from `version.txt` by default and accept a `--version` / `-Version` flag to override it.

## Configuration

`appsettings.json` in the application directory controls all defaults:

| Section | Key | Default | Description |
|---|---|---|---|
| `Audio` | `SampleRate` | `44100` | Audio sample rate (Hz) |
| `Audio` | `Frequency` | `523.25` | Tone frequency (Hz) |
| `Audio` | `Volume` | `0.7` | Volume (0–1) |
| `Audio` | `BeepRampMs` | `4` | Envelope ramp time (ms) |
| `Practice` | `DefaultDurationMins` | `5` | Session length (minutes) |
| `Practice` | `CharacterWpm` | `20` | Character speed (WPM) |
| `Practice` | `AverageWpm` | `15` | Average (Farnsworth) speed (WPM) |
| `Practice` | `DefaultCharacterSet` | `Default` | Character set used on startup |
| `Practice` | `ErrorThreshold` | `10.0` | Maximum error rate (%) to pass |
| `CharacterSets` | _(named sets)_ | see below | Named sets selectable in the UI |

Default character sets:

| Name | Contents |
|---|---|
| `Default` | A–Z, 0–9, `/+?=` |
| `Letters` | A–Z |
| `Digits` | 0–9 |
| `Punctuation` | `/+?=` |
| `Full` | A–Z, 0–9, `/+?=`, prosigns `<ar><as><bk><bt><kn><sk>` |

All settings are editable at runtime through the in-app Settings dialog.

## Project structure

```
src/                  Application source
  Configuration/      Configuration model classes
  Interfaces/         Service abstractions
  Models/             Data transfer objects
  Services/           Business logic (Morse generation, playback, scoring)
  ViewModels/         MVVM view models (CommunityToolkit.Mvvm)
  Views/              Avalonia XAML views
tests/
  PentaGrammata.Tests/  MSTest unit tests (NSubstitute for mocking)
scripts/              Build and packaging scripts
installer/nsis/       NSIS installer script
```

## License

See [LICENSE](LICENSE).
