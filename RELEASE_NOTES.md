# Release notes

The notes of every release, the newest first. A section belongs to a version without its BUILD number (`## 2.0.0`): the releases of the same code (2.0.0.0, 2.0.0.1, …) share it. The release workflow puts the section of the tag into the GitHub release, with the downloads, and refuses a tag that has no section. A paragraph or an item is one line: GitHub shows every line break of a release.

## 2.1.2

- **Import accepts only backups signed `PRMT:PPAK1-LE`**, the signature of 2.1.1, compared byte by byte. Any other file is refused before anything is replaced.
- Backups made by 2.1.0 (signed `PRMT:PPAK1`) can no longer be imported, and Restore can not use the automatic backups 2.1.0 made before an import.

## 2.1.1

- The settings files explain themselves: `settings.prmt`, `windows.prmt` and the settings in a backup carry a comment for every setting (what it is, in which unit), shown by Promptuarium's `pedit` and PStudio. Reading ignores the comments, so the files of 2.1.0 are read as before.
- Backups are written in Promptuarium's specified ppak format, [PPAK1-LE](https://github.com/fuszenecker/Promptuarium/blob/master/examples/PPak/PPAK_FORMAT.md) (signature `PRMT:PPAK1-LE`). The backups made by 2.1.0 can still be imported.

## 2.1.0

