# PentaGrammata — Agent Guidelines

Cross-platform Morse code copying practice app built with .NET 10 and Avalonia UI.

## Functional Requirements

### Core Practice Flow

1. The user starts a timed session (configurable duration, default 1 min).
2. The app generates random **5-character groups** from the selected character set and plays them as synthesized Morse code audio.
3. The user types what they hear; a live countdown shows remaining time.
4. When time runs out (or the user stops early), the session ends and accuracy is evaluated.

### Practice Topics

`Practice.Topic` (persisted, enum by name, default `Groups`) chooses what step 2 sends; the main window has a Topic dropdown above the character set. `IPracticeTextGeneratorFactory` (`Services/PracticeTextGeneratorFactory`) returns the generator of the topic built from the live configuration; `PracticeController` asks it for text worth `duration × average WPM × 0.695` groups.

- `Groups`: random 5-character groups from the selected character set (via `IMorseGenerator`).
- `CallsignsEmea` / `CallsignsGlobal`: callsigns expanded from `Topics.CallsignPatternsEmea` / `CallsignPatternsGlobal`: `#` is a random digit, a lowercase letter a random letter, uppercase letters, digits and `/` are sent as written.
- `Cities`, `Names`, `Abbreviations`: entries picked from `Topics.Cities` / `Names` / `Abbreviations`, sent in uppercase (an entry may hold several words).
- `CustomText`: `Practice.CustomText` sent whole, **line by line**: each non-empty line (whitespace inside it collapsed to word gaps) is one item (`PracticeItems.CustomTextLines`), its words joined by `_` exactly like a multi-word list entry: it is repeated as a whole, shuffled as a whole when `Practice.ShuffleCustomText` is on, and shown as one row of the result (scored word by word). Neither the duration nor the character set applies.
- `Mixed`: each item from a random source among groups (selected character set), the two callsign lists, cities, names, abbreviations and the lines of the custom text; empty lists are skipped.

**Repetition** (`Topics.Repetitions`, persisted, a `Repetition` of `Minimum`/`Maximum` per topic): every item is sent a random number of times in a row, between the minimum and the maximum (both included, 1 to `Repetition.Limit` = 9), as in a real QSO. Defaults: `CallsignsEmea`, `CallsignsGlobal`, `Cities`, `Names` 2–2; `Abbreviations` and `CustomText` 1–1 (the custom text repeats each line as a whole, after the optional shuffle of the lines). Groups are never repeated; `Mixed` repeats each item by its own topic's setting. A callsign is expanded once and that same callsign is repeated. **Every repetition is played, but the item is checked once.** The generator joins the repetitions of an item with `|` (`PracticeItems.RepeatJoiner`; it cannot be sent as Morse code, and the validator rejects it in the lists and the custom text): `LACI|LACI NEW_YORK|NEW_YORK`. `PracticeController` splits that text at once: it plays `PracticeItems.ToPlayback` (`LACI LACI NEW YORK NEW YORK`) and keeps `PracticeItems.ToChecked` (`LACI NEW_YORK`) as `LastGeneratedText`, which is what is checked, revealed, scored and saved, so the user copies each item once and the `|` never leaves the controller (never on screen). The letter budget counts every played repetition, since they all take time to send. Each Topics sub-tab shows the range below its list, exactly as wide as the list ("Repeat N to M times", `RepetitionViewModel`, which keeps the minimum not above the maximum); the validator rejects a range outside 1–9 or a minimum above the maximum.

**Multi-word items** (`Services/PracticeItems`): a list entry may hold several words ("NEW YORK", "LITTLE JOHN", "QSL VIA BURO"). The settings and the dialog keep ordinary spaces, but in the generated text the words of one item are joined by `_` (`PracticeItems.WordJoiner`; it cannot be sent as Morse code, so it never clashes with a real character), and items are separated by spaces. Playback turns `_` into an ordinary word gap, and the revealed sent text shows ordinary spaces. Everywhere the user types (the copy, the lists), `_` and whitespace mean the same: list entries are normalized to single ordinary spaces (`PracticeItems.NormalizeEntry`). Only the check result uses the grouping: `PracticeResultEvaluator` splits the sent text into items at spaces, gives an item of k words the next k received words (split at whitespace and `_`), and compares them word by word, so one row shows the whole item (the result window shows the joiner as a no-break space, `PracticeItems.ToDisplay`: it reads as a space and keeps the item on one line; `_` is never shown) while the character count and errors are the same as word by word (the joiner is not a character); the confusion statistics are also counted word by word. The lines of the custom text are multi-word items the same way (a line is an item); `_` typed into the custom text cannot be sent.

