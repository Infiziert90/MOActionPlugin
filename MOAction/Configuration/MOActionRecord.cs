namespace MOAction.Configuration;

using FFXIVClientStructs.FFXIV.Client.Game;

/// <summary>
/// POJO containing the data required from a MoAction
/// </summary>
public class MoActionRecord(uint rowId, ActionType actionType, string name, bool targetArea, string classJobCategory)
{
    public ActionType ActionType { get; private set; } = actionType;
    public uint RowId { get; private set; } = rowId;
    public string Name { get; private set; } = name;
    public bool TargetArea { get; private set; } = targetArea;
    public string ClassJobCategory {get; private set; } =  classJobCategory;

    public MoActionRecord(Lumina.Excel.Sheets.Action action) : this(action.RowId, ActionType.Action, action.Name.ExtractText(), action.TargetArea, action.ClassJobCategory.Value.Name.ExtractText())
    {
    }

    public MoActionRecord() : this(0, ActionType.None, "Default", false,"ADV")
    {
    }

}