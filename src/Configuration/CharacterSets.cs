using System.Collections.Generic;

namespace PentaGrammata.Configuration;

public sealed class CharacterSets : Dictionary<string, string>
{
    public CharacterSets Clone()
    {
        var clone = new CharacterSets();
        foreach (var kv in this)
        {
            clone[kv.Key] = kv.Value;
        }

        return clone;
    }
}