Word topics add items until they have sent as many letters as the groups would (spaces and joiners do not count). The character set dropdown is enabled only for `Groups` and `Mixed`, the duration for every topic but `CustomText`. "Practice confusions" switches the topic back to `Groups`. The default lists live in the bundled `settings.prmt` under `Topics`; a saved list replaces the bundled one whole. The settings dialog has a Topics tab (after Practice; Character sets follows it, before Audio) with one sub-tab per list and the custom text with its shuffle checkbox; the lists are edited one entry per line and kept in alphabetical order without repeats (`TopicListText`). The validator rejects a list entry or custom text `MorseAlphabet` cannot send, a callsign pattern with spaces, an empty custom text while `CustomText` is the topic, and an empty list for the selected topic.

### Audio Synthesis

- Morse code is synthesized using the **Farnsworth timing method**: `CharacterWpm` controls individual character speed; `AverageWpm` controls the overall (inter-character spacing) speed.
- Configurable tone frequency (Hz), volume (0–1), sample rate, and envelope ramp time.
- Platform-specific implementations: `WindowsAudioPlayer` (NAudio), `LinuxAudioPlayer`, `MacOSAudioPlayer` — all behind `IAudioPlayer`.

### Scoring & Results

- Accuracy is measured with **Levenshtein distance** per group; error rate = total edit distance / total sent characters × 100 %.
- A session is marked **successful** when `ErrorRatePercent ≤ ErrorThreshold` (default 5 %).
- A side-by-side diff of sent vs. received groups is displayed in the results window.
- Every session result (WPM settings, character count, error count, error rate) is persisted to a **SQLite database** (`practice-results.db`) in the per-user config directory.
- The **Trends** dialog charts every saved session (character and average speed, error rate and threshold, SNR, QSB depth) and exports the same records to CSV.
- The **Correlation** dialog has three tabs (`CorrelationTabViewModel`, one per `CorrelationAxis`): Speed, Signal-to-noise and QSB depth. Each plots one dot per session inside the shared rolling analysis window (`Analytics.CorrelationWindowDays`, persisted, default 10): the quantity on the horizontal axis (average WPM; SNR = `-NoiseLevelDb`, only sessions with noise; `QsbDepthDb`, only sessions with QSB on), error rate percent on the vertical axis. `CorrelationAnalysisService.Analyze(records, axis, …)` fits the least-squares line of error rate on that quantity and reports the Pearson coefficient; the dialog shows the line, r, R² and the slope in percent per WPM or dB. `ErrorCorrelation.HasFit` is false (no line drawn) when the window holds fewer than two sessions or when every session shares one value or one error rate, because the fit is then undefined. The horizontal red line is the error threshold currently configured (`Practice.ErrorThreshold`), read once when the dialog opens and included in the vertical domain so it stays visible even when every session scored well below it: only the current value is shown, the per-session thresholds are the trends chart's business. The dots carry their own age: each one is blended from the chart surface towards the session blue by where its `RecordedAt` falls between the oldest and the newest session in the window (the oldest keeps 30 percent of the color), and the sessions recorded today are drawn in yellow at full strength instead. The fade blends towards the opaque surface rather than lowering alpha, so two overlapping dots do not add up to a brighter third shade.
- The chart's shaded yellow band is the **daily speed range**: for each local calendar day, the lowest and the highest `AverageWpm` among that day's sessions whose error rate stayed below their own error threshold (`PracticeTrendPoint.DailyMinWpm` / `DailyMaxWpm`). Both ends are `NaN` on days where no session passed, and the band breaks across those days.

### Auto-adjusting WPM

