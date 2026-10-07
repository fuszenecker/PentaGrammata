# PentaGrammata architecture review and GPT-6-Luna implementation brief

## Scope and evidence

- Reviewed on **2026-10-07**, at commit **`63dcf921c46837e0191d736925a696248b98dda7`**.
- Reviewed the composition root, configuration ownership, session lifecycle, scoring, backup/import, stores, audio pipeline, view models, dialog adapters, chart controls, and relevant tests.
- This is a report only. Application code, tests, release version, and Git history were not changed.
- The existing [architecture-findings.md](architecture-findings.md) is historical and remains intact. Its A08–A13 fixes are present. A01–A07 still have relevant gaps; the automatic pre-import backup now mitigates A01, but does not make replacement transactional.
- **Build:** successful, zero warnings/errors, .NET SDK `10.0.100`.
- **Tests:** **284 passed, zero failed/skipped**. An initial concurrent build/test attempt caused an artifact lock; sequential compilation succeeded. The sandbox denied the test runner's local socket; the suite passed when rerun with approved access.
- Additional probes used the built application classes in a temporary console project, real configuration/SQLite stores where relevant, and deferred substitutes for playback and saving. All data stayed in temporary directories. No actual audio device, GUI window, native playback timing, or crash recovery was exercised.

**Evidence labels:** *Reproduced* means an isolated probe demonstrated the behavior. *Code path* means the source establishes the dependency or state sequence, without an interactive reproduction. *Structural* means a maintenance/testability concern; no runtime failure is claimed.

**Priorities:** P1 = repair data safety or session consistency first; P2 = repair observable correctness and contracts next; P3 = bounded responsiveness/maintenance improvement after correctness work. Estimates describe implementation scope, not guaranteed elapsed time.

## Findings and recommended order

| ID | Priority | Architectural smell | Evidence | Scope |
| --- | --- | --- | --- | --- |
| R01 | P1 | Import has no fully validated candidate boundary | Reproduced | Medium |
| R02 | P1 | Replacement deletes originals and has no rollback boundary | Reproduced + existing test | Large; split into two changes |
| R03 | P1 | Session metadata and adjustment identity depend on live configuration | Reproduced | Medium |
| R04 | P2 | Stopping is treated as completion before playback has drained | Reproduced | Medium |
| R05 | P2 | Scoring has incompatible symbol units and a string-based edit protocol | Reproduced | Small, then medium |
| R06 | P2 | Duration is a text-size heuristic rather than a session deadline | Reproduced rendering + code path | Large; stage carefully |
| R07 | P2 | Result persistence belongs to a disposable window, without an idempotency key | Reproduced state sequence | Medium; includes DB migration |
| R08 | P2 | Settings writer saves snapshots, while reader merges collections | Reproduced | Medium |
| R09 | P2 | Configuration saving conflates completion with success and writes in place | Reproduced failure swallowing + code path | Medium |
| R10 | P2 | Configuration invariants are enforced by individual entry points | Reproduced + code path | Medium |
| R11 | P2 | Analytics load failures escape navigation commands | Code path | Small |
| R12 | P3 | Settings edits synchronously run a full DSP probe on the UI thread | Code path; no latency benchmark | Medium |

Suggested implementation sequence: **R05 denominator fix → R08 → R10 → R01 → R02 → R03 → R04 → R07 → R06 → R09 → R11 → R05 typed edits → R12**. R08/R10 establish the loading and validation contracts R01 needs. R03/R04 establish session identity and lifecycle before persistence/deadline work.

## Instructions for GPT-6-Luna

1. Read `AGENTS.md` and this report. Locate code by the named methods; line numbers below refer to the reviewed commit and will move.
2. Implement one finding or one explicitly described stage at a time. Add the regression cases listed for that stage and run the affected tests before proceeding.
3. Keep `net10.0`, Avalonia compiled bindings, MVVM command enablement, namespace/folder boundaries, and central DI registration in `Composition/ServiceCollectionExtensions.cs`.
4. Put new domain contracts in `Interfaces/`, plain data in `Models/`, SQLite/file handling in `Stores/`, and toolkit/window operations in `Presentation/` or `Views/`. Inject new services; construct per-session records and owned helper objects normally.
5. Reuse the current evaluator, dynamic WPM adjuster, renderer factory, and store abstractions. No project split, general event bus, repository framework, or new chart library is needed.
6. Keep automatic pre-import backups, legacy DB migrations, partial archives, three flat archive names, live `Current` root identity, custom-text behavior, deterministic headroom analysis, and four-part update comparisons.
7. Do not bump `version.txt`, commit, push, tag, or publish as part of these repairs unless separately instructed. A SQLite schema migration is distinct from a release-version bump.
8. Update tests that intentionally assert the old behavior. In particular, partial import after failure and swallowing every awaited configuration-save failure are currently documented/tested policies; changing them requires explicit test updates, not compatibility wrappers that preserve the defect.

