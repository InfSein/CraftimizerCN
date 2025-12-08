using Dalamud.Utility;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace CraftimizerCN.Plugin;

#pragma warning disable IDE1006 // 命名样式

public static class LuminaSheets
{
    private static ExcelModule Module => Service.DataManager.Excel;
    private static Language CurrLanguage
    {
        get
        {
            var lang = Service.DataManager.Language.ToLumina();
            return lang == Language.None ? Language.ChineseSimplified : lang;
        }
    }
    private static Language EnglishLanguage => Language.English;

    public static readonly ExcelSheet<Recipe> RecipeSheet = Module.GetSheet<Recipe>();
    public static readonly ExcelSheet<Action> ActionSheet = Module.GetSheet<Action>();
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = Module.GetSheet<CraftAction>();
    public static readonly ExcelSheet<Status> StatusSheet = Module.GetSheet<Status>();
    public static readonly ExcelSheet<Addon> AddonSheet = Module.GetSheet<Addon>();
    public static readonly ExcelSheet<ClassJob> ClassJobSheet = Module.GetSheet<ClassJob>();
    public static ExcelSheet<Item> ItemSheet => field ??= Module.GetSheet<Item>(CurrLanguage)!;
    private static readonly System.Lazy<ExcelSheet<Item>> _itemSheetEnglish = new(() => {
        var lang = Dalamud.Game.ClientLanguage.English;
        return Service.DataManager.GetExcelSheet<Item>(lang)!;
    });
    public static ExcelSheet<Item> ItemSheetEnglish => _itemSheetEnglish.Value;
    public static readonly ExcelSheet<Level> LevelSheet = Module.GetSheet<Level>();
    public static readonly ExcelSheet<Quest> QuestSheet = Module.GetSheet<Quest>();
    public static readonly ExcelSheet<Materia> MateriaSheet = Module.GetSheet<Materia>();
    public static readonly ExcelSheet<BaseParam> BaseParamSheet = Module.GetSheet<BaseParam>();
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = Module.GetSheet<ItemFood>();
    public static readonly ExcelSheet<WKSMissionToDoEvalutionRefin> WKSMissionToDoEvalutionRefinSheet = Module.GetSheet<WKSMissionToDoEvalutionRefin>();
    public static readonly ExcelSheet<RecipeLevelTable> RecipeLevelTableSheet = Module.GetSheet<RecipeLevelTable>();
    public static readonly ExcelSheet<GathererCrafterLvAdjustTable> GathererCrafterLvAdjustTableSheet = Module.GetSheet<GathererCrafterLvAdjustTable>();
}

#pragma warning restore IDE1006 // 命名样式
