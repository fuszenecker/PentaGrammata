using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// The settings and window-size files are <c>PRMT:CFG1-LE</c> containers, written and read by
/// <see cref="PromptuariumSerializer"/> (the layout is specified in Promptuarium's <c>SERIALIZER_FORMAT.md</c>).
/// </summary>
public static class ConfigurationFormat
{
    /// <summary>The options of every file the application reads and writes: the configuration signature.</summary>
    public static PromptuariumSerializerOptions Options { get; } = new()
    {
        Signature = PromptuariumSerializerOptions.ConfigurationSignature,
    };
}
