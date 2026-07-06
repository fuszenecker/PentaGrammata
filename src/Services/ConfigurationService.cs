using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationStore _store;
    private readonly ILogger<ConfigurationService> _logger;

    public AppConfig Current { get; }

    public ConfigurationService(IConfigurationStore store, ILogger<ConfigurationService> logger)
    {
        _store = store;
        _logger = logger;
        Current = _store.Load();
    }

    public Task SaveAsync() => PersistAsync();

    private async Task PersistAsync()
    {
        try
        {
            await _store.SaveAsync(Current).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist configuration");
        }
    }
}
