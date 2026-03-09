using System;
using System.Collections.Generic;

namespace CraftimizerCN.Utils;

public static class MacroNaming
{
    public static HashSet<string> CreateExistingNameSet(IEnumerable<string?> names)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var trimmed = name?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                set.Add(trimmed);
        }

        return set;
    }

    public static string GenerateDefaultMacroName(ISet<string> existingNames)
    {
        var index = 1;
        while (true)
        {
            var candidate = $"{index}号宏";
            if (!existingNames.Contains(candidate))
                return candidate;
            index++;
        }
    }
}
