using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

/// <summary>
/// Persists window sizes to a dedicated JSON file (<c>window-sizes.json</c>) in the
/// per-user data directory, on every platform. Every operation is best-effort: a storage
/// failure is logged and treated as "no saved size" rather than thrown, so it can never
/// block opening or closing a window.
/// </summary>
public sealed class WindowSizeStore : IWindowSizeStore
{
    private const string FileName = "window-sizes.json";

    private readonly IAppPaths _appPaths;
    private readonly ILogger<WindowSizeStore> _logger;
    private readonly object _gate = new();

    private Dictionary<string, StoredSize>? _sizes;

    public WindowSizeStore(IAppPaths appPaths, ILogger<WindowSizeStore> logger)
    {
        _appPaths = appPaths;
        _logger = logger;
    }

    public (double Width, double Height)? TryGetSize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        try
        {
            lock (_gate)
            {
                var sizes = LoadLocked();
                if (sizes.TryGetValue(key, out var size) && IsUsable(size.Width) && IsUsable(size.Height))
                {
                    return (size.Width, size.Height);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read saved size for window {Key}", key);
        }

        return null;
    }

    public void SaveSize(string key, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(key) || !IsUsable(width) || !IsUsable(height))
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                var sizes = LoadLocked();
                sizes[key] = new StoredSize { Width = width, Height = height };

                var directory = _appPaths.AppDataDirectory;
                Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(sizes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(directory, FileName), json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save size for window {Key}", key);
        }
    }

    // Loads the file once and caches it; callers hold _gate.
    private Dictionary<string, StoredSize> LoadLocked()
    {
        if (_sizes is not null)
        {
            return _sizes;
        }

        var path = Path.Combine(_appPaths.AppDataDirectory, FileName);
        if (File.Exists(path))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, StoredSize>>(File.ReadAllText(path));
            _sizes = parsed is null
                ? new Dictionary<string, StoredSize>(StringComparer.Ordinal)
                : new Dictionary<string, StoredSize>(parsed, StringComparer.Ordinal);
        }
        else
        {
            _sizes = new Dictionary<string, StoredSize>(StringComparer.Ordinal);
        }

        return _sizes;
    }

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;

    private sealed class StoredSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
