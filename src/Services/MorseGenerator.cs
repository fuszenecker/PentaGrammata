using System;
using System.Collections.Generic;
using System.Text;

namespace PentaGrammata.Services;

public class MorseGenerator : IMorseGenerator
{
    public string GenerateGroupsOf5(string characterPalette, int numberOfGroups)
    {
        List<string> morseCodeList = [];

        for (int i = 0; i < characterPalette.Length; i++)
        {
            char c = characterPalette[i];

            if (c == '<')
            {
                int endIndex = characterPalette.IndexOf('>', i);
                if (endIndex == -1)
                    continue; // Invalid format, skip

                string specialSequence = characterPalette.Substring(i, endIndex - i + 1);
                morseCodeList.Add(specialSequence); // Add the special sequence as is
                i = endIndex; // Move index to end of special sequence
            }
            else
            {
                morseCodeList.Add(characterPalette[i].ToString()); // Add the character as is
            }
        }

        var random = new Random(DateTime.Now.Millisecond);
        var result = new StringBuilder();

        for (int i = 0; i < numberOfGroups; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                int index = random.Next(morseCodeList.Count);
                string morseCode = morseCodeList[index];
                result.Append(morseCode);
            }

            if (i < numberOfGroups - 1)
                result.Append(" "); // Add space between groups
        }

        return result.ToString();
    }
}