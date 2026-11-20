# PentaGrammata

A cross-platform Morse code copying practice application built with .NET 10 and Avalonia UI.

PentaGrammata plays random five-character groups (or callsigns, cities, names, abbreviations and Q-codes, or your own text) as Morse code audio and lets you type what you hear. After each session it scores your accuracy using Levenshtein distance and shows a side-by-side diff of sent vs. received groups.

## Features

- Generates random 5-character groups from configurable character sets (letters, digits, punctuation, prosigns)
- Practice topics, chosen from a dropdown on the main window: random 5-character groups, callsigns (EMEA or worldwide), cities, names, ham abbreviations and Q-codes, your own text, or all of them mixed
- Topic lists editable on the Topics tab of Settings, kept in alphabetical order: callsign patterns (`#` is a random digit, a lowercase letter a random letter, uppercase letters, digits and `/` are sent as written, so `HA#xyz` gives e.g. `HA5ABC`), cities, names and abbreviations
- Custom text topic: your own text sent whole, line by line in order or with its lines shuffled; each line is one item, repeated and checked as a whole like a multi-word city
- Repetition as in a real QSO: each topic sends every item a random number of times in a row, between a minimum and a maximum set on its Topics tab (callsigns, cities and names twice by default, abbreviations and the lines of the custom text once; random groups are never repeated). Every repetition is played, but you copy each item once, and it is checked, revealed and shown in the result once
- Multi-word entries (`NEW YORK`, `LITTLE JOHN`, `QSL VIA BURO`) are one item: you type them with ordinary spaces, and the result window shows each one in a single row, with spaces that keep it on one line, scored word by word
- Plays groups as synthesized Morse code audio using the Farnsworth timing method (separate character WPM and average WPM)
- Receiver simulation: noise, band-pass filter, AGC and audio peak filter
- Ionospheric QSB (signal fading) with adjustable fade depth and period
- Timed practice sessions with a live countdown
- Accuracy scoring with per-group diff output and a configurable error-rate threshold
- Performance trends over saved sessions, exportable to CSV: character and average speed, error rate and threshold, SNR and QSB depth, plus a shaded daily speed range band spanning the lowest to the highest average WPM among that day's sessions that stayed under their error threshold
- Error rate correlation over a rolling window (default 10 days), on three tabs: against average speed, against signal-to-noise ratio (sessions with noise) and against QSB fade depth (sessions with QSB). Each session is a dot at its value and error rate, dimmed with age so the oldest sessions in the window are the faintest and today's are picked out in yellow, with the least-squares line, the current error threshold as a horizontal red line, the Pearson coefficient and the slope in percent per WPM or per dB
- Optional auto-adjusting WPM: after each scored session the practice speed slows down when recent errors are high and speeds up when they are low (driven by the average error rate of the last N sessions); the dynamic WPM is kept in memory only and restarts from the configured WPM on each app start
- Optional auto-adjusting signal-to-noise ratio and QSB depth, by the same decision: a passed session lowers the SNR / deepens the fade by its step (dB), a failed one does the opposite; only while noise / QSB is on; unlike the WPM, the new values are saved into the settings
- Backup export/import: File → Export packs your settings and practice statistics into a single dated `.prmt` file (a Promptuarium archive; window sizes stay local); File → Import restores them on any installation, taking effect immediately without a restart
- Fully configurable: tone frequency, volume, sample rate, WPM, session duration, and character set
- Settings are persisted across sessions
- Cross-platform: Windows, Linux (x64/arm64), and macOS

## Configuration and results

- Windows: `%AppData%\PentaGrammata` and `%LocalAppData%\PentaGrammata` are both searched; `%LocalAppData%\PentaGrammata` is used for writing.
- Linux: `$XDG_CONFIG_HOME/PentaGrammata` or `$HOME/.config/PentaGrammata`.
- macOS: audio is played through the native AudioToolbox (AudioQueue) API; no extra software is needed.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Building

```powershell
dotnet build src/PentaGrammata.csproj
```

## Running tests

```powershell
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj
```

## Installers and releases

The installers are built only by the `Create Installer` workflow (`.github/workflows/create-installer.yml`), never by hand: pushing a version tag (`MAJOR.MINOR.PATCH.BUILD`, the version in `version.txt`) builds the Windows `.exe` (NSIS), the `.deb` and `.rpm` (directly on an Ubuntu runner, with AppStream metadata stating the MIT license), and the macOS `.dmg`, and publishes them as a stable GitHub release, marked latest, with the notes of the version from `RELEASE_NOTES.md`. A tag whose version differs from `version.txt`, or which has no notes, is refused; CI checks the notes of `version.txt` on every push. The workflow can be started by hand for an existing tag to rebuild its release. The scripts in `scripts/` are the ones the workflow runs.

## Configuration