## R01 — Prepare and validate an import candidate before publishing it

**Locations:** `Services/UserBackupService.cs:137`, `:330`, `:352`, `:361`; `ViewModels/MainWindowViewModel.cs:189`; `Stores/PracticeResultStatisticsStore.cs:426`.

**Problem:** `ImportAsync` checks the SQLite header, deserializes settings, and checks only that window sizes have an object root. It then replaces files and mutates live configuration. The first practice-settings validation happens later in `MainWindowViewModel.ImportAsync`. The import service can therefore return success with unusable live data. SQLite validity, schema compatibility, and configuration validity have no shared preparation stage.

**Reproduced:** an archive containing only the 16-byte `SQLite format 3\0` header was accepted; the next statistics read threw `StatisticsStoreException`. Importing `{"Practice":{"CharacterWpm":0}}` also succeeded and published zero WPM; the existing validator rejected the resulting live configuration afterwards.

**Implement:**

1. Introduce a prepared import object containing staged files, the effective validated configuration when present, and parsed window sizes. Preparation must not mutate `Current` or replace live files.
2. Use the configuration-loading rules repaired in R08 to build the same effective configuration that will be applied. Run the validation repaired in R10 before automatic backup and replacement. JSON `null`, malformed sections, and invalid values must produce a user-facing `UserBackupException` or a documented normalization result; they must not fail after publication.
3. Delegate SQLite candidate checking to the store boundary. Open the candidate with pooling disabled; verify integrity, recognize the application's schema, reject unsupported future versions, and exercise migration/read compatibility on the private staged copy. Share migration/read logic with normal store operations. Do not put `SqliteConnection` in a view model or duplicate migration SQL in `UserBackupService`.
4. Preserve valid legacy databases without `schema_info`, and valid empty databases produced by a fresh-profile export. The latter can be bootstrapped on the staged copy. Reject unrelated nonempty SQLite schemas rather than silently treating them as application backups.
5. Parse window sizes using a shared store-owned format: valid dictionary entries, non-null size objects, finite positive dimensions. Reject an invalid candidate before replacement; an object-root check alone is insufficient.
6. Apply the validated configuration object after successful file replacement. Avoid rereading bytes through a loader whose collection rules differ from the preparation path. Keep the post-import controller apply/reset path, but it should no longer be the first validation.

**Regression cases:** header-only DB; structurally valid but unrelated DB; unsupported future schema; legacy migration; fresh-profile empty DB; zero WPM; invalid custom text; null sections; `{"TrendsDialog":null}`; valid partial archive. For rejected candidates assert original statistics/configuration/window sizes remain usable and no replacement/cache publication occurred. Keep current path-traversal protections.

## R02 — Give replacement a reversible commit boundary

**Locations:** `Stores/PracticeResultStatisticsStore.cs:238`; `Services/UserBackupService.cs:137`; `Interfaces/IPracticeResultStatisticsStore.cs`; `Services/ConfigurationService.cs:57`.

**Problem:** `ReplaceDatabaseAsync` deletes the live database and sidecars before copying the candidate. Import then changes DB, window sizes, and settings sequentially. An automatic ZIP preserves recoverable data, but a failed import can still leave the running application missing its DB or using a mixed profile.

**Reproduced:** after saving one record, replacement with a missing source threw and `File.Exists(DatabasePath)` was false. The existing test `ImportAsync_WhenReplacementFails_PreservesBackupAndReportsItsPath` explicitly demonstrates a late settings failure leaving the imported DB active.

**Stage A: safe individual DB replacement.**

