using System;
using System.Collections.Generic;
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
                // Every item once, unless a test sets its own repetition.
                Repetitions = new TopicRepetitions
                {
                    CallsignsEmea = new(1, 1),
                    CallsignsGlobal = new(1, 1),
                    Cities = new(1, 1),
                    Names = new(1, 1),
                    Abbreviations = new(1, 1),
                    CustomText = new(1, 1),
                },
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
    public void CustomText_IsSentWholeLineByLineAndNormalized()
    {
        var config = Config();
        config.Practice.CustomText = "  CQ CQ\r\nDE  HA5XYZ \n\n";

        Assert.AreEqual("CQ_CQ DE_HA5XYZ", Generate(PracticeTopic.CustomText, config, 1));
        Assert.AreEqual("CQ_CQ DE_HA5XYZ", Generate(PracticeTopic.CustomText, config, 100));
    }

    [TestMethod]
    public void CustomText_Shuffled_MixesTheLinesAndKeepsEachLineWhole()
    {
        var config = Config();
        var lines = new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "NOVI SAD" };
        config.Practice.CustomText = string.Join('\n', lines);
        config.Practice.ShuffleCustomText = true;

        var orders = Enumerable.Range(0, 20).Select(_ => Generate(PracticeTopic.CustomText, config)).ToList();

        foreach (var order in orders)
        {
            CollectionAssert.AreEquivalent(lines.Select(l => l.Replace(' ', '_')).ToArray(), order.Split(' '));
        }

        Assert.IsTrue(orders.Distinct().Count() > 1, "Twenty shuffles all kept the written order.");
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

        var items = PracticeItems.SplitItems(PracticeItems.ToChecked(Generate(PracticeTopic.Mixed, config, 200)));

        Assert.Contains("DEN_HAAG", items);
        Assert.DoesNotContain("DEN", items);
    }

    [TestMethod]
    public void CustomText_OneLine_IsOneItem()
    {
        var config = Config();
        config.Practice.CustomText = "QSL VIA BURO";

        Assert.AreEqual("QSL_VIA_BURO", Generate(PracticeTopic.CustomText, config));
        CollectionAssert.AreEqual(new[] { "QSL_VIA_BURO" }, PracticeItems.SplitItems(Generate(PracticeTopic.CustomText, config)));
    }

    [TestMethod]
    public void Repetitions_DefaultToTwiceForCallsignsCitiesAndNamesAndOnceForTheRest()
    {
        var defaults = new TopicRepetitions();

        foreach (var twice in new[] { defaults.CallsignsEmea, defaults.CallsignsGlobal, defaults.Cities, defaults.Names })
        {
            Assert.AreEqual(2, twice.Minimum);
            Assert.AreEqual(2, twice.Maximum);
        }

        foreach (var once in new[] { defaults.Abbreviations, defaults.CustomText })
        {
            Assert.AreEqual(1, once.Minimum);
            Assert.AreEqual(1, once.Maximum);
        }
    }

    /// <summary>The generated items, each with its repetitions (split at the repeat joiner).</summary>
    private static List<string[]> Repetitions(string text) =>
        PracticeItems.SplitItems(text).Select(i => i.Split(PracticeItems.RepeatJoiner)).ToList();

    [TestMethod]
    public void WordTopics_SendEachItemAsOftenInARowAsTheRepetitionSays()
    {
        var config = Config();
        config.Topics.Cities = ["NEW YORK", "WIEN"];
        config.Topics.Repetitions.Cities = new(2, 2);

        var items = Repetitions(Generate(PracticeTopic.Cities, config, 30));

        Assert.IsNotEmpty(items);
        foreach (var item in items)
        {
            Assert.HasCount(2, item);
            Assert.AreEqual(item[0], item[1]);
            Assert.IsTrue(item[0] is "NEW_YORK" or "WIEN", item[0]);
        }
    }

    [TestMethod]
    public void Callsigns_RepeatTheSameExpandedCallsign()
    {
        var config = Config();
        config.Topics.Repetitions.CallsignsEmea = new(3, 3);

        foreach (var item in Repetitions(Generate(PracticeTopic.CallsignsEmea, config, 30)))
        {
            Assert.HasCount(3, item);
            Assert.IsTrue(item.All(c => c == item[0]), string.Join('|', item));
        }
    }

    [TestMethod]
    public void Repetition_DrawsEveryCountBetweenTheMinimumAndTheMaximum()
    {
        var counts = Enumerable.Range(0, 500)
            .Select(_ => PracticeTextGeneratorFactory.RepeatCount(new Repetition(1, 3)))
            .ToHashSet();

        CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, counts.ToArray());
    }

    [TestMethod]
    [DataRow(0, 0, 1)]
    [DataRow(5, 2, 5)]
    [DataRow(20, 30, Repetition.Limit)]
    public void Repetition_OutOfRangeValuesAreKeptInRange(int minimum, int maximum, int expected)
    {
        Assert.AreEqual(expected, PracticeTextGeneratorFactory.RepeatCount(new Repetition(minimum, maximum)));
    }

    [TestMethod]
    public void CustomText_RepeatsEachLineAsAWhole()
    {
        var config = Config();
        config.Practice.CustomText = "NOVI SAD\nCQ DE HA5XYZ";
        config.Topics.Repetitions.CustomText = new(2, 2);

        var generated = Generate(PracticeTopic.CustomText, config);

        Assert.AreEqual("NOVI_SAD|NOVI_SAD CQ_DE_HA5XYZ|CQ_DE_HA5XYZ", generated);
        Assert.AreEqual("NOVI SAD NOVI SAD CQ DE HA5XYZ CQ DE HA5XYZ", PracticeItems.ToPlayback(generated), "Never NOVI NOVI SAD SAD.");
        Assert.AreEqual("NOVI_SAD CQ_DE_HA5XYZ", PracticeItems.ToChecked(generated));
    }

    [TestMethod]
    public void Mixed_RepeatsEachItemAsItsOwnTopicWould_AndNeverTheGroups()
    {
        var config = Config();
        config.Topics.CallsignPatternsEmea = [];
        config.Topics.CallsignPatternsGlobal = [];
        config.Topics.Cities = [];
        config.Topics.Abbreviations = [];
        config.Topics.Names = ["LACI"];
        config.Topics.Repetitions.Names = new(3, 3);

        var items = Repetitions(Generate(PracticeTopic.Mixed, config, 200));

        Assert.IsTrue(items.Any(i => i[0] == "LACI") && items.Any(i => i[0] != "LACI"));
        foreach (var item in items)
            Assert.HasCount(item[0] == "LACI" ? 3 : 1, item, string.Join('|', item));
    }

    [TestMethod]
    public void Repetitions_CountAgainstTheLetterBudget()
    {
        var config = Config();
        config.Topics.Repetitions.Names = new(2, 2);

        // One group is worth 5 letters: the first LACI played twice (8 letters) already covers it.
        Assert.AreEqual("LACI|LACI", Generate(PracticeTopic.Names, config, 1));
        // 10 groups are 50 letters: 7 names played twice (56 letters), not 13 names.
        Assert.HasCount(7, Repetitions(Generate(PracticeTopic.Names, config, 10)));
    }

    [TestMethod]
    public void Callsigns_RunsStayWithinTheRange()
    {
        var config = Config();
        config.Topics.Repetitions.CallsignsEmea = new(2, 4);

        var runs = Enumerable.Range(0, 20)
            .SelectMany(_ => Repetitions(Generate(PracticeTopic.CallsignsEmea, config, 30)))
            .Select(i => i.Length)
            .ToList();

        Assert.IsTrue(runs.All(r => r is >= 2 and <= 4), string.Join(',', runs));
        CollectionAssert.IsSubsetOf(new[] { 2, 3, 4 }, runs.Distinct().ToArray());
    }

    [TestMethod]
    public void CustomText_ShuffledAndRepeated_KeepsTheRepetitionsTogether()
    {
        var config = Config();
        config.Practice.CustomText = "ONE\nTWO\nTHREE FOUR\nFIVE";
        config.Practice.ShuffleCustomText = true;
        config.Topics.Repetitions.CustomText = new(2, 2);

        var items = Repetitions(Generate(PracticeTopic.CustomText, config));

        Assert.HasCount(4, items);
        Assert.IsTrue(items.All(i => i.Length == 2 && i[0] == i[1]));
        CollectionAssert.AreEquivalent(new[] { "ONE", "TWO", "THREE_FOUR", "FIVE" }, items.Select(i => i[0]).ToArray());
    }

    [TestMethod]
    public void Mixed_RepeatsTheCustomTextLinesByTheCustomTextSetting()
    {
        var config = Config();
        config.Topics.CallsignPatternsEmea = [];
        config.Topics.CallsignPatternsGlobal = [];
        config.Topics.Cities = [];
        config.Topics.Names = [];
        config.Topics.Abbreviations = [];
        config.Practice.CustomText = "MYWORD";
        config.Topics.Repetitions.CustomText = new(2, 2);

        var items = Repetitions(Generate(PracticeTopic.Mixed, config, 200));

        Assert.Contains("MYWORD", items.Select(i => i[0]).ToList());
        foreach (var item in items)
            Assert.HasCount(item[0] == "MYWORD" ? 2 : 1, item);
    }

    [TestMethod]
    public void Groups_IgnoreTheRepetitions()
    {
        var config = Config();
        config.Topics.Repetitions = new TopicRepetitions
        {
            CallsignsEmea = new(5, 5), CallsignsGlobal = new(5, 5), Cities = new(5, 5),
            Names = new(5, 5), Abbreviations = new(5, 5), CustomText = new(5, 5),
        };

        var text = Generate(PracticeTopic.Groups, config, 10);
        Assert.HasCount(10, text.Split(' '));
        Assert.DoesNotContain(PracticeItems.RepeatJoiner.ToString(), text);
    }

    [TestMethod]
    public void Repeat_WithoutARepetition_SendsTheItemOnce()
    {
        Assert.AreEqual("QRZ", PracticeTextGeneratorFactory.Repeat("QRZ", null));
        Assert.AreEqual("QRZ|QRZ|QRZ", PracticeTextGeneratorFactory.Repeat("QRZ", new Repetition(3, 3)));
    }

    [TestMethod]
    public void RepeatedItems_AreCheckedOnceAndPlayedEveryTime()
    {
        var generated = "LACI|LACI NEW_YORK|NEW_YORK QRZ";

        Assert.AreEqual("LACI NEW_YORK QRZ", PracticeItems.ToChecked(generated));
        Assert.AreEqual("LACI LACI NEW YORK NEW YORK QRZ", PracticeItems.ToPlayback(generated));
        Assert.AreEqual(string.Empty, PracticeItems.ToChecked(null));
        Assert.AreEqual("AB12C", PracticeItems.ToChecked("AB12C"), "Text without repetitions is unchanged.");
    }

    [TestMethod]
    public void Repetition_WithoutArguments_SendsOnce()
    {
        var repetition = new Repetition();

        Assert.AreEqual(1, repetition.Minimum);
        Assert.AreEqual(1, repetition.Maximum);
    }

    [TestMethod]
    public void Create_UnknownTopic_Throws()
    {
        var factory = new PracticeTextGeneratorFactory(new MorseGenerator());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => factory.Create((PracticeTopic)99, Config()));
    }

    [TestMethod]
    public void Mixed_KeepsTheLinesOfTheCustomTextWhole()
    {
        var config = Config();
        config.Topics.CallsignPatternsEmea = [];
        config.Topics.CallsignPatternsGlobal = [];
        config.Topics.Cities = [];
        config.Topics.Names = [];
        config.Topics.Abbreviations = [];
        config.Practice.CustomText = "NOVI SAD";
        config.Topics.Repetitions.CustomText = new(2, 2);

        var played = PracticeItems.ToPlayback(Generate(PracticeTopic.Mixed, config, 200));

        Assert.Contains("NOVI SAD NOVI SAD", played);
        Assert.DoesNotContain("NOVI NOVI", played);
        Assert.DoesNotContain("SAD SAD", played);
    }
}
