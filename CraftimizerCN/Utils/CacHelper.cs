using CraftimizerCN.Simulator.Actions;
using System;
using System.Collections.Generic;

namespace CraftimizerCN.Utils;

public static class CacHelper
{
    private static readonly Dictionary<int, ActionType> CacIdToActionType = new()
    {
        { 1, ActionType.GreatStrides },
        { 2, ActionType.Manipulation },
        { 3, ActionType.WasteNot },
        { 4, ActionType.WasteNot2 },
        { 5, ActionType.Innovation },
        { 6, ActionType.FinalAppraisal },
        { 7, ActionType.Veneration },
        { 8, ActionType.BasicSynthesis },
        { 9, ActionType.BasicTouch },
        { 10, ActionType.MastersMend },
        { 11, ActionType.StandardTouch },
        { 12, ActionType.Observe },
        { 13, ActionType.PreciseTouch },
        { 14, ActionType.CarefulSynthesis },
        { 15, ActionType.PrudentTouch },
        { 16, ActionType.TrainedEye },
        { 17, ActionType.PreparatoryTouch },
        { 18, ActionType.IntensiveSynthesis },
        { 19, ActionType.DelicateSynthesis },
        { 20, ActionType.ByregotsBlessing },
        { 21, ActionType.HastyTouch },
        { 22, ActionType.RapidSynthesis },
        { 23, ActionType.TricksOfTheTrade },
        { 24, ActionType.MuscleMemory },
        { 25, ActionType.Reflect },
        { 26, ActionType.CarefulObservation },
        { 27, ActionType.Groundwork },
        { 28, ActionType.AdvancedTouch },
        { 29, ActionType.HeartAndSoul },
        { 30, ActionType.PrudentSynthesis },
        { 31, ActionType.TrainedFinesse },
        { 32, ActionType.RefinedTouch },
        { 33, ActionType.DaringTouch },
        { 34, ActionType.QuickInnovation },
        { 35, ActionType.ImmaculateMend },
        { 36, ActionType.TrainedPerfection },
    };

    public static bool TryDecodeActions(string payload, int bitWidth, out IReadOnlyList<ActionType> actions, out string error)
    {
        actions = Array.Empty<ActionType>();
        error = string.Empty;

        byte[] bytes;
        try
        {
            var padded = payload.Replace('-', '+').Replace('_', '/');
            var mod = padded.Length % 4;
            if (mod != 0)
                padded = padded.PadRight(padded.Length + (4 - mod), '=');
            bytes = Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            error = "CAC 工序码内容无效。";
            return false;
        }

        var bitBuffer = 0L;
        var bitLength = 0;
        var mask = (1L << bitWidth) - 1;
        var parsedActions = new List<ActionType>();
        var anyId = false;

        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitLength += 8;
            while (bitLength >= bitWidth)
            {
                bitLength -= bitWidth;
                var cacId = (int)((bitBuffer >> bitLength) & mask);
                if (cacId <= 0)
                    continue;
                anyId = true;
                if (!CacIdToActionType.TryGetValue(cacId, out var action))
                {
                    error = $"CAC 工序码包含未知技能：{cacId}。";
                    return false;
                }
                parsedActions.Add(action);
            }
        }

        if (!anyId)
        {
            error = "未能在提供的 CAC 工序码中找到任何合法技能。";
            return false;
        }

        actions = parsedActions;
        return true;
    }
}