1. Validate/copy the candidate to a unique sibling file on the destination filesystem before touching the live DB. A copy failure must preserve the old DB.
2. Hold the existing operation gate while establishing a consistent recoverable original, closing pooled connections, and swapping files. Preserve committed WAL content; renaming the main DB alone is not a sufficient rollback copy.
3. Use a same-filesystem replacement/rename strategy with rollback on failure. Handle a previously absent live DB. Reset schema initialization only in agreement with the file actually active after success or rollback.

**Stage B: coordinated multi-file import.**

1. Retain the mandatory automatic backup and its reported path. Additionally retain original file-presence information, original settings/window-size bytes, and an independent live configuration snapshot for exact rollback.
2. Stage every present entry before commit. Replace files, then publish cache/configuration/controller changes. On ordinary commit/apply failure, restore original files and live state; retain the automatic backup even when rollback succeeds.
3. Coordinate competing mutations for the whole commit/rollback interval. `await` yields the UI thread: the comment that import "holds" that thread does not prevent new saves, result writes, or settings commands. `FlushAsync` drains already queued saves; it does not suspend future ones.
4. Add an explicit maintenance scope or equivalent serialization contract. Keep the DB operation gate held across its rollback-sensitive interval and suspend/drain configuration writes. Disable relevant UI mutations while import is busy, and preserve service-level invariants for direct callers. Do not reacquire a semaphore through a public store method while already holding its maintenance lease; separate guarded entry points from lease-owned core operations.
5. Return failure with both the original cause and backup path. If rollback itself fails, report that distinctly. Do not claim crash-atomicity across three files: crash recovery would require an additional journal/recovery protocol.

**Regression cases:** failed candidate copy; failed DB swap; failed window-size write; failed settings write; failed live apply; cancellation before commit; concurrent queued save/result write; previously absent files; rollback failure. Original data must survive ordinary failures, successful import must stay live, and the backup path must remain available. Change the existing partial-import assertion to the new rollback contract.

## R03 — Model a session independently of current settings

**Locations:** `Services/PracticeController.cs:62`, `:110`, `:185`; `ViewModels/PracticeViewModel.cs:166`; `ViewModels/PracticeResultWindowViewModel.cs:85`; `Models/MorsePlaybackSettings.cs`.

**Problem:** only the two used WPM values are captured. Scoring and auto-adjust policy read live `Practice`; the result window receives current threshold/noise settings; its creation time becomes `RecordedAt`. `ResetDynamicWpm` also resets `_sessionResultRecorded`, so applying settings makes an old session eligible for another adjustment. Settings can be opened during practice, and can also change between playback and checking results.

**Reproduced:** a session started at a 5% threshold was changed to 100%; its 20% error result then passed. Scoring advanced average speed from 15 to 16; applying settings reset it to 15; reopening the old result advanced it to 16 again.

**Implement:**

1. Add a session context with a stable ID, start/completion timestamps, used WPM, immutable `MorsePlaybackSettings`, threshold, auto-adjust policy/window, mode, and generated/sent text. Capture it at start. Deep-copy mutable collections or use immutable/read-only storage; `init` properties containing mutable lists are not sufficient.
2. Add a scored completion object carrying that context, received text, and result. Supply it to the result VM/factory instead of passing independent metadata primitives plus current `NoiseSettings`.
3. Evaluate against the captured threshold, and map saved audio/noise metadata from the same captured playback settings. Set `RecordedAt` from session completion rather than window construction.
4. Track adjustment by session identity, separate from the dynamic-speed reset. Applying settings must still reset dynamic WPM/history as required, without making previous sessions new observations.
5. Define settings-generation handling: increment a configuration generation on settings apply; an already scored session cannot adjust again, and a session from a previous generation must not alter the newly reset dynamic speed. Its historical score still uses its captured threshold.
6. Keep pure evaluation/review separate from recording an adjustment and saving a record. Preserve received-text editing before evaluation; if it remains editable after evaluation, previews can recompute, but reopening/revising must not add another adjustment or another session row. Freeze the completion snapshot that is actually saved.

**Regression cases:** settings changed during playback; changed after playback but before checking; threshold/noise/WPM metadata consistency; opening results on another day; settings reset then reopening; RX revision; settings toggled from auto-adjust off to on after a run. Repeated scoring/opening of one session must produce at most one adjustment. Dynamic values must remain unpersisted.

