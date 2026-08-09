# PentaGrammata

A cross-platform Morse code copying practice application built with .NET 10 and Avalonia UI.

PentaGrammata plays random five-character groups as Morse code audio and lets you type what you hear. After each session it scores your accuracy using Levenshtein distance and shows a side-by-side diff of sent vs. received groups.

## Features

- Generates random 5-character groups from configurable character sets (letters, digits, punctuation, prosigns)
- Optional custom text: enter your own text in Settings to send it verbatim instead of random groups
- Plays groups as synthesized Morse code audio using the Farnsworth timing method (separate character WPM and average WPM)
- Timed practice sessions with a live countdown
- Accuracy scoring with per-group diff output and a configurable error-rate threshold
- Optional auto-adjusting WPM: after each scored session the practice speed slows down when recent errors are high and speeds up when they are low (driven by the average error rate of the last N sessions); the dynamic WPM is kept in memory only and restarts from the configured WPM on each app start
- Fully configurable: tone frequency, volume, sample rate, WPM, session duration, and character set
- Settings are persisted across sessions
- Cross-platform: Windows, Linux (x64/arm64), and macOS

## Configuration and results

- Windows: `%AppData%\PentaGrammata` and `%LocalAppData%\PentaGrammata` are both searched; `%LocalAppData%\PentaGrammata` is used for writing.
- Linux: `$XDG_CONFIG_HOME/PentaGrammata` or `$HOME/.config/PentaGrammata`.
- macOS: audio playback is not yet fully implemented (placeholder only).

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
| `Audio` | `VolumeDb` | `-3.0` | CW signal level (dBFS; 0 = full scale) |
| `Audio` | `BeepRampMs` | `4` | Envelope ramp time (ms) |
| `Audio.Noise` | `Type` | `None` | Background noise: `None`, `Gaussian`, `Uniform`, `Pink` |
| `Audio.Noise` | `LevelDb` | `-15.0` | Noise level relative to the signal (dB) |
| `Audio.Noise` | `BandwidthHz` | `500.0` | Shared receiver filter width (Hz) |
| `Audio.Noise` | `AgcEnabled` | `true` | Automatic gain control on/off |
| `Audio.Noise` | `AgcDelaySeconds` | `0.4` | AGC release/delay (s) |
| `Audio.Noise` | `ApfEnabled` | `true` | Audio peak filter on/off |
| `Audio.Noise` | `ApfBandwidthHz` | `120.0` | Audio peak filter width (Hz) |
| `Audio.Noise` | `ApfPeakGainDb` | `-9.0` | Blend gain of the narrow-peak signal added on top of the passband, in dB relative to the passband level after AGC (0 dB = peak as loud as the passband; negative = subtler ring) |
| `Practice` | `DefaultDurationMins` | `1` | Session length (minutes) |
| `Practice` | `CharacterWpm` | `18` | Character speed (WPM) |
| `Practice` | `AverageWpm` | `18` | Average (Farnsworth) speed (WPM) |
| `Practice` | `DefaultCharacterSet` | `Default` | Character set used on startup |
| `Practice` | `ErrorThreshold` | `5.0` | Maximum error rate (%) to pass |
| `Practice` | `CustomText` | _(empty)_ | Your own text to send instead of generated groups; empty = generate as usual |
| `Practice` | `AutoAdjustWpm` | `false` | Dynamically adjust the practice WPM in memory after each scored session (slow down when the recent average error rate — or the session just finished — is above `ErrorThreshold`, speed up otherwise); the dynamic WPM is not persisted and restarts from the configured WPM on each app start |
| `Practice` | `AutoAdjustWindowSize` | `3` | Number of recent sessions whose error rates are averaged to drive `AutoAdjustWpm` |
| `UiPreferences` | `ReceivedTextFontFamily` | `Cascadia Mono` | Font family for the received-text box |
| `UiPreferences` | `ReceivedTextFontSize` | `20.0` | Font size for the received-text box (pt) |
| `UiPreferences` | `RevealSentTextAfterPractice` | `true` | Show the sent text automatically when the session ends |
| `CharacterSets` | _(named sets)_ | see below | Named sets selectable in the UI |

Default character sets:

