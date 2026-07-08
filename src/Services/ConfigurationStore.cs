using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Services;

public sealed class ConfigurationStore : IConfigurationStore
{
    private readonly ILogger<ConfigurationStore> _logger;
    private readonly string? _userConfigPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ConfigurationStore(ILogger<ConfigurationStore> logger)
    {
        _logger = logger;
        _userConfigPath = ConfigurationPaths.GetPreferredPerUserConfigPath();
    }

    public AppConfig Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        foreach (var userConfigPath in ConfigurationPaths.GetPerUserConfigPaths())
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
        try
        {
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_userConfigPath, json).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
