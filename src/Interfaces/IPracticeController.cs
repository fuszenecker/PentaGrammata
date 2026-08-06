using System.Collections.Generic;
using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeController
{
    int PracticeDurationMins { get; set; }

    IReadOnlyList<KeyValuePair<string, string>> CharacterSets { get; }

    string SelectedCharacterSet { get; set; }

    bool IsResultSaved { get; set; }

    string LastGeneratedText { get; }

    /// <summary>
    /// Character WPM used by the most recent session. Reflects the in-memory dynamic WPM
    /// when auto-adjust is enabled, otherwise the configured WPM.
    /// </summary>
    int LastUsedCharacterWpm { get; }

    /// <summary>
    /// Average (Farnsworth) WPM used by the most recent session. Reflects the in-memory
    /// dynamic WPM when auto-adjust is enabled, otherwise the configured WPM.
    /// </summary>
    int LastUsedAverageWpm { get; }

    /// <summary>
    /// Character WPM the next session will use (the current dynamic value when auto-adjust
    /// is enabled, otherwise the configured value).
    /// </summary>
    int CurrentCharacterWpm { get; }

    /// <summary>
    /// Average (Farnsworth) WPM the next session will use (the current dynamic value when
    /// auto-adjust is enabled, otherwise the configured value).
    /// </summary>
    int CurrentAverageWpm { get; }

    Task StartAsync();

    void Stop();

    PracticeResult BuildResult(string receivedText);

    AppConfig CreateSettingsSnapshot();

    bool TryApplySettings(AppConfig settings, out string error);
}