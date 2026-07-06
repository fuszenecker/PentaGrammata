using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using AppConfig = PentaGrammata.Configuration.Configuration;

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
        return Clone(config);
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

        var snapshot = Clone(configuration);

        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
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

    private AppConfig Clone(AppConfig configuration)
    {
        var characterSets = new CharacterSets();
        foreach (var kv in configuration.CharacterSets)
        {
            characterSets[kv.Key] = kv.Value;
        }

        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = configuration.Practice.DefaultDurationMins,
                CharacterWpm = configuration.Practice.CharacterWpm,
                AverageWpm = configuration.Practice.AverageWpm,
                DefaultCharacterSet = configuration.Practice.DefaultCharacterSet,
                ErrorThreshold = configuration.Practice.ErrorThreshold,
            },
            Audio = new Audio
            {
                SampleRate = configuration.Audio.SampleRate,
                Frequency = configuration.Audio.Frequency,
                Volume = configuration.Audio.Volume,
                BeepRampMs = configuration.Audio.BeepRampMs,
            },
            CharacterSets = characterSets,
            UiPreferences = new UiPreferences
            {
                SuppressedDialogs = [.. (configuration.UiPreferences?.SuppressedDialogs ?? [])],
                ReceivedTextFontSize = configuration.UiPreferences?.ReceivedTextFontSize ?? 24.0,
                RevealSentTextAfterPractice = configuration.UiPreferences?.RevealSentTextAfterPractice ?? true,
            },
        };
    }
}
