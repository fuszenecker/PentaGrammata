using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Platform.Storage;

using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Presentation;

public sealed class BackupDialogService : IBackupDialogService
{
    private static readonly FilePickerFileType ZipFileType = new("ZIP archive")
    {
        Patterns = ["*.zip"],
    };

    private readonly IWindowContext _windowContext;
    private readonly IUserBackupService _backupService;
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IInfoDialogService _infoDialogService;

    public BackupDialogService(
        IWindowContext windowContext,
        IUserBackupService backupService,
        IConfirmDialogService confirmDialogService,
        IInfoDialogService infoDialogService)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _confirmDialogService = confirmDialogService ?? throw new ArgumentNullException(nameof(confirmDialogService));
        _infoDialogService = infoDialogService ?? throw new ArgumentNullException(nameof(infoDialogService));
    }

    public async Task<BackupDialogOutcome> ShowExportAsync()
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return BackupDialogOutcome.Cancelled;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export PentaGrammata data",
            // Dated so repeated exports on different days suggest different names.
            SuggestedFileName = $"pentagrammata-backup-{DateTime.Now:yyyy-MM-dd}.zip",
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ZipFileType],
        });

        if (file is null)
        {
            return BackupDialogOutcome.Cancelled;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await _backupService.ExportAsync(stream);
        }
        catch (UserBackupException ex)
        {
            await _infoDialogService.ShowInfoAsync("Export failed", ex.Message);
            return BackupDialogOutcome.Cancelled;
        }

        return BackupDialogOutcome.Completed;
    }

    public async Task<BackupDialogOutcome> ShowImportAsync(Action? onImportStarted = null)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return BackupDialogOutcome.Cancelled;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import PentaGrammata data",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType],
        });

        var file = files?.FirstOrDefault();
        if (file is null)
        {
            return BackupDialogOutcome.Cancelled;
        }

        BackupContents contents;
        try
        {
            await using var inspectStream = await file.OpenReadAsync();
            contents = _backupService.InspectArchive(inspectStream)
                ?? throw new UserBackupException("The selected file is not a PentaGrammata backup archive.");
        }
        catch (UserBackupException ex)
        {
            await _infoDialogService.ShowInfoAsync("Import", ex.Message);
            return BackupDialogOutcome.Cancelled;
        }

        if (!await _confirmDialogService.ShowConfirmAsync("Import backup", BuildConfirmMessage(contents), "Replace"))
        {
            return BackupDialogOutcome.Cancelled;
        }

        try
        {
            onImportStarted?.Invoke();
            // A fresh stream: the inspection stream has been consumed and may not be
            // re-positioned by every provider.
            await using var importStream = await file.OpenReadAsync();
            var result = await _backupService.ImportAsync(importStream);
            await _infoDialogService.ShowInfoAsync(
                "Import complete",
                $"Imported {BuildContentsDescription(result.Contents)}. The pre-import restore point is saved at:\n{result.BackupPath}",
                detailHeading: "Restore point");
        }
        catch (UserBackupException ex)
        {
            var message = ex.BackupPath is null
                ? ex.Message
                : $"{ex.Message}\nRecovery archive: {ex.BackupPath}";
            await _infoDialogService.ShowInfoAsync("Import failed", message,
                detailHeading: ex.BackupPath is null ? null : "Recovery archive");
            return BackupDialogOutcome.Cancelled;
        }

        return BackupDialogOutcome.Completed;
    }

    public async Task<BackupDialogOutcome> ShowRestoreAsync(Action? onRestoreStarted = null)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null) return BackupDialogOutcome.Cancelled;

        if (!await _confirmDialogService.ShowConfirmAsync(
                "Restore previous data",
                "Restore the most recent snapshot created before an import? Current data will be saved as a recovery archive first.",
                "Restore"))
        {
            return BackupDialogOutcome.Cancelled;
        }

        try
        {
            onRestoreStarted?.Invoke();
            var result = await _backupService.RestoreLatestAsync();
            await _infoDialogService.ShowInfoAsync(
                "Restore complete",
                $"Restored {BuildContentsDescription(result.Contents)}. The data that was active before restore is saved at:\n{result.BackupPath}",
                detailHeading: "Recovery archive");
            return BackupDialogOutcome.Completed;
        }
        catch (UserBackupException ex)
        {
            var message = ex.BackupPath is null
                ? ex.Message
                : $"{ex.Message}\nRecovery archive: {ex.BackupPath}";
            await _infoDialogService.ShowInfoAsync("Restore failed", message,
                detailHeading: ex.BackupPath is null ? null : "Recovery archive");
            return BackupDialogOutcome.Cancelled;
        }
    }

    private static string BuildConfirmMessage(BackupContents contents)
    {
        var parts = new[]
        {
            contents.AppSettings ? "settings" : null,
            contents.Database ? "practice results" : null,
            contents.WindowSizes ? "window sizes" : null,
        }.Where(part => part is not null);

        var list = string.Join(", ", parts);
        return $"This will replace your current {list} with the contents of the selected backup. If applying the import fails, the app will restore your previous data.";
    }

    private static string BuildContentsDescription(BackupContents contents)
    {
        var parts = new[]
        {
            contents.AppSettings ? "settings" : null,
            contents.Database ? "practice results" : null,
            contents.WindowSizes ? "window sizes" : null,
        }.Where(part => part is not null);
        return string.Join(", ", parts);
    }
}
