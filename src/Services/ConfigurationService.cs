using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private const string DefaultCharacterSetName = "Default";
    private const string DefaultCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

    private readonly IConfigurationStore _store;
    private readonly ILogger<ConfigurationService> _logger;

    // Serializes saves: each SaveAsync chains onto the previous one so that rapid
    // or concurrent callers persist in order and never interleave file writes.
    private readonly object _saveGate = new();
    private Task _saveChain = Task.CompletedTask;

    public AppConfig Current { get; }

    public ConfigurationService(IConfigurationStore store, ILogger<ConfigurationService> logger)
    {
        _store = store;
        _logger = logger;
        Current = _store.Load();
        Normalize();
    }

    /// <summary>
    /// Establishes load-time invariants so the rest of the application can assume the
    /// configuration always has at least one usable character set and a non-null default
    /// selection. This is the single owner's responsibility, not each consumer's.
    /// </summary>
    private void Normalize()
    {
        var config = Current;

        if (config.CharacterSets.Count == 0
            || !config.CharacterSets.Any(kv => !string.IsNullOrWhiteSpace(kv.Value)))
        {
            config.CharacterSets[DefaultCharacterSetName] = DefaultCharacterSet;
        }

        if (string.IsNullOrWhiteSpace(config.Practice.DefaultCharacterSet))
        {
            config.Practice.DefaultCharacterSet = config.CharacterSets
                .First(kv => !string.IsNullOrWhiteSpace(kv.Value)).Key;
        }
    }

    public Task SaveAsync()
    {
        // Snapshot on the CALLER's thread (the UI thread, where all mutations happen)
        // so the clone can never race with a concurrent mutation of Current. This also
        // captures the state as it was at the moment SaveAsync was called, which is the
        // state the caller intended to persist.
        var snapshot = Current.Clone();

        lock (_saveGate)
        {
            _saveChain = _saveChain.ContinueWith(
                _ => PersistAsync(snapshot),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();

            return _saveChain;
        }
    }

    public void RequestSave()
    {
        // Fire-and-forget, but still ordered; failures are logged in PersistAsync
        // rather than being lost on an unobserved task.
        _ = SaveAsync();
    }

    public Task FlushAsync()
    {
        lock (_saveGate)
        {
            return _saveChain;
        }
    }

    public bool IsDialogSuppressed(string dialogKey)
    {
        if (string.IsNullOrEmpty(dialogKey))
        {
            return false;
        }

        return Current.UiPreferences.SuppressedDialogs.Contains(dialogKey);
    }

    public Task SuppressDialogAsync(string dialogKey)
    {
        if (string.IsNullOrEmpty(dialogKey) || IsDialogSuppressed(dialogKey))
        {
            return Task.CompletedTask;
        }

        Current.UiPreferences.SuppressedDialogs.Add(dialogKey);
        return SaveAsync();
    }

    public void SetPracticeDuration(int minutes)
    {
        if (Current.Practice.DefaultDurationMins == minutes)
        {
            return;
        }

        Current.Practice.DefaultDurationMins = minutes;
        RequestSave();
    }

    public void SetSelectedCharacterSet(string name)
    {
        if (Current.Practice.DefaultCharacterSet == name)
        {
            return;
        }

        Current.Practice.DefaultCharacterSet = name;
        RequestSave();
    }

    public void ApplyPracticeSettings(AppConfig settings)
    {
        var config = Current;
        config.Practice.DefaultDurationMins = settings.Practice.DefaultDurationMins;
        config.Practice.CharacterWpm = settings.Practice.CharacterWpm;
        config.Practice.AverageWpm = settings.Practice.AverageWpm;
        config.Audio.SampleRate = settings.Audio.SampleRate;
        config.Audio.Frequency = settings.Audio.Frequency;
        config.Audio.VolumeDb = settings.Audio.VolumeDb;
        config.Audio.BeepRampMs = settings.Audio.BeepRampMs;
        config.Audio.Noise = settings.Audio.Noise.Clone();
        config.Practice.DefaultCharacterSet = settings.Practice.DefaultCharacterSet;
        config.Practice.ErrorThreshold = settings.Practice.ErrorThreshold;
        config.Practice.CustomText = settings.Practice.CustomText;
        config.Practice.AutoAdjustWpm = settings.Practice.AutoAdjustWpm;
        config.Practice.AutoAdjustWindowSize = settings.Practice.AutoAdjustWindowSize;

        config.CharacterSets.Clear();
        foreach (var item in settings.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                config.CharacterSets[item.Key] = item.Value;
            }
        }

        RequestSave();
    }

    public Task ApplyUiPreferencesAsync(UiPreferences preferences)
    {
        Current.UiPreferences = preferences.Clone();
        return SaveAsync();
    }

    public void SetConfusionsHalfLife(double days)
    {
        if (Current.Analytics.ConfusionsHalfLifeDays == days)
        {
            return;
        }

        Current.Analytics.ConfusionsHalfLifeDays = days;
        RequestSave();
    }

    public Task UpsertCharacterSetAndSelectAsync(string name, string characters)
    {
        Current.CharacterSets[name] = characters;
        Current.Practice.DefaultCharacterSet = name;
        return SaveAsync();
    }

    private async Task PersistAsync(AppConfig snapshot)
    {
        try
        {
            await _store.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist configuration");
        }
    }
}
