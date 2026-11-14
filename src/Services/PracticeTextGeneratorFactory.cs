using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

/// <summary>
/// Builds the generator of each <see cref="PracticeTopic"/>: random groups from the selected
/// character set, callsigns from patterns, words from the topic lists, the custom text, or a
/// mix of all of them item by item.
/// </summary>
public sealed class PracticeTextGeneratorFactory : IPracticeTextGeneratorFactory
{
    internal const string FallbackCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

    private readonly IMorseGenerator _morseGenerator;

    public PracticeTextGeneratorFactory(IMorseGenerator morseGenerator)
    {
        _morseGenerator = morseGenerator;
    }

    public IPracticeTextGenerator Create(PracticeTopic topic, AppConfiguration configuration)
    {
        var topics = configuration.Topics ?? new Topics();
        return topic switch
        {
            PracticeTopic.Groups => new GroupsGenerator(_morseGenerator, SelectedCharacters(configuration)),
            PracticeTopic.CallsignsEmea => new ItemsGenerator(CallsignSource(topics.CallsignPatternsEmea)),
            PracticeTopic.CallsignsGlobal => new ItemsGenerator(CallsignSource(topics.CallsignPatternsGlobal)),
            PracticeTopic.Cities => new ItemsGenerator(WordSource(topics.Cities)),
            PracticeTopic.Names => new ItemsGenerator(WordSource(topics.Names)),
            PracticeTopic.Abbreviations => new ItemsGenerator(WordSource(topics.Abbreviations)),
            PracticeTopic.CustomText => new CustomTextGenerator(configuration.Practice.CustomText, configuration.Practice.ShuffleCustomText),
            PracticeTopic.Mixed => new ItemsGenerator(MixedSource(configuration, topics)),
            _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, "Unknown practice topic."),
        };
    }

    private static string SelectedCharacters(AppConfiguration configuration)
    {
        if (configuration.CharacterSets.TryGetValue(configuration.Practice.DefaultCharacterSet, out var selected)
            && !string.IsNullOrWhiteSpace(selected))
        {
            return selected;
        }

        var first = configuration.CharacterSets.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return string.IsNullOrWhiteSpace(first) ? FallbackCharacterSet : first;
    }

    private static List<string> Usable(IEnumerable<string>? items) =>
        (items ?? []).Select(i => CustomTextNormalizer.Normalize(i)).Where(i => i.Length > 0).ToList();

    private static Func<string>? WordSource(IEnumerable<string>? words)
    {
        var list = Usable(words).Select(w => w.ToUpperInvariant()).ToList();
        return list.Count == 0 ? null : () => list[Random.Shared.Next(list.Count)];
    }

    private static Func<string>? CallsignSource(IEnumerable<string>? patterns)
    {
        var list = Usable(patterns);
        return list.Count == 0 ? null : () => ExpandCallsign(list[Random.Shared.Next(list.Count)]);
    }

    /// <summary>
    /// Expands a callsign pattern: <c>#</c> becomes a random digit, a lowercase letter a random
    /// letter, everything else is kept as written.
    /// </summary>
    internal static string ExpandCallsign(string pattern)
    {
        var result = new StringBuilder(pattern.Length);
        foreach (var c in pattern)
        {
            if (c == '#')
                result.Append((char)('0' + Random.Shared.Next(10)));
            else if (c is >= 'a' and <= 'z')
                result.Append((char)('A' + Random.Shared.Next(26)));
            else
                result.Append(c);
        }

        return result.ToString();
    }

    private Func<string> MixedSource(AppConfiguration configuration, Topics topics)
    {
        var characters = SelectedCharacters(configuration);
        var sources = new List<Func<string>?>
        {
            () => _morseGenerator.GenerateGroupsOf5(characters, 1),
            CallsignSource(topics.CallsignPatternsEmea),
            CallsignSource(topics.CallsignPatternsGlobal),
            WordSource(topics.Cities),
            WordSource(topics.Names),
            WordSource(topics.Abbreviations),
            WordSource(CustomTextNormalizer.Normalize(configuration.Practice.CustomText).Split(' ', StringSplitOptions.RemoveEmptyEntries)),
        }.OfType<Func<string>>().ToList();

        return () => sources[Random.Shared.Next(sources.Count)]();
    }

    private sealed class GroupsGenerator(IMorseGenerator morseGenerator, string characters) : IPracticeTextGenerator
    {
        public string Generate(int groupCount) => morseGenerator.GenerateGroupsOf5(characters, groupCount);
    }

    /// <summary>Adds items until the session has sent as many characters as its groups would.</summary>
    private sealed class ItemsGenerator(Func<string>? next) : IPracticeTextGenerator
    {
        public string Generate(int groupCount)
        {
            if (next is null)
                return string.Empty;

            int budget = Math.Max(1, groupCount) * 5;
            var items = new List<string>();
            int sent = 0;
            while (sent < budget)
            {
                var item = next();
                items.Add(item);
                sent += MorseAlphabet.Tokenize(item).Count(t => t != " ");
            }

            return string.Join(' ', items);
        }
    }

    private sealed class CustomTextGenerator(string? text, bool shuffle) : IPracticeTextGenerator
    {
        public string Generate(int groupCount)
        {
            var normalized = CustomTextNormalizer.Normalize(text);
            if (!shuffle)
                return normalized;

            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Random.Shared.Shuffle(words);
            return string.Join(' ', words);
        }
    }
}
