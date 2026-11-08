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

    Task<PracticeResult> BuildResultAsync(string receivedText);

    /// <summary>
    /// Feeds the saved result of the current session to the auto-adjusting WPM (when it is
    /// on). Only a saved result moves the dynamic WPM, at most once per session.
    /// </summary>
    void RecordSavedResult(PracticeResult result);

    AppConfig CreateSettingsSnapshot();

    bool TryApplySettings(AppConfig settings, out string error);
}
