using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Checks the project's GitHub "latest release" endpoint for a newer stable version.
/// GitHub's /releases/latest returns the most recent release that is not a draft and not
/// a prerelease, so it already gives us the latest STABLE release.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    // The git remote origin is the authoritative source (fuszenecker/PentaGrammata).
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/fuszenecker/PentaGrammata/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public GitHubUpdateChecker(HttpClient httpClient, ILogger<GitHubUpdateChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            // GitHub requires a User-Agent and supports an explicit API version header.
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PentaGrammata", current.ToString()));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

            if (!TryParseVersion(tag, out var latest))
            {
                _logger.LogWarning("Could not parse latest release tag '{Tag}' as a version", tag);
                return new UpdateCheckResult
                {
                    Succeeded = false,
                    CurrentVersion = current.ToString(),
                    Error = "Could not read the latest release version.",
                };
            }

            return new UpdateCheckResult
            {
                Succeeded = true,
                UpdateAvailable = Normalize(latest) > Normalize(current),
                CurrentVersion = current.ToString(),
                LatestVersion = latest.ToString(),
                ReleaseUrl = releaseUrl,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Update check failed");
            return new UpdateCheckResult
            {
                Succeeded = false,
                CurrentVersion = current.ToString(),
                Error = "Could not reach the update server. Check your internet connection and try again.",
            };
        }
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
    }

    // Compare on Major.Minor.Build only, treating unset components as 0. This keeps a
    // 3-part tag ("1.8.1") and a 4-part assembly version ("1.8.1.0") comparable, and
    // ignores the build/revision noise that isn't part of a release identity.
    private static Version Normalize(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    // Release tags are commonly prefixed with 'v' (e.g. "v1.8.1"); tolerate that.
    private static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        return Version.TryParse(trimmed, out version!);
    }
}