- The settings are read and written by [Promptuarium.Serializer](https://www.nuget.org/packages/Promptuarium.Serializer/), the serializer of the Promptuarium package, instead of PentaGrammata's own. The files stay the same (`settings.prmt` and `windows.prmt`, `PRMT:CFG1-LE`), so the settings of 2.0 are kept. The format is specified in [SERIALIZER_FORMAT.md](https://github.com/fuszenecker/Promptuarium/blob/master/SERIALIZER_FORMAT.md).
- **Backups are Promptuarium files.** File → Export saves a single `.prmt` file (`pentagrammata-backup-YYYY-MM-DD.prmt`) instead of a ZIP, with the settings and the practice statistics; the automatic backups made before an import or a restore are `.prmt` files too. Promptuarium's `ppak` tool can list, verify and unpack them.
- Window sizes are no longer part of a backup: every installation keeps its own.
- ZIP backups made by earlier versions can not be imported any more.

## 2.0.0

The settings and the window sizes are stored in Promptuarium containers instead of JSON files.

- **The settings start from the defaults after upgrading.** They move from the per-user `appsettings.json` to `settings.prmt`, a Promptuarium container in the `PRMT:CFG1-LE` format ([CONFIG_FORMAT.md](https://github.com/fuszenecker/PentaGrammata/blob/2.0.0.0/docs/CONFIG_FORMAT.md)), and the old file is not migrated. The practice statistics are kept.
- The window sizes move from `window-sizes.json` to `windows.prmt` (the windows open in their default sizes once).
- The bundled defaults are a `settings.prmt` too; the per-user file is layered on top of them, member by member, so it holds only what was changed.
- Backups (File → Export) carry `settings.prmt` and `windows.prmt`. A backup made by an earlier version still restores the practice statistics, but not its settings and window sizes.
- The bundled `appsettings.json` keeps only the logging settings.

## 1.15.1

- The File menu has an Exit item.
- A new Edit menu: Undo, Redo, Cut, Copy, Paste and Select all on the received text.

## 1.15.0

- Native audio on macOS (AudioToolbox) instead of the placeholder player.
- 1.15.0.1 and 1.15.0.2 update the libraries (Avalonia, Microsoft.Extensions, SQLite, …); 1.15.0.3 adds a macOS disk image (Apple Silicon, not signed) to the installers.

## 1.14.3

- The heavy work (rendering the audio, analysing the sessions) runs off the user interface thread, so the windows stay responsive.
- Import keeps a backup of the current data, and File → Restore brings it back.
- Scoring treats the prosigns (`<ar>`, `<sk>`, …) as single symbols.
- Fixes: a session result is saved once only, and the settings are saved reliably.

## 1.14.2

- New default settings.
- The summary row of the trends dialog is centred.
- Internal clean-up of the layers (no visible change).

## 1.14.1

- Fix: importing a backup no longer wipes the character sets.
- The About dialog and the documentation describe the backup export and import.

## 1.14.0

- A File menu with Export and Import: the settings, the practice statistics and the window sizes in one ZIP file, with a dated name. Import asks for confirmation, and the imported data takes effect at once, without a restart.
- Fix: the grey surface of the trends chart is drawn where the chart is, edge to edge.

## 1.13.4

- The correlation dots show their age: the older a session in the window, the fainter its dot; today's sessions are yellow.

## 1.13.3

- The correlation window draws the current error threshold as a red line, so the sessions above it read as failed.

## 1.13.2

- The clipping warning reads `clipping`.

## 1.13.1

- The settings dialog warns when the audio and noise settings will clip (with the peak level and the share of clipped samples), computed by the real signal chain. Saving is not blocked.

## 1.13.0

- Analysis → Correlation: every session of a rolling window (default 10 days) as a dot at its average speed and error rate, with the least-squares line, a ±1σ band, and the Pearson r, R² and slope.
- A new character set, `Extended+`, with more punctuation.

## 1.12.2

- The trends chart shades the daily speed range: from the lowest to the highest average speed of the day's sessions that stayed below their error threshold.
- The About dialog is updated.

## 1.12.1

- The About dialog and the README describe QSB.

## 1.12.0

- QSB (signal fading): the signal fades in and out unpredictably, with an adjustable depth (1–40 dB) and period (1–20 s), on the new Noise tab of the settings; the AGC reacts to it as a real receiver would.
- A round settings button next to the received text.
- The SNR band of the trends chart is scaled over all sessions, not only the visible ones.

## 1.11.6

- The confusion matrix has zebra stripes, and its red deepens with the confusion score.

## 1.11.5

- The trends chart shows each day's maximum average speed (of the sessions below the error threshold) as a yellow area; the character and average speed lines have distinct colors.

## 1.11.4

- AGC peak gain: a limit on how much the AGC may boost a weak signal or the noise floor.
- 1.11.4.1 improves the superencode helper script; 1.11.4.4 builds the Windows installer on Windows.

## 1.11.3

- New default buttons in the dialogs.

## 1.11.2

- Fix: the character set text box.

## 1.11.1

- The Linux packages can be built in Podman containers (Ubuntu LTS and Rocky Linux), with nothing but Podman on the host.
- A new About dialog.

## 1.11.0

- The trends can be exported to CSV.
- The practice statistics are saved more robustly (no "database is locked"), the session times are in UTC, and the last change of the settings is no longer lost at exit.
- A large internal clean-up of the architecture.

## 1.10.0

- Auto-adjusting speed: after each session, the speed slows down when the recent errors are high and speeds up when they are low (the average of the last 3 sessions by default).
- A new logo, and a polished user interface.

## 1.9.3

- The audio on Windows is played through P/Invoke.

## 1.9.2

- Character sets for `.` and `,`, and extended characters.
- Fix: the superencode helper script.

## 1.9.1

- Fix: the diff of the sent and the received text.

## 1.9.0

- Custom text: a text of your own, sent verbatim instead of the random groups.
- The revealed text is in lower case.
- The superencode helper script (`scripts/superencode.js`) turns a text into practice text (re-encoding, irregular columnar transposition).

## 1.8.2

- Help → Check for updates.
- Fix: the practice of the confusions.

## 1.8.1

- The window sizes are saved.

## 1.8.0

- Analysis: the trends of the saved sessions (speed, error rate and threshold), and the confusions — the characters you mix up, weighted by age (half-life, 1 day by default), as a matrix, with a character set to practice them.
- Fix: the alignment of the sent and the received text.

## 1.7.1

- Better noise, expressed as SNR, and a nicer user interface.

## 1.7.0

- The noise settings are saved.

## 1.6.1

- Better Farnsworth timing.

## 1.6.0

- A new look without rounded corners, a font family setting, window sizing, the audio peak filter after the AGC, and better defaults.

## 1.5.0

- A font size setting for the received text.
- An option to reveal the sent text automatically after the session.

## 1.4.3

- New default settings.

## 1.4.2

- Fix: when the speeds are locked, the average speed follows the character speed.

## 1.4.1

- Fix: the configuration.

## 1.4.0

- "Do not show again" for the dialog after saving a result.
- Autofill.

## 1.3.3

- The maximum speed is 50 WPM.
- Fix: the RPM package.

## 1.3.2

- A result can not be saved twice.

## 1.3.1

- Fix: the default settings (`appsettings.json`).
- An RPM package (Rocky Linux / RHEL / Fedora) joins the installers.

## 1.3.0

- Koch method character sets, numbered as in LCWO.
- The text wraps.
- Fix: the length corrector of the timing.

## 1.2.1

- The info dialog is modal, and the button colors are fixed.

## 1.2.0

- The practice results are saved (SQLite), and can be reviewed later.
- An application icon.

## 1.1.0

- An error threshold: a session is successful when its error rate stays below it.
- `vvv=` and `<ar>` around the practice, and the controls are disabled while practising.
- A refreshed user interface.

## 1.0.0

The first release: random five-character groups played as Morse code with Farnsworth timing, a settings window (speeds, frequency, volume, duration, character set), and a results window comparing the sent and the received text (Levenshtein distance, colored diff). Installers for Windows and Debian/Ubuntu.
