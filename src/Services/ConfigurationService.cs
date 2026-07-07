using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.Configuration;
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

    public event EventHandler<Exception>? SaveFailed;

    public ConfigurationService(IConfigurationStore store, ILogger<ConfigurationService> logger)
    {
        _store = store;
        _logger = logger;
        Current = _store.Load();
    }

    public Task SaveAsync()
    {
        lock (_saveGate)
        {
            // Snapshot happens inside PersistAsync (via the store) at the moment the
            // save actually runs, so the most recent in-memory state is captured.
            _saveChain = _saveChain.ContinueWith(
                _ => PersistAsync(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();

            return _saveChain;
        }
    }

    public void RequestSave()
    {
        // Fire-and-forget, but still ordered and with failures reported through the
        // event/log rather than being lost on an unobserved task.
        _ = SaveAsync();
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

    private async Task PersistAsync()
    {
        try
        {
            await _store.SaveAsync(Current).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist configuration");
            SaveFailed?.Invoke(this, ex);
        }
    }
}
