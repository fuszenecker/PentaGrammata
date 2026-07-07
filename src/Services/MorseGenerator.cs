using System;
using System.Collections.Generic;
using System.Text;

namespace PentaGrammata.Services;

public class MorseGenerator : IMorseGenerator
{
    public string GenerateGroupsOf5(string characterSet, int numberOfGroups)
    {
        List<string> morseCodeList = [];

        for (int i = 0; i < characterSet.Length; i++)
        {
            char c = characterSet[i];

            if (c == '<')
            {
                int endIndex = characterSet.IndexOf('>', i);
                if (endIndex == -1)
                {
                    continue;
                }

                string specialSequence = characterSet.Substring(i, endIndex - i + 1);
                morseCodeList.Add(specialSequence);
                i = endIndex;
            }
            else
            {
                morseCodeList.Add(characterSet[i].ToString());
            }
        }

        var result = new StringBuilder();

        for (int i = 0; i < numberOfGroups; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                int index = Random.Shared.Next(morseCodeList.Count);
                string morseCode = morseCodeList[index];
                result.Append(morseCode);
            }

            if (i < numberOfGroups - 1)
            {
                result.Append(' ');
            }
        }

        return result.ToString();
    }
}