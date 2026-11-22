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
    public void Serialize_Configuration_WritesComments()
    {
        var root = Element.FromBytes(PromptuariumSerializer.Serialize(new AppConfiguration(), ConfigurationFormat.Options));

        Assert.IsTrue(IsComment(root.Children[0]), "the root starts with the comment of the file");
        var sampleRate = Child(Child(root, "Audio"), "SampleRate");
        Assert.IsTrue(IsComment(sampleRate.Children[1]), "the comment follows the TYPE node of the member");
        Assert.AreEqual("The sample rate of the audio output, in Hz", sampleRate.Children[1].Data.AsUtf8String());
    }

    [TestMethod]
    public void Serialize_FullConfiguration_StaysVersion1()
    {
        // The serializer signs by content: one value of a version-2 type (an int without
        // [PromptuariumType(Float64)], a DateTime, a Guid ...) would make the file PRMT:CFG2-LE,
        // which PentaGrammata 2.4.1 and older can not read.
        var paths = Substitute.For<IAppPaths>();
        paths.UserConfigPaths.Returns(new List<string>());
        var configuration = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>()).Load();
        configuration.AppMajorVersion = ConfigurationFormat.CurrentMajorVersion;

        foreach (var bytes in new[]
        {
            PromptuariumSerializer.Serialize(configuration, ConfigurationFormat.Options),
            PromptuariumSerializer.Serialize(new AppConfiguration(), ConfigurationFormat.Options),
        })
        {
            Assert.AreEqual(ConfigurationFormat.Signature, Element.FromBytes(bytes).MetaData.AsAsciiString());
        }
    }

    [TestMethod]
    public void BundledSettings_AreVersion1()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName));

        Assert.AreEqual("PRMT:CFG1-LE", Element.FromBytes(bytes).MetaData.AsAsciiString());
    }

    [TestMethod]
    public void BundledSettings_CarryComments()
    {
        var root = Element.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName)));

        Assert.IsTrue(IsComment(root.Children[0]));
        Assert.IsTrue(IsComment(Child(Child(root, "Practice"), "ErrorThreshold").Children[1]));
    }

    private static bool IsComment(Element node) => node.MetaData.Span.SequenceEqual(new byte[] { 0x04 });

    private static Element Child(Element holder, string name) => holder.Children.Single(child => child.MetaData.Span.SequenceEqual(new byte[] { 0x00 }) && child.Data.AsUtf8String() == name);

    [TestMethod]
    public void ConfigurationStore_LoadsBundledDefaults()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.UserConfigPaths.Returns(new List<string>());
        var store = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>());

        var configuration = store.Load();

        Assert.AreEqual(18, configuration.Practice.CharacterWpm);
        Assert.AreEqual(1, configuration.Practice.AutoAdjustWindowSize);
        Assert.IsFalse(configuration.Practice.AutoAdjustSnr);
        Assert.AreEqual(3.0, configuration.Practice.AutoAdjustSnrStepDb);
        Assert.IsFalse(configuration.Practice.AutoAdjustQsb);
        Assert.AreEqual(3.0, configuration.Practice.AutoAdjustQsbStepDb);
        Assert.AreEqual("Default", configuration.Practice.DefaultCharacterSet);
        Assert.AreEqual(39, configuration.CharacterSets.Keys.Count(name => name.StartsWith("Koch-LCWO-", StringComparison.Ordinal)));
        var repetitions = configuration.Topics.Repetitions;
        Assert.AreEqual((2, 2), (repetitions.Names.Minimum, repetitions.Names.Maximum));
        Assert.AreEqual((2, 2), (repetitions.Cities.Minimum, repetitions.Cities.Maximum));
        Assert.AreEqual((2, 2), (repetitions.CallsignsEmea.Minimum, repetitions.CallsignsEmea.Maximum));
        Assert.AreEqual((2, 2), (repetitions.CallsignsGlobal.Minimum, repetitions.CallsignsGlobal.Maximum));
        Assert.AreEqual((1, 1), (repetitions.Abbreviations.Minimum, repetitions.Abbreviations.Maximum));
        Assert.AreEqual((1, 1), (repetitions.CustomText.Minimum, repetitions.CustomText.Maximum));
    }

    [TestMethod]
    public void BundledSettings_CommentTheRepetitions()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName));
        var configuration = PromptuariumSerializer.Deserialize<Dictionary<string, object?>>(bytes, ConfigurationFormat.Options);
        var topics = (Dictionary<string, object?>)configuration["Topics"]!;

        Assert.IsTrue(topics.ContainsKey("Repetitions"), "The bundled file carries the repetitions, not only the code.");
    }

    [TestMethod]
    public void ConfigurationStore_BundledDefaultsHaveValidTopicLists()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.UserConfigPaths.Returns(new List<string>());
        var configuration = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>()).Load();

        Assert.AreEqual(PracticeTopic.Groups, configuration.Practice.Topic);
        Assert.IsFalse(configuration.Practice.ShuffleCustomText);
        Assert.IsGreaterThan(100, configuration.Topics.CallsignPatternsEmea.Count);
        Assert.IsGreaterThan(configuration.Topics.CallsignPatternsEmea.Count, configuration.Topics.CallsignPatternsGlobal.Count);
        Assert.Contains("HA#xyz", configuration.Topics.CallsignPatternsEmea);
        Assert.Contains("BUDAPEST", configuration.Topics.Cities);
        Assert.Contains("TOKYO", configuration.Topics.Cities);
        Assert.Contains("LACI", configuration.Topics.Names);
        Assert.Contains("QRZ", configuration.Topics.Abbreviations);
        foreach (var list in new[] { configuration.Topics.CallsignPatternsEmea, configuration.Topics.CallsignPatternsGlobal, configuration.Topics.Cities, configuration.Topics.Names, configuration.Topics.Abbreviations })
        {
            CollectionAssert.AreEqual(PentaGrammata.Services.TopicListText.Sorted(list), list, "The bundled lists are in alphabetical order, without repeats.");
        }

        // Every topic must be playable with the defaults, and the defaults must pass the
        // same validation the settings dialog applies.
        var validator = new PentaGrammata.Services.PracticeSettingsValidator();
        foreach (var topic in Enum.GetValues<PracticeTopic>().Where(t => t != PracticeTopic.CustomText))
        {
            configuration.Practice.Topic = topic;
            Assert.IsTrue(validator.TryValidate(configuration, out var error), $"{topic}: {error}");
        }
    }

    [TestMethod]
    public void ConfigurationStore_LoadsAPreTopicsUserFile()
    {
        // A settings.prmt written before the topics existed: no Practice.Topic, no
        // Practice.ShuffleCustomText, no Topics section, and a custom text in use.
        var directory = Path.Combine(Path.GetTempPath(), $"pentagrammata-old-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var configPath = Path.Combine(directory, "settings.prmt");
            var old = new Dictionary<string, object?>
            {
                ["Practice"] = new Dictionary<string, object?>
                {
                    ["DefaultDurationMins"] = 3.0,
                    ["CharacterWpm"] = 22.0,
                    ["AverageWpm"] = 16.0,
                    ["DefaultCharacterSet"] = "Letters",
                    ["ErrorThreshold"] = 7.0,
                    ["CustomText"] = "CQ CQ DE HA5XYZ",
                    ["AutoAdjustWpm"] = true,
                    ["AutoAdjustWindowSize"] = 3.0,
                },
                ["CharacterSets"] = new Dictionary<string, object?> { ["Mine"] = "KMRS" },
            };
            File.WriteAllBytes(configPath, PromptuariumSerializer.Serialize(old, ConfigurationFormat.Options));
            var paths = Substitute.For<IAppPaths>();
            paths.UserConfigPaths.Returns(new List<string> { configPath });
            paths.PreferredUserConfigPath.Returns(configPath);

            var configuration = new ConfigurationStore(paths, Substitute.For<ILogger<ConfigurationStore>>()).Load();

            Assert.AreEqual(22, configuration.Practice.CharacterWpm);
            Assert.AreEqual("Letters", configuration.Practice.DefaultCharacterSet);
            Assert.AreEqual("CQ CQ DE HA5XYZ", configuration.Practice.CustomText, "The old custom text is kept.");
            Assert.AreEqual(PracticeTopic.Groups, configuration.Practice.Topic, "Old profiles start on random groups.");
            Assert.IsFalse(configuration.Practice.ShuffleCustomText);
            Assert.Contains("HA#xyz", configuration.Topics.CallsignPatternsEmea, "The bundled topic lists fill in.");
            Assert.Contains("BUDAPEST", configuration.Topics.Cities);
            Assert.AreEqual(2, configuration.Topics.Repetitions.Names.Maximum, "The bundled repetitions fill in.");
            Assert.AreEqual("KMRS", configuration.CharacterSets["Mine"]);
            Assert.IsTrue(new PentaGrammata.Services.PracticeSettingsValidator().TryValidate(configuration, out var error), error);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class Profile : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"pentagrammata-major-{Guid.NewGuid():N}");
        public string ConfigPath => Path.Combine(Directory, "settings.prmt");
        public string BackupDirectory => Path.Combine(Directory, "backups");
        public IAppPaths Paths { get; }

        public Profile()
        {
            System.IO.Directory.CreateDirectory(Directory);
            Paths = Substitute.For<IAppPaths>();
            Paths.UserConfigPaths.Returns(new List<string> { ConfigPath });
            Paths.PreferredUserConfigPath.Returns(ConfigPath);
            Paths.AppDataDirectory.Returns(Directory);
        }

        public ConfigurationStore Store(int major) => new(Paths, Substitute.For<ILogger<ConfigurationStore>>(), major);

        public void Write(Dictionary<string, object?> root) =>
            File.WriteAllBytes(ConfigPath, PromptuariumSerializer.Serialize(root, ConfigurationFormat.Options));

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }

    private static Dictionary<string, object?> UserFile(double? major) 
    {
        var root = new Dictionary<string, object?>
        {
            ["Practice"] = new Dictionary<string, object?> { ["CharacterWpm"] = 33.0 },
        };
        if (major is { } value) root["AppMajorVersion"] = value;
        return root;
    }

    [TestMethod]
    public void CurrentMajorVersion_ComesFromVersionTxt()
    {
        var versionTxt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "version.txt")).Trim();
        Assert.AreEqual(int.Parse(versionTxt.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture), ConfigurationFormat.CurrentMajorVersion);
    }

    [TestMethod]
    [DataRow(null, 2)]
    [DataRow(2.0, 2)]
    [DataRow(3.0, 3)]
    public void ReadMajorVersion_ReadsTheFieldOrAssumesLegacy(double? major, int expected)
    {
        Assert.AreEqual(expected, ConfigurationFormat.ReadMajorVersion(PromptuariumSerializer.Serialize(UserFile(major), ConfigurationFormat.Options)));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(2.0)]
    public void Load_SameMajorVersion_AppliesTheUserFile(double? major)
    {
        using var profile = new Profile();
        profile.Write(UserFile(major));
        var store = profile.Store(2);

        var configuration = store.Load();

        Assert.AreEqual(33, configuration.Practice.CharacterWpm);
        Assert.AreEqual(2, configuration.AppMajorVersion);
        Assert.IsNull(store.LastSetAsideBackupPath);
        Assert.IsTrue(File.Exists(profile.ConfigPath));
        Assert.IsFalse(Directory.Exists(profile.BackupDirectory));
    }

    [TestMethod]
    [DataRow(null, 3, 2)]
    [DataRow(2.0, 3, 2)]
    [DataRow(4.0, 3, 4)]
    public void Load_OtherMajorVersion_SetsTheFileAsideAndUsesTheDefaults(double? fileMajor, int currentMajor, int expectedInName)
    {
        using var profile = new Profile();
        profile.Write(UserFile(fileMajor));
        var original = File.ReadAllBytes(profile.ConfigPath);
        var store = profile.Store(currentMajor);

        var configuration = store.Load();

        Assert.AreEqual(18, configuration.Practice.CharacterWpm, "The bundled default, not the old file.");
        Assert.AreEqual(currentMajor, configuration.AppMajorVersion);
        Assert.IsFalse(File.Exists(profile.ConfigPath), "The old file is moved away, so the next start does not set it aside again.");
        var backup = store.LastSetAsideBackupPath;
        Assert.IsNotNull(backup);
        Assert.AreEqual(Path.GetFullPath(profile.BackupDirectory), Path.GetDirectoryName(Path.GetFullPath(backup)));
        StringAssert.StartsWith(Path.GetFileName(backup), $"pentagrammata-settings-v{expectedInName}-");
        CollectionAssert.AreEqual(original, File.ReadAllBytes(backup), "The backup is the old file, byte for byte.");

        var again = profile.Store(currentMajor);
        again.Load();
        Assert.IsNull(again.LastSetAsideBackupPath);
        Assert.HasCount(1, Directory.GetFiles(profile.BackupDirectory));
    }

    [TestMethod]
    public async Task SaveAsync_StampsTheCurrentMajorVersion_AndTheNextLoadAppliesIt()
    {
        using var profile = new Profile();
        var store = profile.Store(3);
        var configuration = store.Load();
        configuration.Practice.CharacterWpm = 40;
        configuration.AppMajorVersion = 0;

        await store.SaveAsync(configuration);

        Assert.AreEqual(3, ConfigurationFormat.ReadMajorVersion(File.ReadAllBytes(profile.ConfigPath)));
        Assert.AreEqual(40, profile.Store(3).Load().Practice.CharacterWpm);
    }

    [TestMethod]
    public void Load_UserFileWithPartOfTheRepetitions_KeepsTheRestFromTheBundle()
    {
        using var profile = new Profile();
        profile.Write(new Dictionary<string, object?>
        {
            ["AppMajorVersion"] = 2.0,
            ["Topics"] = new Dictionary<string, object?>
            {
                ["Repetitions"] = new Dictionary<string, object?>
                {
                    ["Names"] = new Dictionary<string, object?> { ["Maximum"] = 4.0 },
                },
            },
        });

        var repetitions = profile.Store(2).Load().Topics.Repetitions;

        Assert.AreEqual(2, repetitions.Names.Minimum, "The bundled minimum stays under the saved maximum.");
        Assert.AreEqual(4, repetitions.Names.Maximum);
        Assert.AreEqual(2, repetitions.Cities.Maximum);
        Assert.AreEqual(1, repetitions.Abbreviations.Maximum);
    }

    [TestMethod]
    public async Task SaveAsync_RoundTripsTheRepetitions()
    {
        using var profile = new Profile();
        var store = profile.Store(ConfigurationFormat.CurrentMajorVersion);
        var configuration = store.Load();
        configuration.Topics.Repetitions.CallsignsGlobal = new Repetition(1, 3);
        configuration.Topics.Repetitions.CustomText = new Repetition(2, 2);

        await store.SaveAsync(configuration);
        var reloaded = profile.Store(ConfigurationFormat.CurrentMajorVersion).Load().Topics.Repetitions;

        Assert.AreEqual((1, 3), (reloaded.CallsignsGlobal.Minimum, reloaded.CallsignsGlobal.Maximum));
        Assert.AreEqual((2, 2), (reloaded.CustomText.Minimum, reloaded.CustomText.Maximum));
        Assert.AreEqual((2, 2), (reloaded.Names.Minimum, reloaded.Names.Maximum));
    }

    [TestMethod]
    public void Serialize_Repetitions_WritesComments()
    {
        var bytes = PromptuariumSerializer.Serialize(new AppConfiguration(), ConfigurationFormat.Options);
        var repetitions = Child(Child(Element.FromBytes(bytes), "Topics"), "Repetitions");
        var names = Child(repetitions, "Names");

        Assert.IsTrue(repetitions.Children.Any(c => c.Data.AsUtf8String().StartsWith("How many times", StringComparison.Ordinal)));
        Assert.IsTrue(Child(names, "Minimum").Children.Any(c => c.Data.AsUtf8String().StartsWith("The fewest times", StringComparison.Ordinal)));
        Assert.IsTrue(Child(names, "Maximum").Children.Any(c => c.Data.AsUtf8String().StartsWith("The most times", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Clone_CopiesTheRepetitionsIndependently()
    {
        var original = new AppConfiguration();
        original.Topics.Repetitions.Names = new Repetition(3, 4);

        var copy = original.Clone();
        copy.Topics.Repetitions.Names.Maximum = 9;
        copy.Topics.Repetitions.Cities.Minimum = 1;

        Assert.AreEqual(4, original.Topics.Repetitions.Names.Maximum);
        Assert.AreEqual(2, original.Topics.Repetitions.Cities.Minimum);
        Assert.AreEqual(3, copy.Topics.Repetitions.Names.Minimum);
    }

    [TestMethod]
    public void BundledSettings_CommentTheTopicLists()
    {
        var root = Element.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, ConfigurationPaths.SettingsFileName)));
        var topics = Child(root, "Topics");

        Assert.IsTrue(topics.Children.Any(c => c.MetaData.Span.SequenceEqual(new byte[] { 0x00 }) && c.Data.AsUtf8String() == "CallsignPatternsEmea"));
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
            configuration.Practice.Topic = PracticeTopic.CallsignsEmea;
            configuration.Practice.ShuffleCustomText = true;
            configuration.Topics.Cities = ["WIEN"];
            await store.SaveAsync(configuration);

            var reloaded = store.Load();
            Assert.AreEqual(27, reloaded.Practice.CharacterWpm);
            Assert.AreEqual(650.5, reloaded.Audio.Frequency);
            Assert.AreEqual(NoiseType.Gaussian, reloaded.Audio.Noise.Type);
            Assert.AreEqual(PracticeTopic.CallsignsEmea, reloaded.Practice.Topic);
            Assert.IsTrue(reloaded.Practice.ShuffleCustomText);
            // A saved list replaces the bundled one as a whole; the others keep their defaults.
            CollectionAssert.AreEqual(new[] { "WIEN" }, reloaded.Topics.Cities);
            Assert.Contains("HA#xyz", reloaded.Topics.CallsignPatternsEmea);
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
