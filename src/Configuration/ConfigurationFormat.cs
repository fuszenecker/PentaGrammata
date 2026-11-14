using System.Collections.Generic;
using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// The settings and window-size files are <c>PRMT:CFG1-LE</c> containers, written and read by
/// <see cref="PromptuariumSerializer"/> (the layout is specified in Promptuarium's <c>SERIALIZER_FORMAT.md</c>).
/// </summary>
public static class ConfigurationFormat
{
    /// <summary>
    /// The major version assumed for a per-user settings file without
    /// <see cref="AppConfiguration.AppMajorVersion"/>: every such file was written by 2.x.
    /// </summary>
    public const int LegacyMajorVersion = 2;

    /// <summary>The major version of this build (from <c>version.txt</c>, stamped into the assembly).</summary>
    public static int CurrentMajorVersion { get; } = typeof(ConfigurationFormat).Assembly.GetName().Version?.Major ?? LegacyMajorVersion;

    /// <summary>The options of every file the application reads and writes: the configuration signature.</summary>
    public static PromptuariumSerializerOptions Options { get; } = new()
    {
        Signature = PromptuariumSerializerOptions.ConfigurationSignature,
    };

    /// <summary>
    /// The major version a settings file was written by: its <c>AppMajorVersion</c>, or
    /// <see cref="LegacyMajorVersion"/> for files from before the field.
    /// </summary>
    public static int ReadMajorVersion(byte[] bytes)
    {
        var root = PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(bytes, Options);
        return root.TryGetValue(nameof(AppConfiguration.AppMajorVersion), out var value) && value is double major
            ? (int)major
            : LegacyMajorVersion;
    }
}
