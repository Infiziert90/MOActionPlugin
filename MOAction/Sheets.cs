using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace MOAction;

public static class Sheets
{
    public static readonly ExcelSheet<Action> ActionSheet;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet;

    static Sheets()
    {
        ActionSheet = Plugin.DataManager.GetExcelSheet<Action>();
        ClassJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
    }
}