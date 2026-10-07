using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;
using PentaGrammata.Exceptions;

namespace PentaGrammata.ViewModels;

/// <summary>
/// Shell view model for the main window: owns the menu/dialog orchestration (settings, UI
/// settings, about, trends, confusions, updates, backup export/import) and the character-set selection shared by the
/// combo and the confusions flow. The practice session itself lives on
/// <see cref="Practice"/>, exposed for the main window's bindings.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPracticeController _practiceController;
    private readonly IConfigurationService _configurationService;
    private readonly IMorseSettingsDialogService _settingsDialogService;
    private readonly IUiSettingsDialogService _uiSettingsDialogService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly ITrendsDialogService _trendsDialogService;
    private readonly IConfusionsDialogService _confusionsDialogService;
    private readonly ICorrelationDialogService _correlationDialogService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IInfoDialogService _infoDialogService;
    private readonly IBackupDialogService _backupDialogService;
    private readonly ILogger<MainWindowViewModel> _logger;

    // Set while the character-set list is being swapped: the bound ComboBox reacts to a new
    // ItemsSource by pushing its own (now stale) SelectedItem back into the view model, which
    // would otherwise overwrite the controller's freshly chosen set.
    private bool suppressSelectedCharacterSetPush;

    public PracticeViewModel Practice { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenUiSettingsCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }
    public IAsyncRelayCommand OpenTrendsCommand { get; }
    public IAsyncRelayCommand OpenConfusionsCommand { get; }
    public IAsyncRelayCommand OpenCorrelationCommand { get; }
    public IAsyncRelayCommand CheckUpdatesCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }

    [ObservableProperty]
    private string[] characterSets = [];

    [ObservableProperty]
    private string selectedCharacterSet = "Default";

    [ObservableProperty]
    private string receivedTextFontFamily = string.Empty;

    [ObservableProperty]
    private double receivedTextFontSize = 20.0;

    [ObservableProperty]
    private bool isBackupOperationRunning;

    public bool CanInteractWithMainWindow => !IsBackupOperationRunning;

    public MainWindowViewModel(
        IPracticeController practiceController,
        IConfigurationService configurationService,
        IMorseSettingsDialogService settingsDialogService,
        IUiSettingsDialogService uiSettingsDialogService,
        IAboutDialogService aboutDialogService,
        ITrendsDialogService trendsDialogService,
        IConfusionsDialogService confusionsDialogService,
        ICorrelationDialogService correlationDialogService,
        IUpdateChecker updateChecker,
        IInfoDialogService infoDialogService,
        IBackupDialogService backupDialogService,
        PracticeViewModel practice,
        ILogger<MainWindowViewModel> logger)
    {
        _practiceController = practiceController;
        _configurationService = configurationService;
        _settingsDialogService = settingsDialogService;
        _uiSettingsDialogService = uiSettingsDialogService;
        _aboutDialogService = aboutDialogService;
        _trendsDialogService = trendsDialogService;
        _confusionsDialogService = confusionsDialogService;
        _correlationDialogService = correlationDialogService;
        _updateChecker = updateChecker;
        _infoDialogService = infoDialogService;
        _backupDialogService = backupDialogService;
        _logger = logger;
        Practice = practice;

        RefreshCharacterSets();
        ReceivedTextFontFamily = _configurationService.Current.UiPreferences.ReceivedTextFontFamily;
        ReceivedTextFontSize = _configurationService.Current.UiPreferences.ReceivedTextFontSize;

        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsDialogAsync, CanChangePracticeConfiguration);
        OpenUiSettingsCommand = new AsyncRelayCommand(OpenUiSettingsDialogAsync, CanChangePracticeConfiguration);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        OpenTrendsCommand = new AsyncRelayCommand(OpenTrendsAsync);
        OpenConfusionsCommand = new AsyncRelayCommand(OpenConfusionsAsync, CanChangePracticeConfiguration);
        OpenCorrelationCommand = new AsyncRelayCommand(OpenCorrelationAsync);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanChangePracticeConfiguration);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, CanChangePracticeConfiguration);

        Practice.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PracticeViewModel.IsPracticeOperationActive))
            {
                NotifyConfigurationCommandStates();
            }
        };
    }

    public async Task OpenSettingsDialogAsync()
    {
        if (!CanChangePracticeConfiguration()) return;
        var newSettings = await _settingsDialogService.ShowSettingsDialogAsync(_practiceController.CreateSettingsSnapshot());
        if (newSettings is null)
            return;

        if (!_practiceController.TryApplySettings(newSettings, out var error))
        {
            Practice.DisplayStatusMessage(error, StatusLevel.Error);
            return;
        }

        RefreshCharacterSets();
        Practice.RefreshFromAppliedSettings();
        await FlushConfigurationAsync("Settings were applied, but could not be saved.");
    }

    public async Task OpenUiSettingsDialogAsync()
    {
        if (!CanChangePracticeConfiguration()) return;
        var newPrefs = await _uiSettingsDialogService.ShowUiSettingsDialogAsync(
            _configurationService.Current.UiPreferences);
        if (newPrefs is null)
            return;

        ReceivedTextFontFamily = newPrefs.ReceivedTextFontFamily;
        ReceivedTextFontSize = newPrefs.ReceivedTextFontSize;
        try
        {
            await _configurationService.ApplyUiPreferencesAsync(newPrefs);
        }
        catch (ConfigurationPersistenceException ex)
        {
            await _infoDialogService.ShowInfoAsync("Settings not saved", ex.InnerException?.Message ?? ex.Message);
        }
    }

    public Task OpenAboutAsync()
    {
        return _aboutDialogService.ShowAboutAsync();
    }

    public async Task CheckUpdatesAsync()
    {
        var result = await _updateChecker.CheckAsync();

        if (!result.Succeeded)
        {
            await _infoDialogService.ShowInfoAsync("Check for updates", result.Error ?? "Could not check for updates.");
            return;
        }

        if (result.UpdateAvailable)
        {
            var message = $"A new version is available: {result.LatestVersion} (you have {result.CurrentVersion}).";
            if (!string.IsNullOrEmpty(result.ReleaseUrl))
            {
                message += $"\n{result.ReleaseUrl}";
            }

            await _infoDialogService.ShowInfoAsync("Update available", message, detailHeading: "Release page");
        }
        else
        {
            await _infoDialogService.ShowInfoAsync(
                "Check for updates",
                $"You are running the latest version ({result.CurrentVersion}).");
        }
    }

    public Task OpenTrendsAsync()
    {
        return _trendsDialogService.ShowTrendsAsync();
    }

    public async Task OpenConfusionsAsync()
    {
        if (!CanChangePracticeConfiguration()) return;
        await _confusionsDialogService.ShowConfusionsAsync();
        RefreshCharacterSets();
    }

    public Task OpenCorrelationAsync()
    {
        return _correlationDialogService.ShowCorrelationAsync();
    }

    public Task ExportAsync()
    {
        return _backupDialogService.ShowExportAsync();
    }

    public async Task ImportAsync()
    {
        if (!CanChangePracticeConfiguration()) return;
        try
        {
            if (await _backupDialogService.ShowImportAsync(() => IsBackupOperationRunning = true) != BackupDialogOutcome.Completed)
            {
                return;
            }

            // The backup service already reloaded the live configuration in place; push the
            // (possibly new) settings through the same validated apply path as the settings
            // dialog, which also resets the dynamic WPM from the imported values.
            if (!_practiceController.TryApplySettings(_configurationService.Current, out var error))
            {
                Practice.DisplayStatusMessage(error, StatusLevel.Error);
                return;
            }

            RefreshCharacterSets();
            Practice.RefreshFromAppliedSettings();

            // Mirror OpenUiSettingsDialogAsync for the main-window-owned UI preferences.
            var prefs = _configurationService.Current.UiPreferences;
            ReceivedTextFontFamily = prefs.ReceivedTextFontFamily;
            ReceivedTextFontSize = prefs.ReceivedTextFontSize;

            if (!await FlushConfigurationAsync("Backup data was imported, but its settings could not be saved."))
            {
                return;
            }

            Practice.DisplayStatusMessage("Backup imported.", StatusLevel.Neutral);
        }
        finally
        {
            IsBackupOperationRunning = false;
        }
    }

    public async Task RestoreAsync()
    {
        if (!CanChangePracticeConfiguration()) return;
        try
        {
            if (await _backupDialogService.ShowRestoreAsync(() => IsBackupOperationRunning = true) != BackupDialogOutcome.Completed)
            {
                return;
            }

            if (!_practiceController.TryApplySettings(_configurationService.Current, out var error))
            {
                Practice.DisplayStatusMessage(error, StatusLevel.Error);
                return;
            }

            RefreshCharacterSets();
            Practice.RefreshFromAppliedSettings();
            var prefs = _configurationService.Current.UiPreferences;
            ReceivedTextFontFamily = prefs.ReceivedTextFontFamily;
            ReceivedTextFontSize = prefs.ReceivedTextFontSize;

            if (!await FlushConfigurationAsync("Data was restored, but its settings could not be saved."))
            {
                return;
            }

            Practice.DisplayStatusMessage("Previous data restored.", StatusLevel.Neutral);
        }
        finally
        {
            IsBackupOperationRunning = false;
        }
    }

    partial void OnIsBackupOperationRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteractWithMainWindow));
        NotifyConfigurationCommandStates();
    }

    private bool CanChangePracticeConfiguration() => !Practice.IsPracticeOperationActive && !IsBackupOperationRunning;

    private void NotifyConfigurationCommandStates()
    {
        OpenSettingsCommand.NotifyCanExecuteChanged();
        OpenUiSettingsCommand.NotifyCanExecuteChanged();
        OpenConfusionsCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> FlushConfigurationAsync(string failureMessage)
    {
        try
        {
            await _configurationService.FlushAsync();
            return true;
        }
        catch (ConfigurationPersistenceException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            Practice.DisplayStatusMessage($"{failureMessage} {detail}", StatusLevel.Error);
            return false;
        }
    }

    /// <summary>
    /// Re-reads the available character sets and the controller's current selection. The
    /// intended selection is captured before the list is replaced, because assigning
    /// <see cref="CharacterSets"/> makes the bound ComboBox write its stale selection back
    /// into <see cref="SelectedCharacterSet"/>; that push is suppressed so it cannot
    /// overwrite a set just chosen elsewhere (e.g. "Practice confusions").
    /// </summary>
    private void RefreshCharacterSets()
    {
        var selected = _practiceController.SelectedCharacterSet;

        suppressSelectedCharacterSetPush = true;
        try
        {
            CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        }
        finally
        {
            suppressSelectedCharacterSetPush = false;
        }

        SelectedCharacterSet = selected;
    }

    partial void OnSelectedCharacterSetChanged(string value)
    {
        if (suppressSelectedCharacterSetPush)
        {
            return;
        }

        _practiceController.SelectedCharacterSet = value;
    }
}
