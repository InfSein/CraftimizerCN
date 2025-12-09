using CraftimizerCN.Simulator;
using CraftimizerCN.Simulator.Actions;
using CraftimizerCN.Solver;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CraftimizerCN.Plugin.Windows;

public sealed class Settings : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    private static Configuration Config => Service.Configuration;

    private static float OptionWidth => 200 * ImGuiHelpers.GlobalScale;
    private static Vector2 OptionButtonSize => new(OptionWidth, ImGui.GetFrameHeight());

    private string? SelectedTab { get; set; }

    private IFontHandle HeaderFont { get; }
    private IFontHandle SubheaderFont { get; }

    public Settings() : base("CraftimizerCN 偏好设置", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        HeaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 2f)));
        SubheaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 1.5f)));

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new(450, 400),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void SelectTab(string label)
    {
        SelectedTab = label;
    }

    private ImRaii.IEndObject TabItem(string label)
    {
        var isSelected = string.Equals(SelectedTab, label, StringComparison.Ordinal);
        if (isSelected)
        {
            SelectedTab = null;
            var open = true;
            return ImRaii.TabItem(label, ref open, ImGuiTabItemFlags.SetSelected);
        }
        return ImRaii.TabItem(label);
    }

    private static void DrawOption(string label, string tooltip, bool val, Action<bool> setter, ref bool isDirty)
    {
        if (ImGui.Checkbox(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static void DrawOption<T>(string label, string tooltip, T value, T min, T max, Action<T> setter, ref bool isDirty) where T : struct, INumber<T>
    {
        ImGui.SetNextItemWidth(OptionWidth);
        var text = value.ToString();
        ArgumentNullException.ThrowIfNull(text, nameof(value));
        if (ImGui.InputText(label, ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal))
        {
            if (T.TryParse(text, null, out var newValue))
            {
                newValue = T.Clamp(newValue, min, max);
                if (value != newValue)
                {
                    setter(newValue);
                    isDirty = true;
                }
            }
        }
        else
        {
            var newValue = T.Clamp(value, min, max);
            if (value != newValue)
            {
                setter(newValue);
                isDirty = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static void DrawOption(string label, string tooltip, string value, Action<string> setter, ref bool isDirty)
    {
        ImGui.SetNextItemWidth(OptionWidth);
        var text = value;
        if (ImGui.InputText(label, ref text, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            if (!string.Equals(value, text, StringComparison.Ordinal))
            {
                setter(text);
                isDirty = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static void DrawOption<T>(string label, string tooltip, Func<T, string> getName, Func<T, string> getTooltip, T value, Action<T> setter, ref bool isDirty, params T[] excludedValues) where T : struct, Enum
    {
        ImGui.SetNextItemWidth(OptionWidth);
        using (var combo = ImRaii.Combo(label, getName(value)))
        {
            if (combo)
            {
                foreach (var type in Enum.GetValues<T>())
                {
                    if (excludedValues.Contains(type))
                        continue;
                    if (ImGui.Selectable(getName(type), value.Equals(type)))
                    {
                        setter(type);
                        isDirty = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.TooltipWrapped(getTooltip(type));
                }
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static string GetAlgorithmName(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot => "一次性 / Oneshot",
            SolverAlgorithm.OneshotForked => "一次性(分支) / Oneshot Forked",
            SolverAlgorithm.Stepwise => "逐步 / Stepwise",
            SolverAlgorithm.StepwiseForked => "逐步(分支) / Stepwise Forked",
            SolverAlgorithm.StepwiseGenetic => "逐步(遗传) / Stepwise Genetic",
            SolverAlgorithm.Raphael => "最优 / Optimal",
            _ => "Unknown",
        };

    private static string GetAlgorithmTooltip(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot => "运行所有迭代并选出最佳宏",
            SolverAlgorithm.OneshotForked => "一次性算法，但同时使用多个求解器",
            SolverAlgorithm.Stepwise => "运行所有迭代并选出下一个最佳步骤，随后以此前步骤为起点重复执行",
            SolverAlgorithm.StepwiseForked => "逐步算法，但同时使用多个求解器",
            SolverAlgorithm.StepwiseGenetic => "逐步(分支)算法，但从求解器中选出前 N 个最佳的下一步，并将每一个都作为等权的起始点",
            SolverAlgorithm.Raphael => "每次都能找到最佳解。此求解器与其他求解器的选项差异很大，因为它使用完全不同的算法设计。",
            _ => "Unknown"
        };

    private static string GetCopyTypeName(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow => "打开复制窗口",
            MacroCopyConfiguration.CopyType.CopyToMacro => "复制到用户宏",
            MacroCopyConfiguration.CopyType.CopyToClipboard => "复制到剪贴板",
            MacroCopyConfiguration.CopyType.CopyToMacroMate => "复制到 Macro Mate",
            _ => "Unknown",
        };

    private static string GetCopyTypeTooltip(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow =>       "打开一个新窗口，从而自行查看和复制宏的内容。",
            MacroCopyConfiguration.CopyType.CopyToMacro =>      "直接将宏内容设置到游戏的用户宏中。",
            MacroCopyConfiguration.CopyType.CopyToClipboard =>  "将宏内容复制到剪贴板。有多个宏时会用空白行来分隔它们。",
            MacroCopyConfiguration.CopyType.CopyToMacroMate =>  "将宏内容复制到 Macro Mate，你需要先安装这个插件。",
            _ => "Unknown"
        };

    private static string GetProgressBarTypeName(Configuration.ProgressBarType type) =>
        type switch
        {
            Configuration.ProgressBarType.Colorful => "多彩",
            Configuration.ProgressBarType.Simple => "简单",
            Configuration.ProgressBarType.None => "无",
            _ => "Unknown",
        };

    private static string GetProgressBarTooltip(Configuration.ProgressBarType type) =>
        type switch
        {
            Configuration.ProgressBarType.Colorful => "五颜六色",
            Configuration.ProgressBarType.Simple => "简朴灰度",
            Configuration.ProgressBarType.None => "没有进度条，只显示百分比文本",
            _ => "Unknown"
        };

    public override void Draw()
    {
        if (ImGui.BeginTabBar("settingsTabBar"))
        {
            DrawTabGeneral();
            DrawTabRecipeNote();
            if (Config.EnableSynthHelper)
                DrawTabSynthHelper();
            DrawTabMacroEditor();
            DrawTabAbout();

            ImGui.EndTabBar();
        }
    }

    private void DrawTabGeneral()
    {
        using var tab = TabItem("通用");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "启用制作助手",
            "在制作期间的游戏窗口旁添加一个助手窗口，以提示接下来的推荐步骤。" +
            "对于高难度配方格外有效，因为特殊状态会极大地影响使用技能的收益。",
            Config.EnableSynthHelper,
            v => Config.EnableSynthHelper = v,
            ref isDirty
        );

        DrawOption(
            "宏效果统计只显示一条",
            "在计算和显示某个宏的制作效果时只展示最重要的一条。" +
            "如果这个宏能够完成配方的进展，显示宏能够推进的品质；否则显示宏能够推进的进展。" +
            "相应的，“剩余耐久”和“剩余CP”不会显示。",
            Config.ShowOptimalMacroStat,
            v => Config.ShowOptimalMacroStat = v,
            ref isDirty
        );

        DrawOption(
            "检查图纸",
            "在制作助手向你建议使用专家技能之前，预先检查你是否持有“能工巧匠图纸”。",
            Config.CheckDelineations,
            v => Config.CheckDelineations = v,
            ref isDirty
        );

        DrawOption(
            "可靠性测试次数",
            "在编辑器中测试宏的可靠性时，将运行此处所指定次数的试验。" +
            "虽然可以自由指定，但为了获得足够可靠的数据分布，设置值应当不低于 100。" +
            "如果次数太少，可能无法发现离群值，且平均值可能会被扭曲。",
            Config.ReliabilitySimulationCount,
            5,
            5000,
            v => Config.ReliabilitySimulationCount = v,
            ref isDirty
        );

        DrawOption(
            "进度条风格",
            "调整手法左侧进度条的显示风格。",
            GetProgressBarTypeName,
            GetProgressBarTooltip,
            Config.ProgressType,
            v => Config.ProgressType = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);

        using (var panel = ImRaii2.GroupPanel("复制相关", -1, out _))
        {
            DrawOption(
                "复制宏的方式",
                "点击复制宏按钮时进行的操作。",
                GetCopyTypeName,
                GetCopyTypeTooltip,
                Config.MacroCopy.Type,
                v => Config.MacroCopy.Type = v,
                ref isDirty
            );

            if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacroMate &&
                !Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && string.Equals(p.InternalName, "MacroMate", StringComparison.Ordinal)))
            {
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                {
                    using var font = ImRaii.PushFont(UiBuilder.IconFont);
                    ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                }
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("还没有安装 Macro Mate 插件");
            }

            if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "向下递进",
                    "在有多个宏需要设置时，默认的方式是向右递进，如#1、#2、#3。启用此选项会改为向下递进，如#1、#11、#21。",
                    Config.MacroCopy.CopyDown,
                    v => Config.MacroCopy.CopyDown = v,
                    ref isDirty
                );

                DrawOption(
                    "复制到公用宏中",
                    "在公用宏标签页中设置宏。" +
                    "取消勾选此选项则会在角色专用宏标签页中设置宏。",
                    Config.MacroCopy.SharedMacro,
                    v => Config.MacroCopy.SharedMacro = v,
                    ref isDirty
                );

                DrawOption(
                    "起始宏序号",
                    "决定第一个宏将要设置到的序号。" +
                    "后续的宏将递进设置到之后的宏位置。",
                    Config.MacroCopy.StartMacroIdx,
                    0, 99,
                    v => Config.MacroCopy.StartMacroIdx = v,
                    ref isDirty
                );

                DrawOption(
                    "最大宏数",
                    "决定最多连续设置几个宏。" +
                    "如果手法生成的宏数量超过了这个数目，则将自动打开复制窗口。",
                    Config.MacroCopy.MaxMacroCount,
                    1, 99,
                    v => Config.MacroCopy.MaxMacroCount = v,
                    ref isDirty
                );
            }
            else if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacroMate)
            {
                DrawOption(
                    "宏名称",
                    "决定在 Macro Mate 中将要创建的宏的名称。",
                    Config.MacroCopy.MacroMateName,
                    v => Config.MacroCopy.MacroMateName = v,
                    ref isDirty
                );

                DrawOption(
                    "宏归属",
                    "决定将要创建的宏归属于哪一个组。如果不需要设置归属，将输入框留空或是填入 \"/\" 。",
                    Config.MacroCopy.MacroMateParent,
                    v => Config.MacroCopy.MacroMateParent = v,
                    ref isDirty
                );
            }

            DrawOption(
                "显示“已复制”通知",
                "当宏内容被成功复制或应用时，在游戏右下方显示通知。",
                Config.MacroCopy.ShowCopiedMessage,
                v => Config.MacroCopy.ShowCopiedMessage = v,
                ref isDirty
            );

            if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
            {
                DrawOption(
                    "启用宏连锁(/nextmacro)",
                    "将过渡宏的最后一行改为 /nextmacro ，" +
                    "从而能够一键连续执行完整个制作手法流程。",
                    Config.MacroCopy.UseNextMacro,
                    v => Config.MacroCopy.UseNextMacro = v,
                    ref isDirty
                );

                if (Config.MacroCopy.UseNextMacro &&
                    !Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && string.Equals(p.InternalName, "MacroChain", StringComparison.Ordinal)))
                {
                    ImGui.SameLine();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                    {
                        using var font = ImRaii.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Macro Chain is not installed");
                }
            }

            DrawOption(
                "启用宏锁定(/mlock)",
                "在每个宏的开头添加一行 /mlock ，" +
                "以阻止制作宏被其他宏打断。",
                Config.MacroCopy.UseMacroLock,
                v => Config.MacroCopy.UseMacroLock = v,
                ref isDirty
            );

            DrawOption(
                "启用完成提醒",
                "在每个宏的最后添加一行 /echo ，以提醒你这个宏已经执行完毕。",
                Config.MacroCopy.AddNotification,
                v => Config.MacroCopy.AddNotification = v,
                ref isDirty
            );

            if (Config.MacroCopy.AddNotification)
            {
                if ((Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro || !Config.MacroCopy.CombineMacro) && Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                {
                    DrawOption(
                        "强制提醒",
                        "总是在每个宏的末尾添加完成提醒，无论这个宏是否可以正好执行完剩余工序。" +
                        "关闭此选项可以避免出现末尾宏只执行一个技能的情况。",
                        Config.MacroCopy.ForceNotification,
                        v => Config.MacroCopy.ForceNotification = v,
                        ref isDirty
                    );
                }

                DrawOption(
                    "启用提示音",
                    "允许完成提醒播放提示音效。",
                    Config.MacroCopy.AddNotificationSound,
                    v => Config.MacroCopy.AddNotificationSound = v,
                    ref isDirty
                );

                if (Config.MacroCopy.AddNotificationSound)
                {
                    if (!Config.MacroCopy.UseNextMacro && Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                    {
                        DrawOption(
                            "过渡宏提示音",
                            "自定义过渡宏末尾完成提醒所播报的提示音序号，\n" +
                            "即游戏内的 <se.#> 。",
                            Config.MacroCopy.IntermediateNotificationSound,
                            1, 16,
                            v =>
                            {
                                Config.MacroCopy.IntermediateNotificationSound = v;
                                UIGlobals.PlayChatSoundEffect((uint)v);
                            },
                            ref isDirty
                        );
                    }

                    DrawOption(
                        "最终宏提示音",
                        "自定义最终宏末尾完成提醒所播报的提示音序号，\n" +
                        "即游戏内的 <se.#> 。",
                        Config.MacroCopy.EndNotificationSound,
                        1, 16,
                        v =>
                        {
                            Config.MacroCopy.EndNotificationSound = v;
                            UIGlobals.PlayChatSoundEffect((uint)v);
                        },
                        ref isDirty
                    );
                }
            }

            if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "移除等待时间",
                    "移除宏中每个技能行最后的 <wait.#> 。",
                    Config.MacroCopy.RemoveWaitTimes,
                    v => Config.MacroCopy.RemoveWaitTimes = v,
                    ref isDirty
                );

                if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                {
                    DrawOption(
                        "不进行宏拆分",
                        "一般情况下由于游戏内用户宏有15行的限制，手法工序过多时需要拆分为多个宏。" +
                        "如果打开这个选项，插件就不会再自动拆分，而是把所有技能放在一个宏里，不管它会有多少行。",
                        Config.MacroCopy.CombineMacro,
                        v => Config.MacroCopy.CombineMacro = v,
                        ref isDirty
                    );
                }
            }
        }

        if (isDirty)
            Config.Save();
    }

    private static void DrawSolverConfig(ref SolverConfig configRef, SolverConfig defaultConfig, bool disableOptimal, out bool isDirty)
    {
        isDirty = false;

        var config = configRef;

        using (var panel = ImRaii2.GroupPanel("通用", -1, out _))
        {
            if (ImGui.Button("恢复初始设置", OptionButtonSize))
            {
                config = defaultConfig;
                isDirty = true;
            }

            DrawOption(
                "算法",
                "决定在求解宏时要采用的算法。The algorithm to use when solving for a macro. Different " +
                "这些算法各有优劣。algorithms provide different pros and cons for using them. " +
                "目前来看，By far, the Optimal and Stepwise Genetic algorithms provide " +
                "尤其是在处理高难度的制作时。the best results, especially for very difficult crafts.",
                GetAlgorithmName,
                GetAlgorithmTooltip,
                config.Algorithm,
                v => config = config with { Algorithm = v },
                ref isDirty,
                disableOptimal ? [SolverAlgorithm.Raphael] : []
            );

            using (ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseGenetic or SolverAlgorithm.Raphael)))
                DrawOption(
                    "最大核心数",
                    "求解时使用的核心数量。你应尽可能多地使用可用核心。" +
                    $"如果设置过高，可能会影响你的游戏体验。" +
                    $"一个较好的估计是比你的系统核心数少 1 到 2 个（提示：你有 {Environment.ProcessorCount} 个核心），" +
                    $"但请确保为后台任务预留足够的核心（如果有的话）。\n" +
                    "（只在分支、遗传和最优算法中使用）",
                    config.MaxThreadCount,
                    1,
                    Environment.ProcessorCount,
                    v => config = config with { MaxThreadCount = v },
                    ref isDirty
                );

            if (config.Algorithm != SolverAlgorithm.Raphael)
            {
                DrawOption(
                    "目标迭代次数",
                    "每个制作步骤要运行的总迭代次数。" +
                    "较高的数值需要更多的计算能力。" +
                    "较高的数值也可能降低结果的波动性，因此可以根据需要调整其他参数，以获得更理想的结果。",
                    config.Iterations,
                    1000,
                    1000000,
                    v => config = config with { Iterations = v },
                    ref isDirty
                );

                DrawOption(
                    "最大迭代次数",
                    "当制作难度足够高且求解器尚未找到任何完成方式时，" +
                    "求解器可能会超过目标迭代次数。在少数情况下，" +
                    "求解器可能会持续运行非常长的时间。设置该最大值是为了防止求解器占用你所有的内存。",
                    config.MaxIterations,
                    config.Iterations,
                    5000000,
                    v => config = config with { MaxIterations = v },
                    ref isDirty
                );

                DrawOption(
                    "最大步骤数",
                    "制作步骤的最大数量；这通常是你唯一需要调整的设置。" +
                    "它应该比你预计的步骤数多大约 5 步。如果该值过低，" +
                    "求解器在每次迭代中学到的内容会很少；如果过高，" +
                    "则会在无用的额外步骤上浪费时间。",
                    config.MaxStepCount,
                    1,
                    100,
                    v => config = config with { MaxStepCount = v },
                    ref isDirty
                );

                DrawOption(
                    "探索常数",
                    "决定求解器探索新的、可能更佳路径频率的常数。" +
                    "如果该值设置得过高，求解动作将大多随机决定。",
                    config.ExplorationConstant,
                    0,
                    10,
                    v => config = config with { ExplorationConstant = v },
                    ref isDirty
                );

                DrawOption(
                    "评分权重常数",
                    "一个范围在 0 到 1 之间的常数，用于配置求解器如何评分并选择下一步路径。" +
                    "值为 0 时，将根据其平均结果选择；" +
                    "值为 1 时，则使用迄今为止取得的最佳结果选择。",
                    config.MaxScoreWeightingConstant,
                    0,
                    1,
                    v => config = config with { MaxScoreWeightingConstant = v },
                    ref isDirty
                );

                using (ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseGenetic)))
                    DrawOption(
                        "分支数量",
                        "将迭代次数分配到不同的求解器上。" +
                        "通常，你应该将此值至少增加到系统核心数" +
                        $"（提示：你有 {Environment.ProcessorCount} 个核心），" +
                        "以获得最大的加速效果。" +
                        "数量越高，找到更好局部最大值的机会就越大；" +
                        "这一概念与探索常数类似，但不完全相同。\n" +
                        "（只在分支和遗传算法中使用）",
                        config.ForkCount,
                        1,
                        500,
                        v => config = config with { ForkCount = v },
                        ref isDirty
                    );

                using (ImRaii.Disabled(config.Algorithm is not SolverAlgorithm.StepwiseGenetic))
                    DrawOption(
                        "精英动作数量",
                        "在每个制作步骤中，选择此处指定的数量的最佳方案，并将它们作为下一制作步骤的输入。" +
                        "为了获得最佳效果，可设置为分支数量的一半，如有需要可再加 1 到 2。\n" +
                        "（只在逐步遗传算法中使用）",
                        config.FurcatedActionCount,
                        1,
                        500,
                        v => config = config with { FurcatedActionCount = v },
                        ref isDirty
                    );
            }
            else
            {
                DrawOption(
                    "快速求解",
                    "加快求解时间。" +
                    "将所有作业类技能推迟到循环的末尾。",
                    config.BackloadProgress,
                    v => config = config with { BackloadProgress = v },
                    ref isDirty
                );
                DrawOption(
                    "确保可靠性",
                    "找到一种循环策略，" +
                    "无论随机条件多么不利，都能达到目标品质。",
                    config.Adversarial,
                    v => config = config with { Adversarial = v },
                    ref isDirty
                );

                if (config.Adversarial)
                {
                    ImGui.SameLine();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                    {
                        using var font = ImRaii.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.TooltipWrapped("启用“确保可靠性”会使用更多内存，并可能显著增加求解时间。");
                }
            }
        }

        using (var panel = ImRaii2.GroupPanel("技能池", -1, out var poolWidth))
        {
            poolWidth -= ImGui.GetStyle().ItemSpacing.X * 2;

            ImGui.TextUnformatted("点击下方的技能图标以决定是否允许求解器使用它们。");

            var pool = config.ActionPool;
            DrawActionPool(ref pool, poolWidth, out var isPoolDirty);
            if (isPoolDirty)
            {
                config = config with { ActionPool = pool };
                isDirty = true;
            }
        }

        if (config.Algorithm != SolverAlgorithm.Raphael)
        {
            using (var panel = ImRaii2.GroupPanel("高级", -1, out _))
            {
                DrawOption(
                    "最大展开步骤数",
                    "每次迭代可考虑的制作步骤的最大数量。" +
                    "降低此值可能会产生意想不到的副作用。" +
                    "请仅在你完全清楚自己在做什么时才更改此值。",
                    config.MaxRolloutStepCount,
                    1,
                    50,
                    v => config = config with { MaxRolloutStepCount = v },
                    ref isDirty
                );

                DrawOption(
                    "严格技能",
                    "在寻找下一步可执行技能时，" +
                    "使用启发式方法限制尝试的技能范围。" +
                    "这样可以生成更优秀的宏，但代价是可能无法找到极具创造性的宏。",
                    config.StrictActions,
                    v => config = config with { StrictActions = v },
                    ref isDirty
                );
            }
        }

        if (config.Algorithm != SolverAlgorithm.Raphael)
        {
            using (var panel = ImRaii2.GroupPanel("权重分数 (高级)", -1, out _))
            {
                DrawOption(
                    "进展",
                    "决定你要为推进配方进展分配多少权重分数。",
                    config.ScoreProgress,
                    0,
                    100,
                    v => config = config with { ScoreProgress = v },
                    ref isDirty
                );

                DrawOption(
                    "品质",
                    "决定你要为提高配方品质分配多少权重分数。",
                    config.ScoreQuality,
                    0,
                    100,
                    v => config = config with { ScoreQuality = v },
                    ref isDirty
                );

                DrawOption(
                    "耐久",
                    "决定你要为剩余耐久分配多少权重分数。",
                    config.ScoreDurability,
                    0,
                    100,
                    v => config = config with { ScoreDurability = v },
                    ref isDirty
                );

                DrawOption(
                    "制作力",
                    "决定你要为剩余制作力分配多少权重分数。",
                    config.ScoreCP,
                    0,
                    100,
                    v => config = config with { ScoreCP = v },
                    ref isDirty
                );

                DrawOption(
                    "步数",
                    "决定你要为工序步数分配多少权重分数。" +
                    "步数越少，权重分越高。",
                    config.ScoreSteps,
                    0,
                    100,
                    v => config = config with { ScoreSteps = v },
                    ref isDirty
                );
            }
        }

        if (isDirty)
            configRef = config;
    }

    private static void DrawActionPool(ref ActionType[] actionPool, float poolWidth, out bool isDirty)
    {
        isDirty = false;

        var recipeData = Service.Plugin.GetDefaultStats().Recipe;
        HashSet<ActionType> pool = [.. actionPool];

        var imageSize = ImGui.GetFrameHeight() * 2;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        using var _alpha = ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, ImGui.GetStyle().DisabledAlpha * .5f);
        foreach (var category in Enum.GetValues<ActionCategory>())
        {
            if (category == ActionCategory.Combo)
                continue;

            var actions = category.GetActions();
            using var panel = ImRaii2.GroupPanel(category.GetDisplayName(), poolWidth, out var availSpace);
            var itemsPerRow = (int)MathF.Floor((availSpace + spacing) / (imageSize + spacing));
            var itemCount = actions.Count;
            var iterCount = (int)(Math.Ceiling((float)itemCount / itemsPerRow) * itemsPerRow);
            for (var i = 0; i < iterCount; i++)
            {
                if (i % itemsPerRow != 0)
                    ImGui.SameLine(0, spacing);
                if (i < itemCount)
                {
                    var actionBase = actions[i].Base();
                    var isEnabled = pool.Contains(actions[i]);
                    var isInefficient = SolverConfig.InefficientActions.Contains(actions[i]);
                    var isRisky = SolverConfig.RiskyActions.Contains(actions[i]);
                    var iconTint = Vector4.One;
                    if (!isEnabled)
                        iconTint = new(1, 1, 1, ImGui.GetStyle().DisabledAlpha);
                    else if (isInefficient)
                        iconTint = new(1, 1f, .5f, 1);
                    else if (isRisky)
                        iconTint = new(1, .5f, .5f, 1);
                    if (ImGui.ImageButton(actions[i].GetIcon(recipeData.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, iconTint))
                    {
                        isDirty = true;
                        if (isEnabled)
                            pool.Remove(actions[i]);
                        else
                            pool.Add(actions[i]);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        var s = new StringBuilder();
                        s.AppendLine(actions[i].GetName(recipeData.ClassJob));
                        if (isInefficient)
                            s.AppendLine(
                                "不推荐。此技能可能会以对整个制作不利的方式被随机使用。" +
                                "如果启用此技能，请始终使用你的最佳判断。");
                        if (isRisky)
                            s.AppendLine(
                                "无用；求解器目前在制作期间不会冒任何风险。" +
                                "它只会执行成功率为 100% 的步骤。" +
                                "如果你想在制作中尝试冒险（例如高难度配方），" +
                                "就不要在那段时间依赖求解器。");
                        ImGuiUtils.TooltipWrapped(s.ToString());
                    }
                }
                else
                    ImGui.Dummy(new(imageSize));
            }
        }

        if (isDirty)
        {
            bool InPool(BaseComboAction action)
            {
                if (action.ActionTypeA.Base() is BaseComboAction { } aCombo)
                {
                    if (!InPool(aCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeA))
                        return false;
                }
                if (action.ActionTypeB.Base() is BaseComboAction { } bCombo)
                {
                    if (!InPool(bCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeB))
                        return false;
                }
                return true;
            }

            foreach (var combo in ActionCategory.Combo.GetActions())
            {
                if (combo.Base() is BaseComboAction { } comboAction)
                {
                    if (!InPool(comboAction))
                        pool.Remove(combo);
                    else
                        pool.Add(combo);
                }
            }
            actionPool = [.. pool];
        }
    }

    private void DrawTabRecipeNote()
    {
        using var tab = TabItem("制作笔记");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "固定助手窗口",
            "默认情况下助手窗口会固定在你制作笔记面板的右侧。" +
            "取消勾选此项后，你可以将它拖动到其他位置。",
            Config.PinRecipeNoteToWindow,
            v => Config.PinRecipeNoteToWindow = v,
            ref isDirty
        );

        DrawOption(
            "默认折叠助手窗口",
            "启用此选项时会在你开始制作时自动折叠助手窗口，" +
            "同时也会阻止求解器的自动运行。",
            Config.CollapseSynthHelper,
            v => Config.CollapseSynthHelper = v,
            ref isDirty
        );

        DrawOption(
            "自动生成推荐宏",
            "（可能导致掉帧！）" +
            "在浏览新配方或更换装备属性时，自动生成推荐宏" +
            "（相当于在宏编辑器中点击“生成”）。" +
            "在某些电脑或低等级配方下，这可能会导致严重掉帧。" +
            "关闭此选项则会提供一个“生成”按钮，让你仅在需要时获取推荐宏。",
            Config.SuggestMacroAutomatically,
            v => Config.SuggestMacroAutomatically = v,
            ref isDirty
        );

        DrawOption(
            "启用社区宏",
            "从 FFXIV Teamcraft 的社区中为你的制作寻找最佳的宏。" +
            "这将会向它们的服务器发送请求，获取匹配你目标配方等级的宏。" +
            "这种请求每种配方等级只会发送一次，" +
            "并且总是会在本地缓存，以降低服务器负载。",
            Config.ShowCommunityMacros,
            v => Config.ShowCommunityMacros = v,
            ref isDirty
        );

        if (Config.ShowCommunityMacros)
        {
            DrawOption(
                "自动检索社区宏Automatically Search for Community Macro",
                "在你点击一个新的配方或是改变装备属性时自动检索社区宏。" +
                "\n" +
                "这个选项默认关闭，这样不会对它们的服务器造成太大伤害 :)",
                Config.SearchCommunityMacroAutomatically,
                v => Config.SearchCommunityMacroAutomatically = v,
                ref isDirty
            );
        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var solverConfig = Config.RecipeNoteSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.RecipeNoteDefault, false, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.RecipeNoteSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }

    private void DrawTabMacroEditor()
    {
        using var tab = TabItem("宏编辑器");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        var solverConfig = Config.EditorSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.EditorDefault, false, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.EditorSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }

    private void DrawTabSynthHelper()
    {
        using var tab = TabItem("制作助手");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "固定助手窗口",
            "默认情况下助手窗口会固定在你制作窗口的右侧。" +
            "取消勾选此项后，你可以将它拖动到其他位置。",
            Config.PinSynthHelperToWindow,
            v => Config.PinSynthHelperToWindow = v,
            ref isDirty
        );

        DrawOption(
            "执行宏时自动禁用",
            "在你执行游戏内的用户宏时禁用制作助手。",
            Config.DisableSynthHelperOnMacro,
            v => Config.DisableSynthHelperOnMacro = v,
            ref isDirty
        );

        DrawOption(
            "仅模拟第一步",
            "默认情况下仅模拟第一步。" +
            "你仍然可以将鼠标悬停在其他步骤上查看结果，" +
            "但可靠性试验（在悬停宏统计信息时显示）将被隐藏。",
            Config.SynthHelperDisplayOnlyFirstStep,
            v => Config.SynthHelperDisplayOnlyFirstStep = v,
            ref isDirty
        );

        DrawOption(
            "绘制技能提示",
            "在你的热键栏上绘制技能提示，就像PvE打连击一样。" +
            "制作助手分析出的下一步最优技能会被高亮显示。" +
            "在这种场景下，原先应被高亮显示的连击技能和状态触发技能就不会被高亮显示了。",
            Config.SynthHelperAbilityAnts,
            v => Config.SynthHelperAbilityAnts = v,
            ref isDirty
        );

        DrawOption(
            "求解器步骤数",
            "在游戏内制作时，求解器要考虑的最少未来步骤数。" +
            "如果不会对你造成额外成本，求解器仍可能给出超过此数量的步骤。",
            Config.SynthHelperStepCount,
            1,
            100,
            v => Config.SynthHelperStepCount = v,
            ref isDirty
        );

        DrawOption(
            "最大显示步骤数",
            "设置一个阈值，让制作助手不要显示再往后的步骤，" +
            "以减少界面杂乱。",
            Config.SynthHelperMaxDisplayCount,
            Config.SynthHelperStepCount,
            100,
            v => Config.SynthHelperMaxDisplayCount = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var solverConfig = Config.SynthHelperSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.SynthHelperDefault, true, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.SynthHelperSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }

    private void DrawTabAbout()
    {
        using var tab = TabItem("About");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var plugin = Service.Plugin;
        var icon = plugin.Icon;
        var iconDim = new Vector2(128) * ImGuiHelpers.GlobalScale;

        using (var table = ImRaii.Table("settingsAboutTable", 2))
        {
            if (table)
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, iconDim.X);

                ImGui.TableNextColumn();
                ImGui.Image(icon.Handle, iconDim);

                ImGui.TableNextColumn();
                ImGuiUtils.AlignMiddle(new(float.PositiveInfinity, HeaderFont.GetFontSize() + SubheaderFont.GetFontSize() + ImGui.GetFontSize() * 3 + ImGui.GetStyle().ItemSpacing.Y * 4), new(0, iconDim.Y));

                using (HeaderFont.Push())
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize("CraftimizerCN").X);
                    ImGuiUtils.Hyperlink("CraftimizerCN", "https://github.com/WorkingRobot/CraftimizerCN", false);
                }

                using (SubheaderFont.Push())
                    ImGuiUtils.TextCentered($"v{plugin.Version} {plugin.BuildConfiguration}");

                ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"原作者： {plugin.Author} (WorkingRobot)").X);
                ImGui.TextUnformatted($"原作者： {plugin.Author} (");
                ImGui.SameLine(0, 0);
                ImGuiUtils.Hyperlink("WorkingRobot", "https://github.com/WorkingRobot");
                ImGui.SameLine(0, 0);
                ImGui.TextUnformatted(")");

                ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"本地化&改造： InfSein").X);
                ImGui.TextUnformatted($"本地化&改造： ");
                ImGui.SameLine(0, 0);
                ImGuiUtils.Hyperlink("InfSein", "https://github.com/InfSein");

                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.07f, 0.76f, 1.00f, 1f)))
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"在 Ko-fi 上赞助原作者!").X);
                    ImGui.TextUnformatted($"在 ");
                    ImGui.SameLine(0, 0);
                    ImGuiUtils.Hyperlink("Ko-fi", Plugin.SupportLink);
                    ImGui.SameLine(0, 0);
                    ImGui.TextUnformatted(" 上赞助原作者!");
                }
            }
        }

        ImGuiHelpers.ScaledDummy(5);

        ImGui.Separator();

        ImGuiHelpers.ScaledDummy(5);

        using (SubheaderFont.Push())
            ImGuiUtils.TextCentered("特别感谢");

        var startPosX = ImGui.GetCursorPosX();

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("alostsock", "https://github.com/alostsock");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for making ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and the original solver algorithm.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("KonaeAkira", "https://github.com/KonaeAkira");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for making ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("raphael-rs", "https://raphael-xiv.com");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and the Optimal algorithm.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("FFXIV Teamcraft", "https://ffxivteamcraft.com");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and its users for their community rotations.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://dke.maastrichtuniversity.nl/m.winands/documents/multithreadedMCTS2.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://liacs.leidenuniv.nl/~plaata1/papers/paper_ICAART18.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", and ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this paper", "https://arxiv.org/abs/2308.04459");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for inspiration and design references.");
    }

    public void Dispose()
    {
        Service.WindowSystem.RemoveWindow(this);
        SubheaderFont?.Dispose();
        HeaderFont?.Dispose();
    }
}
