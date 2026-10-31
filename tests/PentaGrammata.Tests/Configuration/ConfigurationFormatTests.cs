using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Stores;
using Promptuarium;
using Promptuarium.Serializer;

namespace PentaGrammata.Tests.Configuration;

[TestClass]
public sealed class ConfigurationFormatTests
{
    // Promptuarium SERIALIZER_FORMAT.md, section 7: the files written before the library keep their bytes.
    private const string TestVector = """
        6c 50 52 4d 54 3a 43 46 47 31 2d 4c 45 44 6b 65
        79 31 21 00 41 80 21 01 c6 76 61 6c 75 65 31 21
        03 84 6b 65 79 32 21 00 41 63 21 01 c8 00 00 00
        00 00 00 f8 3f 21 03 84 6b 65 79 33 21 00 41 01
        21 01 c1 01 21 03 84 6b 65 79 34 21 00 41 21 21
        01 e1 02 41 80 21 01 c7 76 61 6c 75 65 34 31 21
        03 a1 02 41 80 21 01 c7 76 61 6c 75 65 34 32 21
        03 80 84 6b 65 79 35 21 00 41 20 21 01 c5 6b 65
        79 35 31 21 00 41 80 21 01 c7 76 61 6c 75 65 35
        31 21 03 80 84 6b 65 79 36 21 00 41 00 21 01 00
        """;

    private static Dictionary<string, object?> Example() => new()
    {
        ["key1"] = "value1",
        ["key2"] = 1.5,
        ["key3"] = true,
        ["key4"] = new List<object?> { "value41", "value42" },
        ["key5"] = new Dictionary<string, object?> { ["key51"] = "value51" },
        ["key6"] = null,
    };

    private static byte[] Hex(string hex) =>
        hex.Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(b => Convert.ToByte(b, 16)).ToArray();

    private static Element Root() =>
        new ElementBuilder().WithSignature(Data.FromAsciiString(PromptuariumSerializerOptions.ConfigurationSignature)).Build();

    private static Element Member(string name, byte type, ReadOnlyMemory<byte>? value = null)
    {
        var member = new Element(Data.FromUtf8String(name), Data.FromByte(0x00), new Element(Data.FromByte(type), Data.FromByte(0x01)));
        if (value is { } data)
        {
            member.Add(new Element(data, Data.FromByte(0x03)));
        }

        return member;
    }

    [TestMethod]
    public void Serialize_SpecExample_MatchesTestVector()
    {
        CollectionAssert.AreEqual(Hex(TestVector), PromptuariumSerializer.Serialize(Example(), ConfigurationFormat.Options));
    }

    [TestMethod]
    public void Deserialize_TestVector_GivesSpecExample()
    {
        var decoded = PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(Hex(TestVector), ConfigurationFormat.Options);

        Assert.AreEqual("value1", decoded["key1"]);
        Assert.AreEqual(1.5, decoded["key2"]);
        Assert.AreEqual(true, decoded["key3"]);
        CollectionAssert.AreEqual(new List<object?> { "value41", "value42" }, (List<object?>)decoded["key4"]!);
        Assert.AreEqual("value51", ((Dictionary<string, object?>)decoded["key5"]!)["key51"]);
        Assert.IsNull(decoded["key6"]);
    }

    private sealed class WithBlob
    {
        public string Name { get; set; } = "";
        public byte[] Payload { get; set; } = [];
        public byte[]? Missing { get; set; }
    }

    [TestMethod]
    public void Blob_IsWrittenFromAndReadIntoByteArray()
    {
        var bytes = PromptuariumSerializer.Serialize(new Dictionary<string, object> { ["key"] = new byte[] { 0xde, 0xad, 0xbe, 0xef } }, ConfigurationFormat.Options);

        CollectionAssert.AreEqual(Hex("6c 50 52 4d 54 3a 43 46 47 31 2d 4c 45 43 6b 65 79 21 00 41 f0 21 01 c4 de ad be ef 21 03 00"), bytes);
        CollectionAssert.AreEqual(new byte[] { 0xde, 0xad, 0xbe, 0xef }, (byte[])PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(bytes, ConfigurationFormat.Options)["key"]!);
    }