## R04 — Keep stopping distinct from completed/stopped

**Locations:** `ViewModels/PracticeViewModel.cs:83`, `:146`, `:260`; `Services/PracticeController.cs:110`, `:177`; `Interfaces/IPracticeController.cs`.

**Problem:** `StopPractice` cancels playback, immediately sets `IsPracticeRunning=false`, and enables result checking while `StartAsync` is still completing. The controller and VM own different running flags. Cancellation sources are replaced without disposal, and the controller itself has no overlapping-start guard. A failed start also leaves `hasPracticeStarted=true`, making a failed attempt look eligible for scoring once text exists.

**Reproduced:** with a player that delays cancellation completion, immediately after Stop: controller running = true, VM running = false, Check result enabled = true. The normal start command also has the toolkit's execution guard; this finding does not claim the standard Practice button always permits overlapping starts.

**Implement:**

1. Give the controller one lifecycle/state contract: idle, running, stopping, completed, stopped, failed. Expose it through the interface or a suitable observable adapter.
2. Stop requests cancellation and moves to stopping. Enable results only after playback cleanup and completion metadata are finalized. Keep settings/import enablement consistent with this state.
3. Guard `StartAsync` itself against an active/stopping session. Use local per-run cancellation ownership so another invocation cannot replace the token source observed by the first call.
4. Dispose controller/timer cancellation sources when their tasks finish, and cancel/await active work during application shutdown before disposing DI services. Keep the existing bounded configuration flush.
5. Clear prior result eligibility when a new attempt starts. A failed attempt must not accidentally score the previous `LastGeneratedText`.

**Regression cases:** deferred cancellation completion; Stop twice; direct overlapping starts; render/generator failure following a successful session; stale cleanup from an old run; shutdown while playback is active. Verify Start/Stop/Check-result `CanExecute` throughout transitions.

## R05 — Use one symbol unit and typed edit data throughout scoring

**Locations:** `Services/PracticeResultEvaluator.cs:19`, `:59`; `Services/LevenshteinAlignment.cs:130`; `Services/MorseAlphabet.cs:79`; `Services/MorseGenerator.cs:11`; `Services/ConfusionObservationExtractor.cs:49`; `Models/PracticeResultRow.cs`; `ViewModels/PracticeResultWindowViewModel.cs:157`.

**Problem:** Levenshtein distance treats a prosign as one symbol, but `CharacterCount` sums string lengths. Separate tokenizers repeat the prosign grammar. The evaluator then serializes edits into bracket-marked strings, and the VM reparses them; literal received brackets are indistinguishable from delimiters. Alignment matrices are also rebuilt for distance, diff, and confusion extraction.

**Reproduced:** sending five `<bk>` symbols and receiving four produced 20 characters, one error, 5% and a passing result at a 5% threshold. The correct symbol count is five and error rate 20%. For sent `AB` / received `AX]YB`, the diff was `.[+X]Y].`; the VM classified `Y` and `]` as substitutions instead of part of an insertion.

**Stage A: small correctness fix.**

1. Count sent Morse symbols, excluding group whitespace, rather than UTF-16 string length. Use the same token grammar as alignment. Preserve case-insensitive scoring and one-symbol prosigns.
2. Consolidate tokenization around the existing alphabet grammar. Scoring must still represent arbitrary received characters, including unsupported ones; do not filter them out. A shared tokenizer is distinct from sendability validation.

**Stage B: remove the text protocol.**

1. Represent ordered edits with a UI-independent kind and expected/actual token in `Models/`. Calculate one alignment per row; derive distance, diff presentation, and confusion counts from it.
2. Map typed edits to existing `DiffSegmentKind` and text in the VM. Preserve the current visual convention for matches and grouping adjacent insertion/deletion segments. Remove bracket-string parsing after migrating all callers/tests.
3. Keep per-group alignment and group ordering; changing to whole-message distance would change scoring behavior.

**Regression cases:** five prosigns / one deletion = 20%; mixed letters/prosigns; ordinary five letters unchanged; lower-case RX; malformed RX prosign; inserted `X]Y`; literal period; extra received group. Confirm confusion totals agree with row edit counts.

**Data note:** historical statistics have aggregate counts/rates without the original session text, so old prosign denominators cannot be reliably repaired. Apply corrected counting to new results; do not rewrite old rates by guessing.