| Name | Contents |
|---|---|
| `Default` | A–Z, 0–9, `/+?=` |
| `Letters` | A–Z |
| `Digits` | 0–9 |
| `Punctuation` | `/+?=` |
| `Full` | A–Z, 0–9, `/+?=`, prosigns `<ar><as><bk><bt><kn><sk>` |
| `Koch-LCWO-01-KM` | K, M |
| `Koch-LCWO-02-U` | K, M, U |
| `Koch-LCWO-03-R` | K, M, U, R |
| `Koch-LCWO-04-E` | K, M, U, R, E |
| `Koch-LCWO-05-S` | K, M, U, R, E, S |
| `Koch-LCWO-06-N` | K, M, U, R, E, S, N |
| `Koch-LCWO-07-A` | K, M, U, R, E, S, N, A |
| `Koch-LCWO-08-P` | K, M, U, R, E, S, N, A, P |
| `Koch-LCWO-09-T` | K, M, U, R, E, S, N, A, P, T |
| `Koch-LCWO-10-L` | K, M, U, R, E, S, N, A, P, T, L |
| `Koch-LCWO-11-W` | K, M, U, R, E, S, N, A, P, T, L, W |
| `Koch-LCWO-12-I` | K, M, U, R, E, S, N, A, P, T, L, W, I |
| `Koch-LCWO-14-J` | K, M, U, R, E, S, N, A, P, T, L, W, I, J |
| `Koch-LCWO-15-Z` | K, M, U, R, E, S, N, A, P, T, L, W, I, J, Z |
| `Koch-LCWO-16-=` | K, M, U, R, E, S, N, A, P, T, L, W, I, J, Z, `=` |
| `Koch-LCWO-17-F` | …+ F |
| `Koch-LCWO-18-O` | …+ O |
| `Koch-LCWO-19-Y` | …+ Y |
| `Koch-LCWO-20-+` | …+ `+` |
| `Koch-LCWO-21-V` | …+ V |
| `Koch-LCWO-22-G` | …+ G |
| `Koch-LCWO-23-5` | …+ 5 |
| `Koch-LCWO-24-/` | …+ `/` |
| `Koch-LCWO-25-Q` | …+ Q |
| `Koch-LCWO-26-9` | …+ 9 |
| `Koch-LCWO-27-2` | …+ 2 |
| `Koch-LCWO-28-H` | …+ H |
| `Koch-LCWO-29-3` | …+ 3 |
| `Koch-LCWO-30-8` | …+ 8 |
| `Koch-LCWO-31-B` | …+ B |
| `Koch-LCWO-32-?` | …+ `?` |
| `Koch-LCWO-33-4` | …+ 4 |
| `Koch-LCWO-34-7` | …+ 7 |
| `Koch-LCWO-35-C` | …+ C |
| `Koch-LCWO-36-1` | …+ 1 |
| `Koch-LCWO-37-D` | …+ D |
| `Koch-LCWO-38-6` | …+ 6 |
| `Koch-LCWO-39-0` | …+ 0 |
| `Koch-LCWO-40-X` | …+ X (full LCWO set) |

The Koch-LCWO sets follow the [LCWO](https://lcwo.net/) character introduction order. Each set is cumulative and **weighted** — it contains all characters from the previous sets plus the newly introduced one, with the new character repeated several times so it appears more frequently during practice.

Lesson 13 (`.`) is intentionally omitted because that character was deemed not useful for practice.

All settings are editable at runtime through the in-app Settings dialog.

### Noise simulation and signal chain

When a noise type other than `None` is selected, the audio passes through a four-stage receiver simulation:

1. **Mix** — the Morse tone and the generated noise are summed, with the noise level set by `LevelDb` (dB relative to the tone).
2. **Receiver filter** — a biquad band-pass filter centred on the tone frequency and `BandwidthHz` wide removes out-of-band noise, emulating a CW receiver's IF or audio filter. Narrower = tighter filter, less noise, easier copy.
3. **AGC** — an automatic gain control rides the combined level so the noise floor breathes up in the gaps and ducks under the tone, simulating the characteristic swelling of a real receiver. `AgcDelaySeconds` controls how slowly the gain recovers after a tone ends; larger values keep the floor suppressed longer between characters. Disable with `AgcEnabled = false` for a flat level.
4. **APF (audio peak filter)** — a second, narrower band-pass filter (`ApfBandwidthHz`) is run over the AGC-levelled signal and its output is *added* on top, creating a resonant peak at the tone frequency. This produces the characteristic CW "ring" that makes individual tones easier to distinguish in noise. `ApfPeakGainDb` sets the blend level relative to the passband signal after AGC: 0 dB adds the peak at full passband amplitude (very prominent ring); −9 dB (default) blends it at ≈ 35 % for a subtle ring. The APF runs after the AGC so the gain control never fights the peak contribution.

## Project structure

```
src/                  Application source
  Composition/        Dependency-injection registration
  Configuration/      Configuration model classes
  Converters/         Avalonia value converters
  Interfaces/         Service abstractions
  Models/             Data transfer objects
  Players/            Audio output stack (Morse rendering, platform players, noise/DSP)
  Presentation/       Dialog and window services
  Services/           Business logic (Morse generation, scoring, statistics)
  Stores/             Persistence (config JSON, statistics SQLite, window sizes)
  ViewModels/         MVVM view models (CommunityToolkit.Mvvm)
  Views/              Avalonia XAML views
tests/
  PentaGrammata.Tests/  MSTest unit tests (NSubstitute for mocking); folders mirror src/
scripts/              Build and packaging scripts
installer/nsis/       NSIS installer script
```

## License

See [LICENSE](LICENSE).
