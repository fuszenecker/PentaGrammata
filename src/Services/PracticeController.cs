using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

public class PracticeController : IPracticeController
{
    private const double LengthCorrector = 0.695;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultEvaluator _resultEvaluator;
    private readonly IPracticeConfigurationStore _configurationStore;
    private readonly ILogger<PracticeController> _logger;
    private readonly AppConfig _configuration;
    private CancellationTokenSource? _cancellationTokenSource;

    public string LastGeneratedText { get; private set; } = string.Empty;
    public string LastReceivedText { get; private set; } = string.Empty;

    public int PracticeDurationMins
    {
        get => _configuration.Practice.DefaultDurationMins;
        set
        {
            if (_configuration.Practice.DefaultDurationMins == value)
                return;

            _configuration.Practice.DefaultDurationMins = value;
            QueueSaveConfiguration();
        }
    }

    public IReadOnlyList<KeyValuePair<string, string>> CharacterSets => _configuration.CharacterSets
        .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value))
        .ToList();

    public string SelectedCharacterSet
    {
        get => _configuration.Practice.DefaultCharacterSet;
        set
        {
            if (_configuration.Practice.DefaultCharacterSet == value)
                return;

            _configuration.Practice.DefaultCharacterSet = value;
            QueueSaveConfiguration();
        }
    }

    public bool IsPracticing { get; private set; }

    public PracticeController(
        IMorsePlayer morsePlayer,
        IMorseGenerator morseGenerator,
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultEvaluator resultEvaluator,
        IPracticeConfigurationStore configurationStore,
        ILogger<PracticeController> logger)
    {
        _morsePlayer = morsePlayer;
        _morseGenerator = morseGenerator;
        _settingsValidator = settingsValidator;
        _resultEvaluator = resultEvaluator;
        _configurationStore = configurationStore;
        _logger = logger;
        _configuration = _configurationStore.Load();

        var characterSets = new List<KeyValuePair<string, string>>();

        foreach (var characterSet in _configuration.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(characterSet.Value))
                characterSets.Add(new KeyValuePair<string, string>(characterSet.Key, characterSet.Value));
        }

        if (characterSets.Count == 0)
        {
            characterSets.Add(new KeyValuePair<string, string>("Default", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>"));
            _configuration.CharacterSets["Default"] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";
        }

        _configuration.Practice.DefaultCharacterSet = _configuration.Practice.DefaultCharacterSet ?? characterSets[0].Key;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting practice session");
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;

        try
        {
            string characterSetCharacters = _configuration.CharacterSets.TryGetValue(SelectedCharacterSet, out var selectedCharacters)
                && !string.IsNullOrWhiteSpace(selectedCharacters)
                    ? selectedCharacters
                    : CharacterSets.FirstOrDefault().Value;

            if (string.IsNullOrWhiteSpace(characterSetCharacters))
                characterSetCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

            int numberOfGroups = (int)Math.Round(PracticeDurationMins * _configuration.Practice.AverageWpm * LengthCorrector);
            _logger.LogDebug("Generating {GroupCount} morse groups", numberOfGroups);
            
            string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);
            LastGeneratedText = morseCode;

            try
            {
                string morseCodeToPlay = "vvv = " + morseCode + " <ar>";
                await _morsePlayer.PlayMorseCodeAsync(
                    morseCodeToPlay,
                    charWpm: _configuration.Practice.CharacterWpm,
                    averageWpm: _configuration.Practice.AverageWpm,
                    sampleRate: _configuration.Audio.SampleRate,
                    frequency: _configuration.Audio.Frequency,
                    volume: _configuration.Audio.Volume,
                    beepRampMs: _configuration.Audio.BeepRampMs,
                    _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Practice session cancelled");
                throw;
            }
        }
        finally
        {
            IsPracticing = false;
            _logger.LogInformation("Practice session finished");
        }
    }

    public void Stop()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public PracticeResult BuildResult(string receivedText)
    {
        LastReceivedText = receivedText ?? string.Empty;
        return _resultEvaluator.Evaluate(LastGeneratedText, LastReceivedText, _configuration.Practice.ErrorThreshold);
    }

    public AppConfig CreateSettingsSnapshot()
    {
        var characterSets = new CharacterSets();
        foreach (var kv in _configuration.CharacterSets)
            characterSets[kv.Key] = kv.Value;

        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = _configuration.Practice.DefaultDurationMins,
                CharacterWpm = _configuration.Practice.CharacterWpm,
                AverageWpm = _configuration.Practice.AverageWpm,
                DefaultCharacterSet = _configuration.Practice.DefaultCharacterSet,
                ErrorThreshold = _configuration.Practice.ErrorThreshold,
            },
            Audio = new Audio
            {
                SampleRate = _configuration.Audio.SampleRate,
                Frequency = _configuration.Audio.Frequency,
                Volume = _configuration.Audio.Volume,
                BeepRampMs = _configuration.Audio.BeepRampMs,
            },
            CharacterSets = characterSets,
        };
    }

    public bool TryApplySettings(AppConfig settings, out string error)
    {
        if (!_settingsValidator.TryValidate(settings, out error))
        {
            return false;
        }

        _configuration.Practice.DefaultDurationMins = settings.Practice.DefaultDurationMins;
        _configuration.Practice.CharacterWpm = settings.Practice.CharacterWpm;
        _configuration.Practice.AverageWpm = settings.Practice.AverageWpm;
        _configuration.Audio.SampleRate = settings.Audio.SampleRate;
        _configuration.Audio.Frequency = settings.Audio.Frequency;
        _configuration.Audio.Volume = settings.Audio.Volume;
        _configuration.Audio.BeepRampMs = settings.Audio.BeepRampMs;
        _configuration.Practice.DefaultCharacterSet = settings.Practice.DefaultCharacterSet;
        _configuration.Practice.ErrorThreshold = settings.Practice.ErrorThreshold;

        _configuration.CharacterSets.Clear();
        foreach (var item in settings.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                _configuration.CharacterSets[item.Key] = item.Value;
            }
        }

        QueueSaveConfiguration();
        error = string.Empty;
        return true;
    }

    private void QueueSaveConfiguration()
    {
        _ = PersistConfigurationAsync();
    }

    private async Task PersistConfigurationAsync()
    {
        try
        {
            await _configurationStore.SaveAsync(CreateSettingsSnapshot()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist user configuration");
        }
    }
}