## R06 — Make duration an explicit session contract

**Locations:** `Services/PracticeController.cs:16`, `:144`; `ViewModels/PracticeViewModel.cs:229`; `Players/MorsePlayer.cs:15`; `Players/MorseSignalRenderer.cs:76`; `Interfaces/IAudioPlayer.cs`; `Views/MainWindow.axaml` duration tooltip.

**Problem:** duration is converted to a number of groups using `LengthCorrector=0.695`. Playback ends when the whole rendered message ends. The timer shows elapsed wall-clock time and never ends playback. There is no deadline, remaining-time contract, or record of which generated symbols were actually heard after an early stop.

**Reproduced rendering:** for a one-minute setting, character/average speeds 20/15, and 8 kHz audio, ten groups of `E` plus the current preamble/trailer rendered to **30.170 s**; ten groups of `0` rendered to **84.170 s**. These are rendered durations, not native-device timing measurements.

**Implement in stages after R03/R04:**

1. Add a controller-owned monotonic clock/deadline using injected `TimeProvider`. The VM observes remaining time; it does not own a second independent session timer. Register the clock centrally and use a controllable clock in tests.
2. Define the clock origin explicitly. Recommended implementation: the configured interval begins when audio playback starts, including the preamble; rendering time is a preparing state. Custom-text mode has no configured deadline and plays the entire normalized text.
3. Introduce a shared timing/render plan with token/group sample boundaries so generation uses actual Farnsworth durations rather than an empirical group-count constant. Generate enough material to cover the configured interval; bound buffers instead of rendering all 999 allowed minutes at once.
4. Extend playback completion/progress reporting as needed to distinguish generated text from transmitted text. At timeout or user Stop, finalize the actually transmitted complete symbols; specify how a partially transmitted final symbol/group is represented. Preamble/trailer must not enter the scored text. Implement the same contract on real backends.
5. Stop at the deadline and complete normally as a timed session, distinct from a user Stop or audio failure. A short final buffer must not make an otherwise active session silently finish far before the requested interval.
6. Do not solve this with `CancelAfter` alone: truncating audio while scoring every pre-generated group would manufacture errors for text the user never heard. Preserve Farnsworth gaps and DSP/QSB/AGC continuity across buffer boundaries.

**Regression cases:** short and long symbol sets; custom text longer than configured duration; pre-render delay; preamble accounting; deadline exhaustion; early Stop; partial final group; actual transmitted denominator; buffer-boundary timing. Use a fake clock/playback driver for lifecycle tests, shared timing-plan assertions for renderer tests, and a manual native-device smoke test on Windows/Linux for final cutoff behavior.

## R07 — Own saved-session state outside result windows

**Locations:** `ViewModels/PracticeResultWindowViewModel.cs:119`; `Presentation/PracticeResultWindowService.cs:31`; `Views/PracticeResultWindow.axaml.cs`; `ViewModels/PracticeViewModel.cs:31`; `Stores/PracticeResultStatisticsStore.cs:307`.

**Problem:** the only saved flag is in the result VM, and the caller reads it when the window closes. The result window has no saving-close guard. Closing before a save completes returns false; the save can finish afterwards, and reopening creates another independently saveable record. SQLite inserts have no stable session key.

**Reproduced state sequence:** a deferred save was started, the flag was read at simulated close, and the save then completed. The returned flag was false while the old VM ended true. Saving from a newly opened VM caused two writes. Real window closing was not exercised, but the service code reads that exact flag after `ShowDialog`, with no guard.

**Implement:**

1. Use the session/completion ID from R03. Place save state and the frozen record in a session/application service; result windows observe it. They must not rebuild session identity/timestamps on each opening.
2. Add a nullable legacy `session_id` column and a unique index for non-null IDs in a new SQLite schema migration. Existing rows remain readable. Require stable IDs for new session records.
3. Make saving idempotent: duplicate submission of the same ID must not insert another statistics row or child confusion rows. Perform parent/child writes within the existing transaction. Do not use timestamp equality as identity.
4. Disable Save while pending/completed; allow retry after genuine failure. As an immediate UI mitigation, reuse the Trends dialog's closing-guard pattern while saving. Service/store idempotency is still required even with the guard.
5. Notify the owner on save completion independently of whether the window still exists. A failed/successful info dialog must not determine whether the DB save is considered committed.

