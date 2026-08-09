using System.Collections.Generic;
using System.Linq;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class MorseGeneratorTests
{
    private readonly MorseGenerator _generator = new();

    [TestMethod]
    public void GenerateGroupsOf5_ProducesRequestedNumberOfGroups()
    {
        var output = _generator.GenerateGroupsOf5("ABCDE", numberOfGroups: 4);

        var groups = output.Split(' ');
        Assert.HasCount(4, groups);
    }

    [TestMethod]
    public void GenerateGroupsOf5_EachGroupHasFiveCharacters()
    {
        var output = _generator.GenerateGroupsOf5("ABCDE", numberOfGroups: 10);

        foreach (var group in output.Split(' '))
        {
            Assert.HasCount(5, group);
        }
    }

    [TestMethod]
    public void GenerateGroupsOf5_OnlyUsesCharactersFromTheSet()
    {
        const string set = "ABCDE";
        var output = _generator.GenerateGroupsOf5(set, numberOfGroups: 20);

        foreach (var ch in output.Replace(" ", string.Empty))
        {
            Assert.Contains(ch, set);
        }
    }

    [TestMethod]
    public void GenerateGroupsOf5_ZeroGroups_ReturnsEmpty()
    {
        var output = _generator.GenerateGroupsOf5("ABCDE", numberOfGroups: 0);

        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void GenerateGroupsOf5_KeepsMultiCharacterProsignsIntact()
    {
        // The set contains only prosigns, so every emitted token must be one of them.
        const string set = "<bk><sk><ar>";
        var validTokens = new HashSet<string> { "<bk>", "<sk>", "<ar>" };

        var output = _generator.GenerateGroupsOf5(set, numberOfGroups: 3);

        var tokens = Tokenize(output.Replace(" ", string.Empty));
        Assert.IsTrue(tokens.All(validTokens.Contains), $"Unexpected token in: {output}");
        // 3 groups x 5 tokens each.
        Assert.HasCount(15, tokens);
    }

    [TestMethod]
    public void GenerateGroupsOf5_SingleGroup_HasNoTrailingSeparator()
    {
        var output = _generator.GenerateGroupsOf5("ABCDE", numberOfGroups: 1);

        Assert.DoesNotContain(' ', output);
        Assert.HasCount(5, output);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                var end = text.IndexOf('>', i);
                tokens.Add(text.Substring(i, end - i + 1));
                i = end;
            }
            else
            {
                tokens.Add(text[i].ToString());
            }
        }

        return tokens;
    }
}