`settings.prmt` in the application directory holds all defaults. It is a binary Promptuarium container (`PRMT:CFG1-LE`, written by [Promptuarium.Serializer](https://github.com/fuszenecker/Promptuarium/blob/master/src/Promptuarium.Serializer/README.md), format in [SERIALIZER_FORMAT.md](https://github.com/fuszenecker/Promptuarium/blob/master/SERIALIZER_FORMAT.md)); view or edit it with Promptuarium's [`pedit`](https://github.com/fuszenecker/Promptuarium/blob/master/tools/PEdit/README.md) or PStudio. Your own changes are saved to a per-user `settings.prmt` layered on top of it, and window sizes go to a per-user `windows.prmt`. `appsettings.json` in the application directory holds only the `Logging` section.

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
| `Audio.Noise` | `AgcMaxGainDb` | `18.0` | Maximum amount the AGC may boost a weak signal or the noise floor (dB); caps the gain so the gaps between characters don't swell uncontrollably. 18 dB ≈ 8× |
| `Audio.Noise` | `ApfEnabled` | `true` | Audio peak filter on/off |
| `Audio.Noise` | `ApfBandwidthHz` | `120.0` | Audio peak filter width (Hz) |
| `Audio.Noise` | `ApfPeakGainDb` | `-9.0` | Blend gain of the narrow-peak signal added on top of the passband, in dB relative to the passband level after AGC (0 dB = peak as loud as the passband; negative = subtler ring) |
| `Audio.Noise` | `QsbEnabled` | `false` | Ionospheric signal fading (QSB) on/off: the signal strength wanders down and recovers unpredictably, independently of noise |
| `Audio.Noise` | `QsbDepthDb` | `10.0` | Deepest fade below full signal strength (dB); the signal wanders between full strength and this depth. 10 dB ≈ 3× down |
| `Audio.Noise` | `QsbPeriodSeconds` | `5.0` | Seconds per fade cycle; larger = slower, more gradual fading |
| _(root)_ | `AppMajorVersion` | current major | The major version that wrote the file; a per-user file of another major version is moved to `backups/` and the defaults are used |
| `Practice` | `DefaultDurationMins` | `1` | Session length (minutes) |
| `Practice` | `CharacterWpm` | `18` | Character speed (WPM) |
| `Practice` | `AverageWpm` | `18` | Average (Farnsworth) speed (WPM) |
| `Practice` | `DefaultCharacterSet` | `Default` | Character set used on startup |
| `Practice` | `ErrorThreshold` | `5.0` | Maximum error rate (%) to pass |
| `Practice` | `Topic` | `Groups` | What a session sends: `Groups`, `CallsignsEmea`, `CallsignsGlobal`, `Cities`, `Names`, `Abbreviations`, `CustomText` or `Mixed` |
| `Practice` | `CustomText` | _(empty)_ | The text the `CustomText` topic sends whole (the duration and character set do not apply); each line is one item; `Mixed` mixes its lines in too |
| `Practice` | `ShuffleCustomText` | `false` | Send the lines of the custom text in a random order |
| `Practice` | `AutoAdjustWpm` | `false` | Dynamically adjust the practice WPM in memory after each scored session (slow down when the recent average error rate — or the session just finished — is above `ErrorThreshold`, speed up otherwise); the dynamic WPM is not persisted and restarts from the configured WPM on each app start |
| `Practice` | `AutoAdjustWindowSize` | `1` | Number of recent sessions whose error rates are averaged to drive `AutoAdjustWpm`, `AutoAdjustSnr` and `AutoAdjustQsb` |
| `Practice` | `AutoAdjustSnr` | `false` | Adjust the signal-to-noise ratio after each saved session by the same decision as `AutoAdjustWpm` (a pass lowers it, a failure raises it); only while noise is on; the new level is saved into `Audio.Noise.LevelDb` |
| `Practice` | `AutoAdjustSnrStepDb` | `1` | How many dB one SNR adjustment moves (greater than 0, at most 20) |
| `Practice` | `AutoAdjustQsb` | `false` | Adjust the QSB fade depth after each saved session by the same decision (a pass deepens it, a failure makes it shallower); only while QSB is on; the new depth is saved into `Audio.Noise.QsbDepthDb` |
| `Practice` | `AutoAdjustQsbStepDb` | `1` | How many dB one QSB depth adjustment moves (greater than 0, at most 20) |
| `Topics` | `CallsignPatternsEmea` | ~180 patterns | Callsign patterns of the EMEA region (`#` digit, lowercase letter = random letter) |
| `Topics` | `CallsignPatternsGlobal` | ~310 patterns | Callsign patterns of the whole world |
| `Topics` | `Cities` | ~280 cities | Larger cities of the EMEA region and the capitals of the world |
| `Topics` | `Names` | ~180 names | First names popular in Europe and ham nicknames |
| `Topics` | `Abbreviations` | ~100 entries | Ham abbreviations and Q-codes |
| `Topics` | `Repetitions` | 2–2, 1–1 | How many times each item is sent in a row (`Minimum`–`Maximum`, 1 to 9) per topic: `CallsignsEmea`, `CallsignsGlobal`, `Cities` and `Names` 2–2, `Abbreviations` and `CustomText` (each word) 1–1 |
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

Before the receiver chain, an optional **QSB** stage can act on the clean Morse tone itself: with `QsbEnabled` the signal strength wanders down and recovers like a real sky-wave path — a new fade depth between full strength and `QsbDepthDb` is drawn roughly twice per `QsbPeriodSeconds`, and the gain eases toward it. QSB is applied independently of the noise setting (it also works with noise off), and a downstream AGC partly compensates for the fade, as a real rig would.

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