**Regression cases:** close while save is pending; reopen after completion; concurrent duplicate submission; retry after failure; one statistics row and one set of confusion children; old schema migration. Keep the current manual Save flow for this repair.

**Requirement discrepancy:** `AGENTS.md` says every session result is persisted, while the current UI requires explicit Save. Automatic saving is a separate product/workflow decision; do not silently add it as part of the duplicate-save fix.

## R08 — Make settings load/save round-trip faithfully

**Locations:** `Stores/ConfigurationStore.cs:27`, `:63`; `Services/ConfigurationService.cs:135`, `:205`; `tests/PentaGrammata.Tests/Services/UserBackupServiceTests.cs:522`.

**Problem:** the writer serializes a complete configuration snapshot. The reader overlays flattened JSON configuration providers, which merge dictionary keys instead of replacing the saved dictionary. Deleted bundled/roaming character sets reappear on reload or import. The backup tests substitute a JSON-deserialization loader, so they exercise different semantics from production.

**Reproduced with the real store:** bundled configuration had 47 sets. Saving a configuration containing only `Only=E` and loading it yielded **48 sets**, rather than one.

**Implement:**

1. Define collection replacement: a present `CharacterSets` object replaces the preceding dictionary; an absent one inherits it. Apply an equivalent explicit policy to list settings, especially `SuppressedDialogs`; test the chosen semantics.
2. Preserve bundled defaults and Windows roaming→local precedence for scalar/absent properties. Use a shared JSON document overlay/deserialization path that replaces present collections. Do not simply deserialize every partial user file into a default-constructed `AppConfiguration` and treat those defaults as explicit overrides.
3. Distinguish absent, explicitly empty, and invalid/null values. Hand that effective candidate to normalization/validation rather than silently merging back deleted keys.
4. Reuse the loader for staged imports and normal startup/reload. Keep `Current` root identity when publishing a new effective configuration.
5. Add real `ConfigurationStore` tests and a backup integration test using it. The existing substituted-store tests remain useful for other failure paths, but cannot prove production round-trips.

**Regression cases:** save only one set and reload exactly one; remove a bundled set; remove a roaming set with a local override; partial scalar override retains unrelated bundled settings; lists replaced rather than index-merged; export/import across profiles has identical effective collections.

## R09 — Separate best-effort persistence from awaited success

**Locations:** `Services/ConfigurationService.cs:57`, `:75`, `:233`; `Stores/ConfigurationStore.cs:43`; `ViewModels/MainWindowViewModel.cs:123`; `ViewModels/ConfusionsDialogViewModel.cs:178`; `Composition/ServiceCollectionExtensions.cs`; `App.axaml.cs:63`.

**Problem:** `PersistAsync` catches every exception. `SaveAsync`, `FlushAsync`, and awaited mutation methods therefore return successfully when nothing was saved. `ConfigurationStore` writes directly over the only user file; interruption/write failure can leave invalid JSON that startup loads without recovery. Logging alone does not communicate failure to callers; the composition root also registers logging without configuring an application provider.

**Reproduced:** a store returning a faulted task with `IOException("Disk full")` still allowed `await ConfigurationService.SaveAsync()` to finish successfully. This behavior is explicitly asserted by `SaveAsync_WhenStoreThrows_DoesNotPropagate`.

**Implement:**

1. Define an observable persistence outcome or a `ConfigurationStoreException` for awaited saves. Background `RequestSave` must still observe/log failures without creating unobserved tasks. Awaited Save/Flush paths must distinguish committed, failed, and intentionally memory-only persistence.
2. Keep snapshots captured on the owning thread and ordered saves. A failed save must not poison the queue and prevent a later valid save.
3. Serialize first, write a unique sibling temporary file, close/flush it, then replace the destination on the same filesystem. Retain the previous valid file on ordinary failure; clean temporary files without masking the main error. Reuse the file replacement primitive needed by R02 where appropriate.
4. Surface explicit settings/save failures through existing UI status/info services. Retain the deliberate best-effort treatment of window sizes and the bounded shutdown flush. Do not make changing a spinner throw an unhandled UI exception.
5. Keep platforms with no preferred config path usable in memory; expose that outcome honestly rather than calling it a disk-write failure.