- `Practice.AutoAdjustWpm` (persisted) enables an in-memory dynamic WPM that adapts after each scored session. `Practice.AutoAdjustWindowSize` (persisted, default 1) is N — the number of recent sessions averaged.
- The decision is made when a session's result is **saved** (`PracticeController.RecordSavedResult`, called once per session after the result window reports a save), never when it is merely checked: checking, re-checking or correcting the copy before saving moves nothing, an unsaved session does not count, and only the saved result's error rate enters the window. Then the error rates of the last N saved sessions are averaged and compared to `ErrorThreshold`: **above** it slows the average WPM by 1, **at or below** it speeds up by 1. The newest session's own error rate is also compared to the threshold and **vetoes a speed-up** — if the session that just finished failed, the speed drops even when the window average is still below the threshold. Speeding up therefore requires both the newest session and the window average to be clean. The window fills from the start of the application, so the first few saved sessions average over fewer than N error rates. When the dynamic average WPM reaches the character WPM, the character WPM is raised too so the average can keep climbing.
- **Auto-adjusting SNR and QSB depth:** `Practice.AutoAdjustSnr` / `AutoAdjustQsb` (persisted, default off) with `AutoAdjustSnrStepDb` / `AutoAdjustQsbStepDb` (persisted, default 1 dB, validated > 0 and ≤ 20) follow the **same decision** as the WPM (same window, same veto, made once per saved session by `DynamicDifficultyAdjuster`, which owns the window and all dynamic values): harder lowers the SNR / deepens the fade by the step, easier does the opposite, clamped to the dialog's ranges (SNR −20…80 dB, depth 1…40 dB). Each moves only while its feature is active (noise type not `None` / `QsbEnabled`); the window is fed whenever any of the three adjusts. Like the WPM they live **in memory only** (never written into the settings) and restart from the configured `Audio.Noise.LevelDb` / `QsbDepthDb` on construction and whenever settings are applied. The Practice tab groups every auto-adjust setting (WPM, window, SNR and QSB with their steps) in one "Auto-adjust" GroupBox. `PracticeController.LastUsedNoise` is the receiver settings the session played with (dynamic levels in place); the result window and the saved record use it. The status bar shows the next session's SNR and QSB depth while noise / QSB is on.
- The dynamic WPM lives only in `PracticeController` memory: it is never persisted, and it **restarts from the configured WPM** on construction and whenever settings are applied. Only the toggle and window size are saved.
- `PracticeController.LastUsedCharacterWpm` / `LastUsedAverageWpm` expose the WPM actually used by the most recent session; the result window and the saved statistics record use these (not the configured WPM) so they reflect the dynamic speed.

### Character Sets

- Named sets defined in the bundled `settings.prmt` under `CharacterSets`; the active set is chosen in Settings.
- Built-in sets: `Default` (A–Z, 0–9, `/+?=`), `Letters`, `Digits`, `Punctuation`, `Full` (includes prosigns), and the **Koch/LCWO progression** (levels 1–40, numbered as in LCWO).
- Koch level 13 is **intentionally absent** (`Koch-LCWO-12-I` is followed directly by `Koch-LCWO-14-J`), so there are 39 Koch sets, not 40. Do not "fix" the gap or renumber the levels.
- Prosigns are encoded as `<ar>`, `<as>`, `<bk>`, `<bt>`, `<kn>`, `<sk>`.

### Settings & Persistence

