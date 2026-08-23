# PentaGrammata — Agent Guidelines

Cross-platform Morse code copying practice app built with .NET 10 and Avalonia UI.

## Functional Requirements

### Core Practice Flow

1. The user starts a timed session (configurable duration, default 1 min).
2. The app generates random **5-character groups** from the selected character set and plays them as synthesized Morse code audio.
3. The user types what they hear; a live countdown shows remaining time.
4. When time runs out (or the user stops early), the session ends and accuracy is evaluated.

If `Practice.CustomText` is non-blank, step 2 is skipped entirely: that text is sent verbatim
(whitespace collapsed to word gaps) and neither the character set nor the duration applies. The
settings dialog rejects custom text containing anything `MorseAlphabet` cannot send.

### Audio Synthesis

- Morse code is synthesized using the **Farnsworth timing method**: `CharacterWpm` controls individual character speed; `AverageWpm` controls the overall (inter-character spacing) speed.
- Configurable tone frequency (Hz), volume (0–1), sample rate, and envelope ramp time.
- Platform-specific implementations: `WindowsAudioPlayer` (NAudio), `LinuxAudioPlayer`, `MacOSAudioPlayer` — all behind `IAudioPlayer`.

### Scoring & Results

- Accuracy is measured with **Levenshtein distance** per group; error rate = total edit distance / total sent characters × 100 %.
- A session is marked **successful** when `ErrorRatePercent ≤ ErrorThreshold` (default 5 %).
- A side-by-side diff of sent vs. received groups is displayed in the results window.
- Every session result (WPM settings, character count, error count, error rate) is persisted to a **SQLite database** (`practice-results.db`) in the per-user config directory.

### Auto-adjusting WPM

- `Practice.AutoAdjustWpm` (persisted) enables an in-memory dynamic WPM that adapts after each scored session. `Practice.AutoAdjustWindowSize` (persisted, default 3) is N — the number of recent sessions averaged.
- After `BuildResult`, the error rates of the last N sessions are averaged and compared to `ErrorThreshold`: **above** it slows the average WPM by 1, **at or below** it speeds up by 1. The newest session's own error rate is also compared to the threshold and **vetoes a speed-up** — if the session that just finished failed, the speed drops even when the window average is still below the threshold. Speeding up therefore requires both the newest session and the window average to be clean. The window fills from the start of the application, so the first few sessions average over fewer than N error rates. When the dynamic average WPM reaches the character WPM, the character WPM is raised too so the average can keep climbing.
- The dynamic WPM lives only in `PracticeController` memory: it is never persisted, and it **restarts from the configured WPM** on construction and whenever settings are applied. Only the toggle and window size are saved.
- `PracticeController.LastUsedCharacterWpm` / `LastUsedAverageWpm` expose the WPM actually used by the most recent session; the result window and the saved statistics record use these (not the configured WPM) so they reflect the dynamic speed.

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
  Converters/      # IValueConverter implementations keeping toolkit types out of view models
  Presentation/    # Dialog/window services and the dialog view-model factory
  Services/        # Business logic implementing Interfaces/
  Players/         # Audio output stack: Morse rendering, platform players, noise/DSP
  Stores/          # Persistence surfaces (JSON config, SQLite statistics, window sizes)
  Interfaces/      # Abstractions injected via Microsoft.Extensions.DependencyInjection
  Models/          # Plain data records
  Configuration/   # Strongly-typed settings bound from appsettings.json
  Composition/     # DI registration (ServiceCollectionExtensions)
tests/
  PentaGrammata.Tests/   # MSTest + NSubstitute; folders mirror src/
```

Platform-specific audio is isolated in `Players/` (`WindowsAudioPlayer`, `LinuxAudioPlayer`, `MacOSAudioPlayer`) behind `IAudioPlayer`, selected by `AudioPlayerFactory`. `Players/` also holds `MorsePlayer`, its `MorsePlaybackSettings` record, and the DSP helpers it owns (`BandPassFilter`, `AutomaticGainControl`, the noise generators). `Stores/` holds the three persistence implementations; view models and services reach them only through `Interfaces/`.

Each folder's namespace matches its path (`PentaGrammata.Players`, `PentaGrammata.Stores`, …). The one deliberate exception is `Interfaces/`: contracts live there rather than beside their implementations, so `IMorsePlayer` and `IAudioPlayer` are in `PentaGrammata.Interfaces` while the classes implementing them are in `PentaGrammata.Players`. `Presentation/` is the exception to that exception — its `I*.cs` files sit next to their implementations.

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

## Releases

Releases are produced automatically by the `Create Installer` workflow (`.github/workflows/create-installer.yml`) on every pushed tag. The version is **always strict semver** — `MAJOR.MINOR.PATCH` (e.g. `1.11.5`). Never a 4th build-number segment, no exceptions.

To cut a new release:

1. **Bump the version in `version.txt`** to the next semver value:
   - **Build/CI/packaging/docs-only changes (no code change):** bump the **patch** (`1.11.4` → `1.11.5`). This is the only case that calls for a build bump.
   - **Code changes:** bump per semver as the change warrants (patch for fixes, minor for backward-compatible features, major for breaking changes).
2. **Commit** the version bump together with the changes on `main`.
3. **Push `main`.**
4. **Tag** the commit as `<version>` (no leading `v`): `git tag 1.11.5`.
5. **Push the tag**: `git push origin 1.11.5`.

The tag push triggers `create-installer.yml`, which builds the Linux (`.deb` + `.rpm` via Podman on `ubuntu-latest`) and Windows (`.exe` via NSIS on `windows-latest`) installers in parallel, then creates a GitHub **pre-release** (not `latest`) named after the tag with all three installers attached. Do not create the release or upload assets by hand — the workflow owns that. Verify the run succeeded and the release lists all three assets before considering the release done.
