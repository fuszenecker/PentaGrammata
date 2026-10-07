using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PentaGrammata.Interfaces;
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
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        foreach (var userConfigPath in _appPaths.UserConfigPaths)
        {
            builder.AddJsonFile(userConfigPath, optional: true, reloadOnChange: false);
        }

        var configRoot = builder.Build();
        var config = configRoot.Get<AppConfig>() ?? new AppConfig();
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

            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_userConfigPath)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
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
