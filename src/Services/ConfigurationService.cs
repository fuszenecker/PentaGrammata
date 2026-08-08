using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

public sealed class ConfigurationService : IConfigurationService
{
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
