namespace PentaGrammata.Models;

/// <summary>
/// Outcome of checking GitHub for a newer stable release.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>Whether the check completed successfully (false = network/parse failure).</summary>
    public bool Succeeded { get; init; }

    /// <summary>True when a stable release newer than the running version was found.</summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>The running application version (e.g. "1.8.1.0").</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>The latest stable release version from GitHub, when the check succeeded.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Browser URL of the latest release, when available.</summary>
    public string? ReleaseUrl { get; init; }

    /// <summary>Human-readable error, when <see cref="Succeeded"/> is false.</summary>
    public string? Error { get; init; }
}
