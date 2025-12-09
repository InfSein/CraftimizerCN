using CraftimizerCN.Simulator.Actions;
using System.Collections.Frozen;

namespace CraftimizerCN.Simulator;

public enum ActionCategory
{
    FirstTurn,
    Synthesis,
    Quality,
    Durability,
    Buffs,
    Combo,
    Other
}

public static class ActionCategoryUtils
{
    private static readonly FrozenDictionary<ActionCategory, ActionType[]> SortedActions;

    static ActionCategoryUtils()
    {
        SortedActions =
            Enum.GetValues<ActionType>()
            .GroupBy(a => a.Category())
            .ToFrozenDictionary(g => g.Key, g => g.OrderBy(a => a.Level()).ToArray());
    }

    public static IReadOnlyList<ActionType> GetActions(this ActionCategory me)
    {
        if (SortedActions.TryGetValue(me, out var actions))
            return actions;

        throw new ArgumentException($"Unknown action category {me}", nameof(me));
    }

    public static string GetDisplayName(this ActionCategory category) =>
        category switch
        {
            ActionCategory.FirstTurn => "首次作业",
            ActionCategory.Synthesis => "作业",
            ActionCategory.Quality => "加工",
            ActionCategory.Durability => "耐久度",
            ActionCategory.Buffs => "增益",
            ActionCategory.Other => "其他",
            _ => category.ToString()
        };
}
