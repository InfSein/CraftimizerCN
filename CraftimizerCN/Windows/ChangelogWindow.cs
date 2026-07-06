using CraftimizerCN.Plugin;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;

namespace CraftimizerCN.Windows;

public sealed class ChangelogWindow : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoCollapse;

    private sealed record Section(string Header, string[] Entries);
    private sealed record Release(string Version, string Title, Section[] Sections);

    private static readonly Release[] Releases =
    [
        // Add new releases at the top
        new("2.11.0.2", "合并上游更新",
        [
            new("功能新增/调整",
            [
                "新增“下一步（Next Action）”求解器，制作助手现已默认使用该求解器。它不再每次都求解整个宏，而是将全部计算资源集中用于找出当前最佳的下一步动作。",
                "“下一步”求解器新增“时间限制”设置，无论你的电脑性能快慢，都能在设定时间内给出建议。",
                "新增“品质目标 (%)”设置：以配方最大品质的一定百分比作为目标，达到后停止，而不是始终追求满品质。",
                "新增“将品质限制为最高收藏价值门槛”设置：制作收藏品时，求解器会在达到最高收藏价值后停止，而不会继续浪费额外步骤提升不再需要的品质。",
                "求解器整体速度得到提升；对于核心数较少的电脑，“下一步”求解器会先快速评估所有可选动作，再将计算时间集中用于最有希望的候选动作（可在高级设置中调整）。",
                "移除了旧的“评分权重”设置，因为评分机制重构后，它们已不再发挥任何作用。"
            ]),
            new("问题修复",
            [
                "修复了一个线程问题：分叉求解器和遗传求解器会共享同一个随机数生成器，从而导致一些烦人的低概率崩溃。",
                "求解器不再为了耗尽剩余耐久或CP而在制作结束后追加无意义的动作。现在会优先完成制作，然后尽可能提高品质，最后使用尽可能少的步骤完成整个流程。",
                "“将品质限制为最高收藏品门槛”不再对宇宙探索收藏品生效。这类制作在达到最高价值门槛后仍可获得额外奖励，因此求解器现在会继续追求最高品质。",
            ])
        ]),
    ];

    private static string LatestVersion => Releases[0].Version;

    public ChangelogWindow() : base("CraftimizerCN 更新日志", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        IsOpen = false;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(500, 400),
            MaximumSize = new(800, 1400),
        };
    }

    // Opens the window once if a newer changelog exists than the user last saw. Call on startup.
    public void OpenIfUpdated()
    {
        if (string.Equals(Service.Configuration.LastSeenChangelogVersion, LatestVersion, StringComparison.Ordinal))
            return;

        Service.Configuration.LastSeenChangelogVersion = LatestVersion;
        Service.Configuration.Save();

        Open();
    }

    public void Open() => IsOpen = true;

    public override void Draw()
    {
        for (var i = 0; i < Releases.Length; ++i)
        {
            var release = Releases[i];

            var flags = i == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (ImGui.CollapsingHeader($"{release.Version}\t\t{release.Title}###release_{release.Version}", flags))
                DrawRelease(release);
        }
    }

    private static void DrawRelease(Release release)
    {
        ImGui.Indent();
        foreach (var section in release.Sections)
        {
            ImGui.Spacing();
            using (ImRaii.PushFont(UiBuilder.DefaultFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                ImGui.TextUnformatted(section.Header);
            foreach (var line in section.Entries)
                DrawBullet(line);
        }
        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawBullet(string text)
    {
        ImGui.TextUnformatted("•");
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    public void Dispose() =>
        Service.WindowSystem.RemoveWindow(this);
}
