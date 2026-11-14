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
    private readonly int _currentMajorVersion;

    public ConfigurationStore(IAppPaths appPaths, ILogger<ConfigurationStore> logger)
        : this(appPaths, logger, ConfigurationFormat.CurrentMajorVersion)
    {
    }

    internal ConfigurationStore(IAppPaths appPaths, ILogger<ConfigurationStore> logger, int currentMajorVersion)
    {
        _logger = logger;
        _appPaths = appPaths;
        _userConfigPath = appPaths.PreferredUserConfigPath;
        _currentMajorVersion = currentMajorVersion;
    }

    /// <inheritdoc />
    public string? LastSetAsideBackupPath { get; private set; }

    public AppConfig Load()
    {
        LastSetAsideBackupPath = null;
        // The bundled defaults, then each per-user file applied on top of them.
        var config = PromptuariumSerializer.Deserialize<AppConfig>(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName)), ConfigurationFormat.Options);

        foreach (var userConfigPath in _appPaths.UserConfigPaths)
        {
            if (!File.Exists(userConfigPath))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(userConfigPath);
            var fileMajorVersion = ConfigurationFormat.ReadMajorVersion(bytes);
            if (fileMajorVersion != _currentMajorVersion)
            {
                // A breaking change: the file is not layered on the new defaults. It is moved
                // aside (not deleted) so it can be recovered, and the next save writes a fresh
                // file of the current major version.
                LastSetAsideBackupPath = SetAside(userConfigPath, fileMajorVersion);
                _logger.LogWarning(
                    "Settings file {Path} belongs to major version {FileMajor}, not {CurrentMajor}; moved it to {Backup} and starting from the defaults",
                    userConfigPath, fileMajorVersion, _currentMajorVersion, LastSetAsideBackupPath);
                continue;
            }

            PromptuariumSerializer.Populate(bytes, config, ConfigurationFormat.Options);
        }

        config.AppMajorVersion = _currentMajorVersion;
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

            configuration.AppMajorVersion = _currentMajorVersion;
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

    private string SetAside(string userConfigPath, int fileMajorVersion)
    {
        var directory = Path.Combine(_appPaths.AppDataDirectory, "backups");
        Directory.CreateDirectory(directory);
        var backupPath = Path.Combine(directory,
            $"pentagrammata-settings-v{fileMajorVersion}-{DateTime.Now:yyyy-MM-dd-HHmmss}-{Guid.NewGuid():N}.prmt");
        File.Move(userConfigPath, backupPath);
        return backupPath;
    }
}
