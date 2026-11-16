using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Interfaces;

public interface IConfigurationStore
{
    /// <summary>
    /// Loads the bundled defaults with the per-user files layered on top. A per-user file
    /// written by another major version of the application is not applied: it is moved to the
    /// backups folder (<see cref="LastSetAsideBackupPath"/>) and the defaults are used.
    /// </summary>
    AppConfig Load();

    /// <summary>
    /// Where the last <see cref="Load"/> moved a settings file of another major version, or
    /// null when it moved none.
    /// </summary>
    string? LastSetAsideBackupPath { get; }

    /// <summary>
    /// Persists the given configuration. The caller must pass an isolated snapshot
    /// (not a live, concurrently-mutated instance); the store does not clone it.
    /// </summary>
    Task SaveAsync(AppConfig configuration);
}
