using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;

using Promptuarium;

namespace PentaGrammata.Services;

/// <summary>
/// A backup archive: a Promptuarium container in the PPAK1-LE format of Promptuarium's ppak example
/// (<c>examples/PPak</c>, specified in its <c>PPAK_FORMAT.md</c>), so <c>ppak list</c>, <c>ppak verify</c> and <c>ppak unpack</c> work on it:
///
/// <code>
/// root                      metadata: the signature "PRMT:PPAK1-LE"
/// ├── entry                 data: the file name (UTF-8)
/// │   ├── info "type"       "file" (UTF-8)
/// │   ├── info "content"    the content of the file
/// │   ├── info "crc32"      the CRC-32 of the content (uint)
/// │   └── info "modified"   the time of the export (DateTimeOffset)
/// └── ...
/// </code>
///
/// An info node has its flag in the metadata and its value in the data. Only the name and the
/// content are needed to read a file back; everything else is written as a plus.
/// </summary>
internal static class BackupArchive
{
    public const string Signature = "PRMT:PPAK1-LE";

    private const string TypeFlag = "type";
    private const string ContentFlag = "content";
    private const string Crc32Flag = "crc32";
    private const string ModifiedFlag = "modified";
    private const string FileKind = "file";

    // A backup is a settings file and a small database; the limit only stops a corrupt or
    // hostile file from allocating without bound.
    private const long MaxBytes = 1L << 30;
    private const long MaxNodes = 10_000;

    /// <summary>Writes the files, in the order given, as an archive.</summary>
    public static void Write(Stream destination, IEnumerable<(string Name, byte[] Content)> files)
    {
        var modified = Data.FromDateTimeOffset(DateTimeOffset.UtcNow);
        var root = new ElementBuilder().WithSignature(Data.FromAsciiString(Signature)).Build();
        foreach (var (name, content) in files)
        {
            root.Add(new Element(Data.FromUtf8String(name), null,
                Info(TypeFlag, Data.FromUtf8String(FileKind)),
                Info(ContentFlag, content),
                Info(Crc32Flag, Data.FromUInt(Crc32.HashToUInt32(content))),
                Info(ModifiedFlag, modified)));
        }

        var bytes = root.ToBytes();
        destination.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Reads the files of an archive whose names are in <paramref name="names"/> (matched
    /// regardless of case, keyed by the name given there). Reading is tolerant: what matters
    /// is a top-level entry with a known name and a "content" info node. The signature, the
    /// type, the time and every other info node are optional, the entries that are not wanted
    /// are ignored (only flat names are matched, which also rules out path traversal), and a
    /// repeated name keeps its first entry. The only extra a reader does not ignore is a
    /// well-formed CRC-32 that does not match the content: that content is corrupt.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The stream is not a Promptuarium container, or the content of a wanted file does not match its CRC.
    /// </exception>
    public static Dictionary<string, byte[]> Read(Stream source, IReadOnlyCollection<string> names)
    {
        var root = Load(source);
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.Children)
        {
            var name = names.FirstOrDefault(candidate => string.Equals(candidate, entry.Data.AsUtf8String(), StringComparison.OrdinalIgnoreCase));
            if (name is null || files.ContainsKey(name) || Find(entry, ContentFlag) is not { } content)
            {
                continue;
            }

            if (Find(entry, Crc32Flag) is { Data.Length: sizeof(uint) } crc && Crc32.HashToUInt32(content.Data.Span) != crc.Data.AsUInt())
            {
                throw new InvalidDataException($"The content of {name} does not match its CRC.");
            }

            files.Add(name, content.Data.ToArray());
        }

        return files;
    }

    private static Element Load(Stream source)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        if (buffer.Length > MaxBytes)
        {
            throw new InvalidDataException("The archive is too large.");
        }

        var bytes = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        try
        {
            // Whatever follows the end of the tree is not part of the archive.
            return Element.FromBytes(bytes, new LoadOptions { MaxBytes = bytes.Length, MaxNodes = MaxNodes });
        }
        catch (PromptuariumException ex)
        {
            throw new InvalidDataException("The file is not a Promptuarium container.", ex);
        }
    }

    private static Element Info(string flag, ReadOnlyMemory<byte> value) => new(value, Data.FromAsciiString(flag));

    private static Element? Find(Element entry, string flag)
    {
        return entry.Children.FirstOrDefault(info => info.MetaData.AsAsciiString() == flag);
    }
}