    [TestMethod]
    public void Blob_RoundTripsThroughATypedProperty()
    {
        var bytes = PromptuariumSerializer.Serialize(new WithBlob { Name = "x", Payload = [1, 2, 3] }, ConfigurationFormat.Options);

        var copy = PromptuariumSerializer.Deserialize<WithBlob>(bytes, ConfigurationFormat.Options);

        Assert.AreEqual("x", copy.Name);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, copy.Payload);
        Assert.IsNull(copy.Missing);
    }

    [TestMethod]
    public void Populate_LayersMembersAndKeepsValuesUnderNull()
    {
        var target = new AppConfiguration { Practice = new Practice { CharacterWpm = 18, AverageWpm = 12, CustomText = "keep" } };
        target.CharacterSets["Default"] = "ABC";

        var overlay = Root().Add(
            new Element(Data.FromUtf8String("PRACTICE"), Data.FromByte(0x00),
                new Element(Data.FromByte(0x20), Data.FromByte(0x01)),
                Member("characterWpm", 0x63, Data.FromDouble(25)),
                Member("CustomText", 0x00)),
            new Element(Data.FromUtf8String("CharacterSets"), Data.FromByte(0x00),
                new Element(Data.FromByte(0x20), Data.FromByte(0x01)),
                Member("Mine", 0x80, Data.FromUtf8String("XYZ"))));

        PromptuariumSerializer.Populate(overlay.ToBytes(), target, ConfigurationFormat.Options);

        Assert.AreEqual(25, target.Practice.CharacterWpm);
        Assert.AreEqual(12, target.Practice.AverageWpm);
        Assert.AreEqual("keep", target.Practice.CustomText);
        Assert.AreEqual("ABC", target.CharacterSets["Default"]);
        Assert.AreEqual("XYZ", target.CharacterSets["Mine"]);
    }

    [TestMethod]
    public void Enum_IsWrittenByNameAndReadFromNameOrNumber()
    {
        var bytes = PromptuariumSerializer.Serialize(new NoiseSettings { Type = NoiseType.Pink }, ConfigurationFormat.Options);
        Assert.AreEqual(NoiseType.Pink, PromptuariumSerializer.Deserialize<NoiseSettings>(bytes, ConfigurationFormat.Options).Type);
        Assert.AreEqual("Pink", PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(bytes, ConfigurationFormat.Options)["Type"]);

        var numeric = Root().Add(Member("Type", 0x63, Data.FromDouble((double)NoiseType.Pink)));
        Assert.AreEqual(NoiseType.Pink, PromptuariumSerializer.Deserialize<NoiseSettings>(numeric, ConfigurationFormat.Options).Type);
    }

    [TestMethod]
    public void Deserialize_WrongSignature_Throws()
    {
        var bytes = new ElementBuilder().WithSignature(Data.FromAsciiString("PRMT:CFG1")).Build().ToBytes();

        Assert.ThrowsExactly<InvalidDataException>(() => PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(bytes, ConfigurationFormat.Options));
    }

    [TestMethod]
    [DataRow((byte)0x42)] // reserved INT32
    [DataRow((byte)0x7f)] // unknown
    public void Deserialize_UnsupportedType_Throws(byte type)
    {
        var root = Root().Add(Member("key", type, Data.FromInt(1)));

        Assert.ThrowsExactly<InvalidDataException>(() => PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(root, ConfigurationFormat.Options));
    }

    [TestMethod]
    public void Deserialize_FractionIntoInteger_Throws()
    {
        var root = Root().Add(
            new Element(Data.FromUtf8String("Practice"), Data.FromByte(0x00),
                new Element(Data.FromByte(0x20), Data.FromByte(0x01)),
                Member("CharacterWpm", 0x63, Data.FromDouble(18.5))));

        Assert.ThrowsExactly<InvalidDataException>(() => PromptuariumSerializer.Deserialize<AppConfiguration>(root, ConfigurationFormat.Options));
    }

    [TestMethod]
    public void Deserialize_DuplicateNames_LastOneWins()
    {
        var root = Root().Add(Member("key", 0x80, Data.FromUtf8String("first")), Member("key", 0x80, Data.FromUtf8String("second")));

        Assert.AreEqual("second", PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(root, ConfigurationFormat.Options)["key"]);
    }

    [TestMethod]
    public void ConfigurationStore_LoadsBundledDefaults()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.UserConfigPaths.Returns(new List<string>());
        var store = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>());

        var configuration = store.Load();

        Assert.AreEqual(18, configuration.Practice.CharacterWpm);
        Assert.AreEqual("Default", configuration.Practice.DefaultCharacterSet);
        Assert.AreEqual(39, configuration.CharacterSets.Keys.Count(name => name.StartsWith("Koch-LCWO-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConfigurationStore_SaveThenLoad_RoundTripsUserSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pentagrammata-serializer-{Guid.NewGuid():N}");
        try
        {
            var configPath = Path.Combine(directory, "settings.prmt");
            var paths = Substitute.For<IAppPaths>();
            paths.UserConfigPaths.Returns(new List<string> { configPath });
            paths.PreferredUserConfigPath.Returns(configPath);
            var store = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>());

            var configuration = store.Load();
            configuration.Practice.CharacterWpm = 27;
            configuration.Audio.Frequency = 650.5;
            configuration.Audio.Noise.Type = NoiseType.Gaussian;
            await store.SaveAsync(configuration);

            var reloaded = store.Load();
            Assert.AreEqual(27, reloaded.Practice.CharacterWpm);
            Assert.AreEqual(650.5, reloaded.Audio.Frequency);
            Assert.AreEqual(NoiseType.Gaussian, reloaded.Audio.Noise.Type);
            Assert.IsTrue(File.ReadAllBytes(configPath).AsSpan(1, 12).SequenceEqual("PRMT:CFG1-LE"u8));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