- All settings (WPM, duration, character set, audio parameters) are editable in a Settings dialog and saved to a per-user `settings.prmt`.
- Settings files are **Promptuarium containers** in the `PRMT:CFG1-LE` format (specified in Promptuarium's `SERIALIZER_FORMAT.md`; little-endian, BLOB = `0xF0`), read and written by the `Promptuarium.Serializer` package's `PromptuariumSerializer` with `Configuration/ConfigurationFormat.Options` (the configuration signature), which maps .NET objects to the container and back by reflection with no JSON in between (`byte[]` ↔ BLOB, numbers ↔ FLOAT64, enums by name, objects and string-keyed dictionaries ↔ OBJECT, collections ↔ ARRAY). The package is experimental, so the projects opt in with `NoWarn` `PRMTSER000`. Layering: the bundled `settings.prmt` (all defaults) is deserialized, then each per-user `settings.prmt` is applied onto it with `Populate` (objects and dictionaries merge member by member, arrays replace, NULL keeps the value underneath). `appsettings.json` stays in the application directory with only the `Logging` section. There is no per-user `appsettings.json` and no migration from the old JSON files.
- The settings classes in `Configuration/` and the window-size record carry `[PromptuariumComment("...")]` attributes: the serializer writes them as COMMENT nodes, so every written `settings.prmt`, `windows.prmt` and the settings entry of a backup explains itself in `pedit`/PStudio (readers ignore them). Give every new persisted property a comment with its unit.
- **Major version stamp:** `AppConfiguration.AppMajorVersion` records the major version of the application that wrote a `settings.prmt` (`ConfigurationFormat.CurrentMajorVersion`, from the assembly version, which local builds read from `version.txt` through the `.csproj` and the installer scripts pass as `-p:Version`). `ConfigurationStore.SaveAsync` stamps it; a file without it counts as `ConfigurationFormat.LegacyMajorVersion` (2). On load, a per-user file of another major version is not layered: it is moved to `backups/pentagrammata-settings-v<old>-<time>-<guid>.prmt` in the per-user data directory, the bundled defaults are used, and the main window tells the user where the old file went (`IConfigurationService.SetAsideSettingsBackupPath`, shown once on open). Exports stamp the current major; an import whose settings entry has another major is refused in `InspectArchive`, before the confirmation. Moving MAJOR is therefore the deliberate signal of a breaking settings change.
- Window sizes are stored in a per-user `windows.prmt` (same format) by `WindowSizeStore`.
- The bundled `src/settings.prmt` is a committed binary file (with the comments of the attributes); edit it with Promptuarium's `pedit`/PStudio (or regenerate it with the codec), not by hand.
- Config location: `%AppData%\PentaGrammata` and `%LocalAppData%\PentaGrammata` on Windows (local is preferred for writing); `$XDG_CONFIG_HOME/PentaGrammata` (fallback `~/.config/PentaGrammata`) on Linux.

### Backup Export & Import

- The File menu (before Settings) has Export, Import and Restore. Export saves a single Promptuarium archive containing the per-user `settings.prmt` and `practice-results.db` (window sizes are never part of a backup), with a dated suggested name (`pentagrammata-backup-YYYY-MM-DD.prmt`). The extension of every backup archive — exported, `before-import` and `before-restore` alike — is always `.prmt` (`IUserBackupService.FileExtension`); there is no ZIP support, not even for reading old backups. The archive is in the PPAK1-LE format of Promptuarium's `ppak` tool (`tools/PPak`, specified in its `PPAK_FORMAT.md`, signature `PRMT:PPAK1-LE`; it is the one thing import is strict about: the root must carry exactly `PRMT:PPAK1-LE`, byte by byte, so archives with the earlier `PRMT:PPAK1` signature, another case or no signature are rejected): one top-level entry per file, its name in the data, with `type`, `content`, `crc32` and `modified` info nodes; `Services/BackupArchive` writes and reads it. The database entry is a `VACUUM INTO` snapshot (consistent, no WAL sidecars); the settings entry is serialized from the live configuration, so it works on fresh profiles and platforms without a per-user config file (macOS), where the settings entry is omitted on export and applied in memory only on import.
- Import picks such an archive, checks the signature strictly (above), and otherwise reads it **tolerantly**: only the entry name and its `content` matter; the type, the time and any other info node are optional extras, unknown entries (including `windows.prmt`) are ignored, a repeated name keeps its first entry, and only the two flat entry names are honored (no path traversal). The one extra not ignored is a well-formed CRC-32 that does not match the content. Import then validates the contents and and shows a confirmation dialog before replacing anything. Everything is validated and extracted to temp files first; then the files present in the archive are replaced and the change is made **live**: pooled SQLite connections are cleared, the schema-initialized flag is reset (an older-schema database migrates on next use), and the live configuration is reloaded in place into the shared `Current` instance. After the import, the same validated apply path as the settings dialog pushes the reloaded config into the practice controller.
- `UserBackupService` (`Services/`, behind `IUserBackupService` in `Interfaces/`) owns the archive logic; `BackupDialogService` and `ConfirmDialogService` (`Presentation/`) own the pickers and the confirmation dialog.
- After archive validation and before replacing any data, import automatically exports the current data to a uniquely named `.prmt` archive in the per-user data directory's `backups` subdirectory. Import stops if this backup cannot be created. The info dialog shows the backup's full path after import, including when replacement fails so the original data can be recovered. Import also captures temporary database/file/live-configuration rollback state and restores it if an apply step fails. Restore applies the most recent `before-import` archive and preserves the currently active data in a separate `before-restore` archive.

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
  Exceptions/      # Exceptions thrown across layer boundaries (store, backup service)
  Models/          # Plain data records
  Configuration/   # Strongly-typed settings bound from settings.prmt
  Composition/     # DI registration (ServiceCollectionExtensions)
tests/
  PentaGrammata.Tests/   # MSTest + NSubstitute; folders mirror src/
```

Platform-specific audio is isolated in `Players/` (`WindowsAudioPlayer`, `LinuxAudioPlayer`, `MacOSAudioPlayer`) behind `IAudioPlayer`, selected by `AudioPlayerFactory`. `Players/` also holds `MorsePlayer` and the DSP helpers it owns (`BandPassFilter`, `AutomaticGainControl`, the noise generators); the `MorsePlaybackSettings` record `IMorsePlayer` and `IAudioHeadroomAnalyzer` take lives in `Models/` with the other cross-layer data records. `Stores/` holds the three persistence implementations; view models and services reach them only through `Interfaces/`.

Each folder's namespace matches its path (`PentaGrammata.Players`, `PentaGrammata.Stores`, …). The one deliberate exception is `Interfaces/`: contracts live there rather than beside their implementations, so `IMorsePlayer` and `IAudioPlayer` are in `PentaGrammata.Interfaces` while the classes implementing them are in `PentaGrammata.Players`. `Presentation/` is the exception to that exception — its `I*.cs` files sit next to their implementations.

## Build & Test

```bash
dotnet build src/PentaGrammata.csproj
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj
dotnet test tests/PentaGrammata.Headless.Tests/PentaGrammata.Headless.Tests.csproj
```

## Conventions

- ViewModels use `[ObservableProperty]` and `[RelayCommand]` source generators — keep classes `partial`.
- Commands must preserve `canExecute` (enable/disable semantics); do not remove `CanExecute` logic.
- Dependency injection: register everything in `Composition/ServiceCollectionExtensions.cs`; the container is built in `App.axaml.cs` (`Program.cs` holds no DI). Never use `new` for services.
- Nullable reference types are enabled project-wide — no `null!` suppressions without justification.
- Target framework: `net10.0`; do not downgrade or add TFM conditions.
- Avalonia compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`); keep bindings compile-time safe.

