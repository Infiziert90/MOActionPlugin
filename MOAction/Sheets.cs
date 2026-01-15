using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace MOAction;

public static class Sheets
{
    public static readonly ExcelSheet<Action> ActionSheet;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet;
    public static readonly ClassJobCategory JobsOfMagicAndWarCategory;

    static Sheets()
    {
        ActionSheet = Plugin.DataManager.GetExcelSheet<Action>();
        ClassJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        Plugin.DataManager.GetExcelSheet<ClassJobCategory>().TryGetRow(110, out var jobCategory);
        JobsOfMagicAndWarCategory = jobCategory;
    }
}