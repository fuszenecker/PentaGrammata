using System.Collections.Generic;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

/// <summary>
/// Default <see cref="IAppPaths"/> backed by <see cref="ConfigurationPaths"/>, which
/// remains the single source of truth for how the locations are derived. This type
/// exists so those locations can be resolved through DI (and substituted in tests).
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public IReadOnlyList<string> UserConfigPaths { get; } = ConfigurationPaths.GetPerUserConfigPaths();

    public string? PreferredUserConfigPath { get; } = ConfigurationPaths.GetPreferredPerUserConfigPath();

    public string AppDataDirectory { get; } = ConfigurationPaths.GetAppDataDirectory();
}
