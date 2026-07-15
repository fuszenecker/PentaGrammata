using System.Collections.Generic;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Provides the per-user filesystem locations PentaGrammata reads from and writes to.
/// Injected so stores don't derive environment-dependent paths themselves, which keeps
/// them unit-testable against a temporary directory.
/// </summary>
public interface IAppPaths
{
    /// <summary>
    /// Per-user config files to layer on top of the bundled <c>appsettings.json</c>,
    /// in ascending precedence (later entries win). May be empty on unsupported platforms.
    /// </summary>
    IReadOnlyList<string> UserConfigPaths { get; }

    /// <summary>
    /// The single config file new user changes are written to, or <c>null</c> when the
    /// platform has no per-user location.
    /// </summary>
    string? PreferredUserConfigPath { get; }

    /// <summary>
    /// The per-user directory where PentaGrammata stores its data (configuration,
    /// statistics database, etc.).
    /// </summary>
    string AppDataDirectory { get; }
}
