using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using Promptuarium.Serializer;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Stores;

public sealed class ConfigurationStore : IConfigurationStore
{
    private readonly ILogger<ConfigurationStore> _logger;
    private readonly IAppPaths _appPaths;
    private readonly string? _userConfigPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ConfigurationStore(IAppPaths appPaths, ILogger<ConfigurationStore> logger)
    {
        _logger = logger;
        _appPaths = appPaths;
        _userConfigPath = appPaths.PreferredUserConfigPath;
    }

    public AppConfig Load()
    {
        // The bundled defaults, then each per-user file applied on top of them.
        var config = PromptuariumSerializer.Deserialize<AppConfig>(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName)), ConfigurationFormat.Options);

        foreach (var userConfigPath in _appPaths.UserConfigPaths)
        {
            if (File.Exists(userConfigPath))
            {
                PromptuariumSerializer.Populate(File.ReadAllBytes(userConfigPath), config, ConfigurationFormat.Options);
            }
        }

        return config.Clone();
    }

    public async Task SaveAsync(AppConfig configuration)
    {
        if (string.IsNullOrWhiteSpace(_userConfigPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_userConfigPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        // Callers (ConfigurationService) hand us an already-isolated snapshot, so we
        // don't clone again here. The lock still serializes concurrent file writes.
        await _saveLock.WaitAsync().ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(directory);

            var bytes = PromptuariumSerializer.Serialize(configuration, ConfigurationFormat.Options);

            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_userConfigPath)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllBytesAsync(temporaryPath, bytes).ConfigureAwait(false);
            File.Move(temporaryPath, _userConfigPath, overwrite: true);
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(cleanupException, "Could not remove temporary configuration file {TemporaryPath}", temporaryPath);
                }
            }
            _saveLock.Release();
        }
    }
}
