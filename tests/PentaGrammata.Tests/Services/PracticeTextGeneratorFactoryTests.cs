using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeTextGeneratorFactoryTests
{
    private static AppConfiguration Config()
    {
        var config = new AppConfiguration
        {
            Practice = new Practice { DefaultCharacterSet = "Digits" },
            CharacterSets = new CharacterSets { ["Digits"] = "0123456789", ["Letters"] = "ABC" },
            Topics = new Topics
            {
                CallsignPatternsEmea = ["HA#xyz"],
                CallsignPatternsGlobal = ["W#xy", "JA1xyz"],
                Cities = ["Budapest", "Cape Town"],
                Names = ["Laci"],
                Abbreviations = ["QRZ", "73"],
            },
        };
        return config;
    }

    private static string Generate(PracticeTopic topic, AppConfiguration config, int groups = 10) =>
        new PracticeTextGeneratorFactory(new MorseGenerator()).Create(topic, config).Generate(groups);

    [TestMethod]
    public void Groups_UseTheSelectedCharacterSet()
    {
        var text = Generate(PracticeTopic.Groups, Config(), 4);

        var groups = text.Split(' ');
        Assert.HasCount(4, groups);
        Assert.IsTrue(groups.All(g => g.Length == 5 && g.All(char.IsDigit)), text);
    }

    [TestMethod]
    public void Groups_FallBackToTheFirstSetWhenTheSelectedOneIsMissing()
    {
        var config = Config();
        config.Practice.DefaultCharacterSet = "Missing";
        var generator = Substitute.For<IMorseGenerator>();

        new PracticeTextGeneratorFactory(generator).Create(PracticeTopic.Groups, config).Generate(3);

        generator.Received(1).GenerateGroupsOf5("0123456789", 3);
    }

    [TestMethod]
    public void EmeaCallsigns_ExpandThePatterns()
    {
        var text = Generate(PracticeTopic.CallsignsEmea, Config());

        foreach (var call in text.Split(' '))
        {
            Assert.AreEqual(6, call.Length, call);
            Assert.StartsWith("HA", call);
            Assert.IsTrue(char.IsDigit(call[2]), call);
            Assert.IsTrue(call[3..].All(c => c is >= 'A' and <= 'Z'), call);
        }
    }

    [TestMethod]
    public void GlobalCallsigns_KeepFixedDigitsAndLetters()
    {
        var calls = Generate(PracticeTopic.CallsignsGlobal, Config(), 40).Split(' ');

        Assert.IsTrue(calls.All(c => (c.Length == 4 && c[0] == 'W' && char.IsDigit(c[1])) || (c.Length == 6 && c.StartsWith("JA1", StringComparison.Ordinal))), string.Join(' ', calls));
    }

    [TestMethod]
    [DataRow("HA#xyz", "HA", 6)]
    [DataRow("DL/HA#x", "DL/HA", 7)]
    [DataRow("9A#xy", "9A", 5)]
    public void ExpandCallsign_ReplacesOnlyThePlaceholders(string pattern, string prefix, int length)
    {
        for (int i = 0; i < 50; i++)
        {
            var call = PracticeTextGeneratorFactory.ExpandCallsign(pattern);
            Assert.HasCount(length, call);
            Assert.StartsWith(prefix, call);
            Assert.AreEqual(call.ToUpperInvariant(), call);
            Assert.DoesNotContain("#", call);
        }
    }

    [TestMethod]
    public void WordTopics_PickFromTheirListInUppercase()
    {
        var config = Config();
        Assert.IsTrue(Generate(PracticeTopic.Names, config).Split(' ').All(w => w == "LACI"));
        Assert.IsTrue(Generate(PracticeTopic.Abbreviations, config).Split(' ').All(w => w is "QRZ" or "73"));
        var cities = PracticeItems.SplitItems(Generate(PracticeTopic.Cities, config, 30));
        Assert.IsTrue(cities.All(c => c is "BUDAPEST" or "CAPE_TOWN"), string.Join('|', cities));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(30)]
    public void WordTopics_SendAboutAsManyCharactersAsTheGroupsWould(int groups)
    {
        var text = Generate(PracticeTopic.Names, Config(), groups);

        int letters = text.Replace(" ", string.Empty).Length;
        Assert.IsGreaterThanOrEqualTo(groups * 5, letters);
        Assert.IsLessThan(groups * 5 + 4, letters);
    }

    [TestMethod]
    public void WordTopics_GiveNothingForAnEmptyList()
    {
        var config = Config();
        config.Topics.Names = ["  ", ""];

        Assert.AreEqual(string.Empty, Generate(PracticeTopic.Names, config));
    }

    [TestMethod]
    public void CustomText_IsSentWholeAndNormalized()
    {
        var config = Config();
        config.Practice.CustomText = "  CQ CQ\r\nDE  HA5XYZ ";

        Assert.AreEqual("CQ CQ DE HA5XYZ", Generate(PracticeTopic.CustomText, config, 1));
        Assert.AreEqual("CQ CQ DE HA5XYZ", Generate(PracticeTopic.CustomText, config, 100));
    }

    [TestMethod]
    public void CustomText_Shuffled_KeepsEveryWord()
    {
        var config = Config();
        config.Practice.CustomText = "ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT NINE TEN";
        config.Practice.ShuffleCustomText = true;

        var orders = Enumerable.Range(0, 20).Select(_ => Generate(PracticeTopic.CustomText, config)).ToList();

        foreach (var order in orders)
        {
            CollectionAssert.AreEquivalent(config.Practice.CustomText.Split(' '), order.Split(' '));
        }

        Assert.IsTrue(orders.Any(o => o != config.Practice.CustomText), "Twenty shuffles all kept the written order.");
    }

    [TestMethod]
    public void Mixed_DrawsFromEveryTopic()
    {
        var config = Config();
        config.Practice.CustomText = "MYWORD";
        config.Topics.Cities = ["WIEN"];

        var items = Generate(PracticeTopic.Mixed, config, 400).Split(' ');

        Assert.IsTrue(items.Any(i => i.Length == 5 && i.All(char.IsDigit)), "no group");
        Assert.IsTrue(items.Any(i => i.StartsWith("HA", StringComparison.Ordinal)), "no EMEA call");
        Assert.IsTrue(items.Any(i => i.StartsWith('W') && i != "WIEN"), "no global call");
        Assert.Contains("WIEN", items);
        Assert.Contains("LACI", items);
        Assert.Contains("QRZ", items);
        Assert.Contains("MYWORD", items);
    }

    [TestMethod]
    public void Mixed_SkipsEmptyListsAndStillSendsGroups()
    {
        var config = Config();
        config.Topics = new Topics();

        var items = Generate(PracticeTopic.Mixed, config, 10).Split(' ');

        Assert.IsTrue(items.All(i => i.Length == 5 && i.All(char.IsDigit)), string.Join(' ', items));
    }

    [TestMethod]
    public void WordTopics_JoinTheWordsOfAMultiWordEntryWithAUnderscore()
    {
        var config = Config();
        config.Topics.Names = ["Little  John"];
        config.Topics.Abbreviations = ["QSL VIA BURO"];

        var names = Generate(PracticeTopic.Names, config, 5);
        var abbreviations = Generate(PracticeTopic.Abbreviations, config, 5);

        Assert.IsTrue(PracticeItems.SplitItems(names).All(n => n == "LITTLE_JOHN"), names);
        Assert.IsTrue(PracticeItems.SplitItems(abbreviations).All(a => a == "QSL_VIA_BURO"), abbreviations);
        // Between items there is an ordinary space.
        Assert.Contains("JOHN LITTLE", names);
    }

    [TestMethod]
    public void WordTopics_CountTheLettersOfMultiWordItemsAgainstTheBudget()
    {
        var config = Config();
        config.Topics.Cities = ["NEW YORK"];

        var items = PracticeItems.SplitItems(Generate(PracticeTopic.Cities, config, 7));

        // 35 characters at 7 letters an item: 5 items.
        Assert.HasCount(5, items);
    }

    [TestMethod]
    public void Mixed_KeepsMultiWordItemsWhole()
    {
        var config = Config();
        config.Topics = new Topics { Cities = ["DEN HAAG"] };

        var items = PracticeItems.SplitItems(Generate(PracticeTopic.Mixed, config, 200));

        Assert.Contains("DEN_HAAG", items);
        Assert.DoesNotContain("DEN", items);
    }

    [TestMethod]
    public void CustomText_StaysWordByWord()
    {
        var config = Config();
        config.Practice.CustomText = "QSL VIA BURO";

        Assert.AreEqual("QSL VIA BURO", Generate(PracticeTopic.CustomText, config));
    }
}
