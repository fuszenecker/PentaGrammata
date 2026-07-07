using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Single owner of the live, in-memory application configuration and the only
/// component permitted to persist it. All configuration reads and writes must go
/// through <see cref="Current"/>; nothing else should talk to
/// <see cref="IConfigurationStore"/> directly, otherwise concurrent writers can
/// clobber each other's unpersisted changes.
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
}
