using CraftimizerCN.Plugin;
using CraftimizerCN.Utils;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;
using System;
using CraftimizerCN.Simulator;
using CraftimizerCN.Simulator.Actions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sim = CraftimizerCN.Simulator.SimulatorNoRandom;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace CraftimizerCN.Windows;

public sealed class MacroList : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    public CharacterStats? CharacterStats { get; private set; }
    public RecipeData? RecipeData { get; private set; }

    private static IReadOnlyList<Macro> Macros => Service.Configuration.Macros;
    private Dictionary<Macro, SimulationState> MacroStateCache { get; } = [];

    public MacroList() : base("CraftimizerCN 本地宏列表", WindowFlags, false)
    {
        RefreshSearch();

        Macro.OnMacroChanged += OnMacroChanged;
        Configuration.OnMacroListChanged += OnMacroListChanged;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        SizeConstraints = new() { MinimumSize = new(465, 520), MaximumSize = new(float.PositiveInfinity) };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => Service.Plugin.OpenSettingsTab("通用"),
                ShowTooltip = () => ImGuiUtils.Tooltip("打开设置")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("赞助原作者")
            }
        ];

        Service.WindowSystem.AddWindow(this);
    }

    public override bool DrawConditions()
    {
        return Service.Objects.LocalPlayer != null;
    }

    public override void PreDraw()
    {
        var oldCharacterStats = CharacterStats;
        var oldRecipeData = RecipeData;

        (CharacterStats, RecipeData, _) = Service.Plugin.GetDefaultStats();

        if (oldCharacterStats != CharacterStats || oldRecipeData != RecipeData)
            RecalculateStats();
    }

    public override void Draw()
    {
        DrawSearchBar();
        using var group = ImRaii.Child("macros", new(-1, -1));
        if (sortedMacros.Count > 0)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var macros = new List<Macro>(sortedMacros);
            for (var i = 0; i < macros.Count; ++i)
            {
                var pos = ImGui.GetCursorPos();
                DrawMacro(macros[i]);
                ImGui.SetCursorPos(pos);
                ImGui.InvisibleButton($"###macroButton{i}", ImGui.GetItemRectSize());
                if (isUnsorted)
                {
                    using (var _source = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroListItem", i);
                            DrawMacro(macros[i], width);
                        }
                    }
                    using (var _target = ImRaii.DragDropTarget())
                    {
                        if (_target)
                        {
                            if (ImGuiExtras.AcceptDragDropPayload("macroListItem", out int j))
                                Service.Configuration.MoveMacro(j, i);
                        }
                    }
                }
            }
        }
        else
        {
            var text1 = "还没有保存过任何本地宏！";
            var text2 = "在宏编辑器和制作笔记助手处可以将手法保存到本地。";
            var text3 = "打开制作笔记";
            var text4 = "打开宏编辑器";
            var buttonRowWidth = ImGui.CalcTextSize(text3).X + ImGui.CalcTextSize(text4).X + ImGui.GetStyle().ItemSpacing.X * 5;
            var size = new Vector2(
                Math.Max(
                    Math.Max(ImGui.CalcTextSize(text1).X, ImGui.CalcTextSize(text2).X),
                    buttonRowWidth
                ),
                ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetFrameHeight()
            );
            ImGuiUtils.AlignMiddle(size);
            using var child = ImRaii.Child("##macroMessage", size);
            ImGuiUtils.TextCentered(text1);
            ImGuiUtils.TextCentered(text2);
            ImGuiUtils.AlignCentered(buttonRowWidth);
            if (ImGui.Button(text3))
                Plugin.Plugin.OpenCraftingLog();
            ImGui.SameLine();
            if (ImGui.Button(text4))
                OpenEditor(null);
        }
    }

    private string searchText = string.Empty;
    private List<Macro> sortedMacros = null!;
    private bool isUnsorted = true;
    private void DrawSearchBar()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonSize = ImGui.GetFrameHeight();
        var width = Math.Max(1f, ImGui.GetContentRegionAvail().X - buttonSize - spacing);

        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##search", "搜索", ref searchText, 100))
            RefreshSearch();

        ImGui.SameLine(0, spacing);
        if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.FileImport))
            ShowBatchImportPopup();
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("批量导入");

        DrawBatchImportPopup();
    }

    private void DrawMacro(Macro macro, float width = -1)
    {
        width = width < 0 ? ImGui.GetContentRegionAvail().X : width;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (macro.Actions.Any(a => a.Category() == ActionCategory.Combo))
            throw new InvalidOperationException("Combo actions should be sanitized away");

        var stateNullable = GetMacroState(macro);

        using var panel = ImRaii2.GroupPanel(macro.Name, width - ImGui.GetStyle().ItemSpacing.X * 2, out var availWidth);
        var stepsAvailWidthOffset = width - availWidth;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var miniRowHeight = (windowHeight - spacing) / 2f;

        using var table = ImRaii.Table("table", stateNullable.HasValue ? 3 : 2, ImGuiTableFlags.BordersInnerV);
        if (table)
        {
            if (stateNullable.HasValue)
                ImGui.TableSetupColumn("stats", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("steps", ImGuiTableColumnFlags.WidthStretch, 0);

            ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
            if (stateNullable is { } state)
            {
                ImGui.TableNextColumn();
                if (Service.Configuration.ShowOptimalMacroStat)
                {
                    var progressHeight = windowHeight;
                    if (state.Progress >= state.Input.Recipe.MaxProgress && state.Input.Recipe.MaxQuality > 0)
                    {
                        ImGuiUtils.ArcProgress(
                        (float)state.Quality / state.Input.Recipe.MaxQuality,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"品质: {state.Quality} / {state.Input.Recipe.MaxQuality}");
                    }
                    else
                    {
                        ImGuiUtils.ArcProgress(
                        (float)state.Progress / state.Input.Recipe.MaxProgress,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"进展: {state.Progress} / {state.Input.Recipe.MaxProgress}");
                    }
                }
                else
                {
                    ImGuiUtils.ArcProgress(
                        (float)state.Progress / state.Input.Recipe.MaxProgress,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"进展: {state.Progress} / {state.Input.Recipe.MaxProgress}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)state.Quality / state.Input.Recipe.MaxQuality,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"品质: {state.Quality} / {state.Input.Recipe.MaxQuality}");

                    ImGuiUtils.ArcProgress((float)state.Durability / state.Input.Recipe.MaxDurability,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Durability));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"剩余耐久: {state.Durability} / {state.Input.Recipe.MaxDurability}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)state.CP / state.Input.Stats.CP,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.CP));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"剩余CP: {state.CP} / {state.Input.Stats.CP}");
                }
            }

            ImGui.TableNextColumn();
            {
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, miniRowHeight))
                    OpenEditor(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("在宏编辑器中打开");
                ImGui.SameLine(0, spacing);
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.PencilAlt, miniRowHeight))
                    ShowRenamePopup(macro);
                DrawRenamePopup(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("重命名");
                ImGui.SameLine(0, spacing);
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.ShareAlt, miniRowHeight))
                    ShowSharePopup(macro);
                DrawSharePopup(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("分享");

                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste, miniRowHeight))
                    MacroCopy.Copy(macro.Actions);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("复制到剪贴板");
                ImGui.SameLine(0, spacing);
                using (var _disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift))
                {
                    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Trash, miniRowHeight))
                        Service.Configuration.RemoveMacro(macro);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGuiUtils.Tooltip("删除 (按住Shift点击此按钮以确认)");
            }

            ImGui.TableNextColumn();
            {
                var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing * 2) / (miniRowHeight + spacing));
                var itemCount = macro.Actions.Count;
                for (var i = 0; i < itemsPerRow * 2; i++)
                {
                    if (i % itemsPerRow != 0)
                        ImGui.SameLine(0, spacing);
                    if (i < itemCount)
                    {
                        var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                        if (!shouldShowMore)
                        {
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip(macro.Actions[i].GetName(RecipeData!.ClassJob));
                        }
                        else
                        {
                            var amtMore = itemCount - itemsPerRow * 2;
                            var pos = ImGui.GetCursorPos();
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip($"{macro.Actions[i].GetName(RecipeData!.ClassJob)}\n以及另外 {amtMore} 步");
                            ImGui.SetCursorPos(pos);
                            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), ImGui.GetColorU32(ImGuiCol.FrameBg), miniRowHeight / 8f);
                            ImGui.GetWindowDrawList().AddTextClippedEx(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), $"+{amtMore}", null, new(.5f), null);
                        }
                    }
                    else
                        ImGui.Dummy(new(miniRowHeight));
                }
            }
        }
    }
    private string popupBatchImportText = string.Empty;
    private string popupBatchImportError = string.Empty;

    private void ShowBatchImportPopup()
    {
        ImGui.OpenPopup("##batchImportPopup");
        popupBatchImportText = string.Empty;
        popupBatchImportError = string.Empty;
    }

    private void DrawBatchImportPopup()
    {
        const string Example = "1v6bZCFER 自定宏1\n1v6biZCF";

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSizeConstraints(new(500, 0), new(float.PositiveInfinity));
        using var popup = ImRaii.Popup("##batchImportPopup", ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove);
        if (!popup)
            return;

        ImGui.TextUnformatted("批量导入");
        ImGui.TextWrapped("一行一个。");
        ImGui.TextWrapped("格式：CAC工序码 宏名称(可选)。");
        ImGui.Dummy(default);

        var availWidth = ImGui.GetContentRegionAvail().X;
        using (var mono = ImRaii.PushFont(UiBuilder.MonoFont))
            ImGuiUtils.InputTextMultilineWithHint("##batchImportText", Example, ref popupBatchImportText, 8192, new(availWidth, ImGui.GetTextLineHeight() * 10 + ImGui.GetStyle().FramePadding.Y), ImGuiInputTextFlags.AutoSelectAll);

        if (!string.IsNullOrWhiteSpace(popupBatchImportError))
        {
            using var c = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(popupBatchImportError);
        }

        var halfWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2f;
        if (ImGui.Button("导入", new(halfWidth, 0)))
            TryImportBatchMacros();
        ImGui.SameLine();
        if (ImGui.Button("取消", new(halfWidth, 0)))
            ImGui.CloseCurrentPopup();
    }

    private void TryImportBatchMacros()
    {
        if (!MacroImport.TryParseBatchCacImports(popupBatchImportText, out var imports, out var error))
        {
            popupBatchImportError = error;
            return;
        }

        var existingNames = MacroNaming.CreateExistingNameSet(Service.Configuration.Macros.Select(m => m.Name));
        var macros = new List<Macro>(imports.Count);
        foreach (var imported in imports)
        {
            var name = string.IsNullOrWhiteSpace(imported.Name)
                ? MacroNaming.GenerateDefaultMacroName(existingNames)
                : imported.Name!.Trim();

            existingNames.Add(name);
            macros.Add(new Macro
            {
                Name = name,
                Actions = [.. imported.Actions]
            });
        }

        foreach (var macro in macros)
            Service.Configuration.AddMacro(macro);

        Plugin.Plugin.DisplayNotification(new()
        {
            Content = $"成功导入了 {macros.Count} 个宏",
            MinimizedText = $"导入了 {macros.Count} 个宏",
            Title = "成功导入",
            Type = NotificationType.Success
        });

        ImGui.CloseCurrentPopup();
    }
    private string popupMacroName = string.Empty;
    private Macro? popupMacro;
    private void ShowRenamePopup(Macro macro)
    {
        ImGui.OpenPopup($"##renamePopup-{macro.GetHashCode()}");
        popupMacro = macro;
        popupMacroName = macro.Name;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawRenamePopup(Macro macro)
    {
        using var popup = ImRaii.Popup($"##renamePopup-{macro.GetHashCode()}");
        if (popup)
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(ImGui.CalcItemWidth());
            if (ImGui.InputTextWithHint($"##setName", "Name", ref popupMacroName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(popupMacroName))
                {
                    popupMacro!.Name = popupMacroName;
                    Service.Configuration.Save();
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private string popupShareCode = string.Empty;
    private string popupShareError = string.Empty;
    private Macro? popupShareMacro;
    private void ShowSharePopup(Macro macro)
    {
        ImGui.OpenPopup($"##sharePopup-{macro.GetHashCode()}");
        popupShareMacro = macro;
        popupShareError = string.Empty;
        if (!CacHelper.TryEncodeActions(macro.Actions, out popupShareCode, out popupShareError))
            popupShareCode = string.Empty;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawSharePopup(Macro macro)
    {
        using var popup = ImRaii.Popup($"##sharePopup-{macro.GetHashCode()}");
        if (!popup)
            return;

        if (popupShareMacro != macro)
        {
            popupShareMacro = macro;
            popupShareError = string.Empty;
            if (!CacHelper.TryEncodeActions(macro.Actions, out popupShareCode, out popupShareError))
                popupShareCode = string.Empty;
        }

        var width = ImGui.CalcTextSize("复制CAC分享链接").X + ImGui.GetStyle().FramePadding.X * 2;
        var shareUrl = string.IsNullOrWhiteSpace(popupShareCode) ? string.Empty : $"https://cac.nbb.fan/?s={popupShareCode}";

        using (var _disabled = ImRaii.Disabled(!string.IsNullOrEmpty(popupShareError) || string.IsNullOrEmpty(popupShareCode)))
        {
            if (ImGui.Button("复制CAC工序码", new(width, 0)))
            {
                ImGui.SetClipboardText(popupShareCode);
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.Button("复制CAC分享链接", new(width, 0)))
            {
                ImGui.SetClipboardText(shareUrl);
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.Button("打开CAC分享链接", new(width, 0)))
            {
                Util.OpenLink(shareUrl);
                ImGui.CloseCurrentPopup();
            }
        }

        if (!string.IsNullOrWhiteSpace(popupShareError))
        {
            using (var c = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                ImGui.TextWrapped(popupShareError);
        }
    }

    private void RecalculateStats()
    {
        MacroStateCache.Clear();
    }

    private void RefreshSearch()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            sortedMacros = [.. Macros];
            isUnsorted = true;
            return;
        }
        isUnsorted = false;
        var matcher = new FuzzyMatcher(searchText.ToLowerInvariant(), MatchMode.FuzzyParts);
        var query = Macros.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.Name.ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Item);
        sortedMacros = [.. query];
    }

    private static void OpenEditor(Macro? macro)
    {
        var stats = Service.Plugin.GetDefaultStats();
        Service.Plugin.OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, macro?.Actions ?? Enumerable.Empty<ActionType>(), macro != null ? (actions => { macro.ActionEnumerable = actions; Service.Configuration.Save(); }) : null);
    }

    private void OnMacroChanged(Macro macro)
    {
        MacroStateCache.Remove(macro);
    }

    private void OnMacroListChanged()
    {
        RefreshSearch();
    }

    private SimulationState? GetMacroState(Macro macro)
    {
        if (CharacterStats == null || RecipeData == null)
            return null;

        if (MacroStateCache.TryGetValue(macro, out var state))
            return state;

        state = new SimulationState(new(CharacterStats, RecipeData.RecipeInfo));
        var sim = new Sim();
        (_, state, _) = sim.ExecuteMultiple(state, macro.Actions);
        return MacroStateCache[macro] = state;
    }

    public void Dispose()
    {
        Macro.OnMacroChanged -= OnMacroChanged;
        Configuration.OnMacroListChanged -= OnMacroListChanged;

        Service.WindowSystem.RemoveWindow(this);
    }
}
