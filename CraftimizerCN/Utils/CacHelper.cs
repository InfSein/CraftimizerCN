using CraftimizerCN.Simulator.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraftimizerCN.Utils;

public static class CacHelper
{
    private const int CacVersionForExport = 1;
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
        { 37, ActionType.DutyAction2 },
    };
    private static readonly Dictionary<ActionType, int> ActionTypeToCacId = CacIdToActionType.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static bool TryEncodeActions(IReadOnlyList<ActionType> actions, out string code, out string error)
    {
        code = string.Empty;
        error = string.Empty;

        if (actions.Count == 0)
        {
            code = $"{CacVersionForExport}v1b";
            return true;
        }

        var ids = new int[actions.Count];
        var maxId = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (!ActionTypeToCacId.TryGetValue(action, out var id))
            {
                error = $"不支持的技能：{action}。";
                return false;
            }
            ids[i] = id;
            if (id > maxId)
                maxId = id;
        }

        var bitWidth = 1;
        while ((1 << bitWidth) <= maxId)
            bitWidth++;
        if (bitWidth <= 0 || bitWidth > 30)
        {
            error = "CAC 工序码位宽无效。";
            return false;
        }

        var bytes = new List<byte>();
        var bitBuffer = 0L;
        var bitLength = 0;
        foreach (var id in ids)
        {
            bitBuffer = (bitBuffer << bitWidth) | id;
            bitLength += bitWidth;
            while (bitLength >= 8)
            {
                bitLength -= 8;
                bytes.Add((byte)((bitBuffer >> bitLength) & 0xFF));
            }
        }
        if (bitLength > 0)
            bytes.Add((byte)((bitBuffer << (8 - bitLength)) & 0xFF));

        var payload = Convert.ToBase64String(bytes.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        code = $"{CacVersionForExport}v{bitWidth}b{payload}";
        return true;
    }

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