**Regression cases:** disk/write failure reaches awaited caller; background failure is observed; failed save followed by successful save; write failure leaves previous valid JSON intact; no temporary-file leak; ordering under rapid edits; memory-only platform. Update the old swallowing test to the selected contract.

## R10 — Establish configuration invariants at the ownership boundary

**Locations:** `Services/ConfigurationService.cs:26`, `:37`, `:113`, `:198`; `Services/PracticeSettingsValidator.cs:10`; `Services/MorseGenerator.cs:11`; `Players/MorseSignalRenderer.cs:139`; `Configuration/AppConfiguration.cs`.

**Problem:** validation happens mainly on controller settings apply. Startup/import/direct configuration mutations take other paths. `Normalize` covers only some character-set defaults; setters accept invalid values. Comparison-only double checks accept NaN, and character sets are checked for nonempty text but not sendability. The renderer silently skips unsupported symbols.

**Reproduced:** the validator accepted `Audio.Frequency=NaN` and a selected set containing only `£`. A generated `£` can enter scored text even though the renderer cannot transmit it.

**Implement:**

1. Centralize effective-configuration normalization and validation under the configuration owner, with a pure validator reusable by settings UI and import preparation. Validate before publishing changes; avoid a cycle where the validator requires the controller/configuration owner.
2. Validate all relevant doubles with `double.IsFinite`; validate `NoiseType` membership and token sendability in each character set. Preserve repeated tokens: weighted "Practice confusions" sets intentionally repeat symbols.
3. Repair missing/invalid default-set selection during compatible load normalization, choosing a usable configured set. Distinguish deliberate fallback policy on startup from rejecting a malformed import.
4. Validate numeric/resource bounds at the service boundary, not only in AXAML controls. Establish supported sample-rate/timing bounds and avoid arithmetic overflow in renderer allocations. Keep auto-adjust free to climb above the settings spinner's 50-WPM limit; do not accidentally cap that required behavior.
5. Include analytics window/half-life and UI preference shape/finite dimensions in whole-configuration validation. Clone cannot substitute for validation; nullable JSON and partially initialized object graphs need intentional handling.
6. Route typed setters/upserts through the appropriate validation. Keep custom text whitespace normalization and its override of duration/set selection.

**Regression cases:** NaN/±infinity for threshold/audio/analytics values; invalid noise enum; null nested/list objects; unsupported/malformed set token; weighted repeated tokens; empty/missing default; invalid duration/upsert; valid custom text; settings apply leaves old state unchanged when rejected. Keep valid bundled sets and Koch progression accepted.

## R11 — Translate analytics load errors at the navigation boundary

**Locations:** `Presentation/TrendsDialogService.cs:32`; `Presentation/CorrelationDialogService.cs:30`; `Presentation/ConfusionsDialogService.cs:30`; `ViewModels/MainWindowViewModel.cs:168`; `ViewModels/TrendsDialogViewModel.cs:135`.

**Problem:** each analytics service awaits initialization before showing its dialog, and statistics failures propagate directly through the shell's `AsyncRelayCommand`. There is no corresponding user-facing load-error path. A corrupt/locked/unreadable DB can therefore fail navigation through an unhandled command exception rather than explain the problem and leave the shell usable.

**Evidence:** source call path only; no GUI crash was reproduced. The corrupt-DB probe in R01 demonstrates a store exception that can feed this path.

**Implement:**

1. Catch expected `StatisticsStoreException` at one consistent shell/presentation boundary. Log context and show an existing info/status message. Do not hide arbitrary programming exceptions with a catch-all.
2. Ensure failed initialization never leaves a partially opened/loading dialog or a command permanently busy. Treat user cancellation separately from storage failure where cancellation is added.
3. Keep UI-observable updates on the caller's UI context. `TrendsDialogViewModel.InitializeAsync` currently uses `ConfigureAwait(false)` before updating `Points`, command states, and summary. Today this normally happens before binding, so it is a structural inconsistency rather than evidence of an existing cross-thread GUI failure. Use the same UI-continuation policy as the other analytics VMs.

**Regression cases:** substituted statistics read failure for each navigation command; user-facing message and no exception escaping the expected error path; retry succeeds; no window opened on failure. If testing asynchronous collection updates, use a deliberately deferred task and controllable synchronization context, not an already completed substitute task.