## Packaging

Installers are built **only by the pipeline** (`create-installer.yml`), never locally; the scripts in `scripts/` are what the workflow runs. They read the version from `version.txt`. Update `version.txt` when bumping the version — not the `.csproj`.

## Releases

Releases are produced automatically by the `Create Installer` workflow (`.github/workflows/create-installer.yml`) on every pushed version tag (`[0-9]+.[0-9]+.[0-9]+.[0-9]+`). It can also be run by hand (`workflow_dispatch`, input `tag`) for an existing tag, which rebuilds the installers and refreshes the release.

The version is **4-part: `MAJOR.MINOR.PATCH.BUILD`** (e.g. `1.11.4.4`), and the roles are fixed — do not deviate:

- **MAJOR** (`1`): frozen. Only the owner ever changes it. Never touch it.
- **MINOR**: the owner decides when it moves (usually tied to a feature/release they direct). Never touch it unless instructed.
- **PATCH**: the owner controls it. Never touch it unless instructed.
- **BUILD** (the 4th segment): the **only** part the agent ever increments, and **only when the owner commands a new release from the same code**. A build-only / CI / packaging / docs change with no code change is exactly such a case — bump BUILD and re-tag the same `MAJOR.MINOR.PATCH`.

To cut a new release (only when commanded):

1. **Increment the BUILD segment** in `version.txt` (`1.11.4.4` → `1.11.4.5`). Do not change MAJOR, MINOR, or PATCH.
2. **Write the release notes**: `RELEASE_NOTES.md` needs a section for the version without its BUILD number (`## 1.11.4`), newest first, describing what changed for the user, one line per paragraph or item (GitHub shows every line break); releases of the same code share the section. Check it with `scripts/Release-Notes.sh <version>`. Every release has proper notes: CI fails when the version in `version.txt` has no notes, and the workflow refuses a tag without notes or whose version is not the one in `version.txt`.
3. **Commit** the version bump together with the changes on `main`.
4. **Push `main`.**
5. **Tag** the commit as `<version>` (no leading `v`): `git tag 1.11.4.5`.
6. **Push the tag**: `git push origin 1.11.4.5`.

The tag push triggers `create-installer.yml`, which checks the version and the notes, runs the tests, and builds the Linux (`.deb` + `.rpm` built directly on `ubuntu-latest` with `dpkg-deb` and `rpmbuild`, no containers; both carry the AppStream metainfo `scripts/linux/io.github.fuszenecker.PentaGrammata.metainfo.xml`, which software centers read the license from), Windows (`.exe` via NSIS on `windows-latest`) and macOS (`.dmg` on `macos-latest`) installers in parallel, then creates a GitHub release named after the tag — **always a stable release, always marked `latest`** — with the notes of the version (from `RELEASE_NOTES.md`), the downloads, and all the installers attached. Do not create the release or upload assets by hand — the workflow owns that. Verify the run succeeded and the release lists all four assets before considering the release done.
