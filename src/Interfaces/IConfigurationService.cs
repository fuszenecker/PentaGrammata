using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Single owner of the live, in-memory application configuration and the only
/// component permitted to mutate or persist it. Reads go through <see cref="Current"/>;
/// every write goes through one of the typed mutation methods below, which update
/// <see cref="Current"/> and schedule a serialized save. Nothing else should talk to
/// <see cref="IConfigurationStore"/> directly or reach into <see cref="Current"/> to
/// mutate sub-objects, otherwise concurrent writers can clobber each other's
/// unpersisted changes.
/// </summary>
public interface IConfigurationService
{
    AppConfig Current { get; }

    /// <summary>
    /// Persists the current configuration. Saves are serialized so concurrent or
    /// rapid calls run in order and never interleave writes to the backing file.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Waits for all configuration saves requested so far to complete.
    /// </summary>
    Task FlushAsync();

    /// <summary>
    /// Fire-and-forget variant of <see cref="SaveAsync"/> for callers (e.g. property
    /// setters) that cannot await. The save is still ordered behind any in-flight
    /// save; failures are logged.
    /// </summary>
    void RequestSave();

    /// <summary>
    /// Returns whether an informational dialog identified by <paramref name="dialogKey"/>
    /// has been suppressed by the user.
    /// </summary>
    bool IsDialogSuppressed(string dialogKey);

    /// <summary>
    /// Marks the dialog identified by <paramref name="dialogKey"/> as suppressed and
    /// persists the change through the single configuration owner.
    /// </summary>
    Task SuppressDialogAsync(string dialogKey);

    /// <summary>
    /// Sets the default practice duration (minutes) and persists if it changed.
    /// </summary>
    void SetPracticeDuration(int minutes);

    /// <summary>
    /// Sets the default character-set selection and persists if it changed.
    /// </summary>
    void SetSelectedCharacterSet(string name);

    /// <summary>
    /// Applies a validated wholesale settings snapshot (practice, audio, noise, and
    /// character sets) to the live configuration and persists. Used after the settings
    /// dialog returns a new <see cref="AppConfig"/>.
    /// </summary>
    void ApplyPracticeSettings(AppConfig settings);

    /// <summary>
    /// Replaces the UI preferences and awaits persistence.
    /// </summary>
    Task ApplyUiPreferencesAsync(UiPreferences preferences);

    /// <summary>
    /// Sets the confusions retention half-life (days) and persists if it changed.
    /// </summary>
    void SetConfusionsHalfLife(double days);

    /// <summary>
    /// Inserts or replaces a named character set and selects it, awaiting persistence.
    /// Used by "Practice confusions" to publish the generated set.
    /// </summary>
    Task UpsertCharacterSetAndSelectAsync(string name, string characters);
}
