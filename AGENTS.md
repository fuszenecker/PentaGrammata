# PentaGrammata — Agent Guidelines

Cross-platform Morse code copying practice app built with .NET 10 and Avalonia UI.

## Functional Requirements

### Core Practice Flow

1. The user starts a timed session (configurable duration, default 1 min).
2. The app generates random **5-character groups** from the selected character set and plays them as synthesized Morse code audio.
3. The user types what they hear; a live countdown shows remaining time.
4. When time runs out (or the user stops early), the session ends and accuracy is evaluated.

### Audio Synthesis

- Morse code is synthesized using the **Farnsworth timing method**: `CharacterWpm` controls individual character speed; `AverageWpm` controls the overall (inter-character spacing) speed.
- Configurable tone frequency (Hz), volume (0–1), sample rate, and envelope ramp time.
- Platform-specific implementations: `WindowsAudioPlayer` (NAudio), `LinuxAudioPlayer`, `MacOSAudioPlayer` — all behind `IAudioPlayer`.

### Scoring & Results

- Accuracy is measured with **Levenshtein distance** per group; error rate = total edit distance / total sent characters × 100 %.
- A session is marked **successful** when `ErrorRatePercent ≤ ErrorThreshold` (default 5 %).
- A side-by-side diff of sent vs. received groups is displayed in the results window.
- Every session result (WPM settings, character count, error count, error rate) is persisted to a **SQLite database** (`practice-results.db`) in the per-user config directory.

### Character Sets

- Named sets defined in `appsettings.json` under `CharacterSets`; the active set is chosen in Settings.
- Built-in sets: `Default` (A–Z, 0–9, `/+?=`), `Letters`, `Digits`, `Punctuation`, `Full` (includes prosigns), and a full **Koch/LCWO progression** (40 levels).
- Prosigns are encoded as `<ar>`, `<as>`, `<bk>`, `<bt>`, `<kn>`, `<sk>`.

### Settings & Persistence

- All settings (WPM, duration, character set, audio parameters) are editable in a Settings dialog and saved to a per-user `appsettings.json`.
- Config location: `%AppData%\PentaGrammata` and `%LocalAppData%\PentaGrammata` on Windows (local is preferred for writing); `$XDG_CONFIG_HOME/PentaGrammata` (fallback `~/.config/PentaGrammata`) on Linux.

## Architecture

```
src/
  Views/           # Avalonia AXAML + code-behind (minimal logic)
  ViewModels/      # CommunityToolkit.Mvvm partial classes; IAsyncRelayCommand / IRelayCommand
  Services/        # Business logic implementing Interfaces/
  Interfaces/      # Abstractions injected via Microsoft.Extensions.DependencyInjection
  Models/          # Plain data records
  Configuration/   # Strongly-typed settings bound from appsettings.json
tests/
  PentaGrammata.Tests/   # MSTest + NSubstitute
```

Platform-specific audio is isolated in `Services/` (`WindowsAudioPlayer`, `LinuxAudioPlayer`, `MacOSAudioPlayer`) behind `IAudioPlayer`.

## Build & Test

```bash
dotnet build src/PentaGrammata.csproj
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj
```

## Conventions

- ViewModels use `[ObservableProperty]` and `[RelayCommand]` source generators — keep classes `partial`.
- Commands must preserve `canExecute` (enable/disable semantics); do not remove `CanExecute` logic.
- Dependency injection: register everything in `Program.cs`; never use `new` for services.
- Nullable reference types are enabled project-wide — no `null!` suppressions without justification.
- Target framework: `net10.0`; do not downgrade or add TFM conditions.
- Avalonia compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`); keep bindings compile-time safe.

## Packaging

Scripts in `scripts/` read the version from `version.txt`. Update `version.txt` when bumping the version — not the `.csproj`.
