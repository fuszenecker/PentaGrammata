# Avalonia headless scenarios

Run from the repository root:

```sh
dotnet test tests/PentaGrammata.Headless.Tests/PentaGrammata.Headless.Tests.csproj --logger "trx;LogFileName=headless-tests.trx" --results-directory TestResults
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj --logger "trx;LogFileName=unit-tests.trx" --results-directory TestResults
```

The project is included in `PentaGrammata.slnx`, so the existing CI solution test command also runs it. No desktop session, display server, sound device, or network connection is required. Skia renders the actual Avalonia views, including compiled bindings and converters.

Each scenario has a new service container and temporary user profile. Production configuration, controllers, scoring, Morse synthesis, analysis, dialog services, backup services, and SQLite stores run together. Only the audio device, HTTP transport, native file pickers, and profile paths are controlled. Pointer and keyboard input operate the views; numeric and selection controls are set through their public control properties. Tests inspect bound controls and persisted files as well as domain results.

The assembly serializes scenarios through a shared Avalonia dispatcher. Each journey has a 90 second deadline, with shorter condition waits that name the missing action. Cleanup closes owned windows, flushes settings, releases SQLite pools, disposes services, and deletes its temporary profile.

| Feature | Covered scenarios |
| --- | --- |
| Practice controls | Initial command states, random five-symbol groups, duration and palette changes, custom text bypass, live clock, playback completion, stop and restart, input focus and caret, disabling configuration during playback, empty copy gating |
| Alphabet | Every shipped character set, all Koch levels, every supported symbol, prosigns, whitespace normalization, comments and blank editor lines, parser rejection and repair, replacing and selecting sets |
| Scoring | Correct and case-insensitive copies, substitutions, insertions, deletions, missing and extra groups, whitespace-only copy, prosign symbol counts, exact pass threshold, visible diff text and colors |
| Result persistence | SQLite records, repeated scoring and reopening, disabled save after success, duplicate prevention, saved notification details, suppression, disk failure and retry, preventing closure during save |
| Adaptive speed | Windowed adjustment, newest failure veto, rising character speed, actual speed in statistics, reset after settings, repeated scoring without another adjustment |
| Morse settings | Save, Cancel and title-bar dismissal, WPM locking, error threshold, custom text validation, palette editor validation, settings write failure and recovery |
| Audio and receiver | Every sample rate, all noise types, all 32 noise/AGC/APF/QSB toggle combinations, dependent controls, tone/volume/ramp boundaries, every receiver numeric limit, persisted receiver fields, actual synthesized samples, distortion warning and recovery, device failure and retry |
| Display preferences | Font selection and size, minimum/maximum size, reveal and lowercase options, retaining typed copy, cancel and title-bar dismissal, persistence failure and retry |
| Trends | Empty history, all series toggles, daily passing speed ranges and gaps, SNR and QSB data, hover/zoom/pan input, CSV cancellation, export contents, write failure and retry |
| Correlation | Empty/single history, equal speed and equal errors, fitted history, rolling window filtering, configured threshold, historical points, rendered age colors, today's yellow, overlap brightness, settings persistence and close retry |
| Confusions | Empty and populated matrices, retention setting, generated practice palette and selection refresh, palette save failure and retry, retention save failure and close retry |
| Backup | Export/cancel/retry, complete and individual-component import, confirmation cancellation, corrupt/unsafe archives, old database migration, live settings/history reload, safety backup creation, refusing import without safety backup, rollback after apply failure, restore and recovery archive, missing restore point, missing/unreadable source including disappearance during confirmation |
| Windows and help | All application dialogs, About/version, update available/current/failure outcomes, window size restoration, info details and notification suppression |

The complementary `PentaGrammata.Tests` project covers isolated calculations, validation branches, DSP, store schemas, and service contracts. Native OS sound drivers, real native pickers, installer packaging, and GitHub availability require platform/integration checks; replacing those boundaries here allows reproducible headless UI runs. This suite is a concrete scenario inventory, not a claim of exhaustive coverage of every possible input or interleaving.