## R12 — Schedule headroom probes without blocking settings editing

**Locations:** `ViewModels/MorseSettingsDialogViewModel.cs:213`, `:229`; `Services/AudioHeadroomAnalyzer.cs:29`, `:43`, `:51`; `Interfaces/IAudioHeadroomAnalyzer.cs`.

**Problem:** each audio-chain property notification synchronously renders a probe through the complete DSP chain. Coupled WPM setters can trigger multiple probes per edit. QSB probes can span tens of seconds of samples. The 45-second ceiling only constrains the QSB word-count calculation; the two-word minimum at 1 average WPM already represents about 120 seconds of audio samples. This is CPU work on the UI thread, not 120 seconds of playback, and no UI latency benchmark was performed.

**Implement after correctness repairs:**

1. Capture immutable playback settings on the UI thread; validate them before scheduling analysis. Debounce edits and cancel/supersede an earlier pending probe.
2. Add cancellation through the analyzer and its existing renderer call. Keep the fixed random seed and injected renderer factory.
3. Run DSP work off the UI thread. Publish only the latest completed request on the UI context; old analyses must not overwrite a newer warning.
4. Cancel/dispose probe work when the settings dialog closes. Analysis should not keep a closed dialog alive or block saving valid settings.
5. Enforce a real sample/work budget even at low WPM. A shortened probe must be described honestly in the analysis result rather than silently claiming full two-period coverage.

**Regression cases:** burst edits cause one latest publication; deferred old result cannot overwrite newer result; cancellation reaches renderer; close cancels work; deterministic repeat result; 1-WPM analysis respects the work budget. Use a manual responsiveness check for the settings dialog; do not add fragile stopwatch thresholds to CI.

## Smaller structural improvements to consider later

- `IConfigurationService.Current` exposes a writable object graph despite documenting exclusive ownership. Production callers mostly respect the rule today. A future read-only view/snapshot contract would enforce ownership; avoid a broad rewrite before the concrete defects above are repaired.
- `ConfigurationService.ApplyPracticeSettings` copies every scalar manually while `AppConfiguration.Clone` is documented as the copying authority. Once R08/R10 are established, replace whole owned sections with cloned sections while preserving analytics/UI sections outside the Morse settings dialog's scope. This reduces the chance that new settings disappear on apply. Keep aliasing tests for callers passing `Current` itself.
- The custom charts are large and repeat scale/layout/palette work. Extract pure scale/geometry helpers only when a concrete chart change needs them. No evidence here requires replacing the chart implementation or introducing another rendering framework.

## Verified healthy boundaries and existing constraints

- The composition root remains in `App.axaml.cs`; service registration is centralized.
- View models use injected service interfaces; native audio and persistence implementations stay behind their boundaries.
- Window/toolkit adapters and font conversion are in presentation/converter code rather than business services.
- Morse playback and deterministic headroom analysis share the injected renderer factory.
- Cancellation reaches the renderer/DSP/sample conversion; native error codes are translated; CSV export is awaitable and has a close guard; update comparison includes all four version components. These earlier repaired findings should not be reopened without new evidence.
- Correlation mathematics is isolated in a pure analysis service. Its no-fit behavior and current-threshold treatment match the stated contract; the daily range uses the stated strict below-threshold rule.
- The thin statistics service facade and single application assembly match repository conventions and are not findings.
- macOS playback remains a documented simulation. Windows currently uses native waveOut, although `AGENTS.md` mentions NAudio. Those are existing platform/documentation discrepancies, not reasons to change the backend during this repair series.
- Bundled settings use the required one-minute duration and 5% threshold; model fallback defaults still use five minutes and 10%. Align fallback policy during R10 if appropriate, while preserving saved user values.

## Final verification for implementation

Run compilation and tests sequentially to avoid shared-artifact locks:

```bash
dotnet build src/PentaGrammata.csproj
dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj
```

Use real temporary-directory stores for loading, import, rollback, and migration tests. Use deferred asynchronous substitutes for lifecycle/save races, and a fake clock for deadlines. Recheck settings bindings after VM/interface changes. Complete the named manual Windows/Linux playback and settings-dialog checks where unit tests cannot establish native or interactive behavior. Report any untested platform explicitly.
