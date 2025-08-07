namespace MOAction.Configuration;

using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

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

    public MoActionRecord(Action action) : this(action.RowId, ActionType.Action, action.Name.ExtractText(), action.TargetArea, action.ClassJobCategory.Value.Name.ExtractText())
    {
    }

    public MoActionRecord(GeneralAction generalAction) : this(generalAction.RowId, ActionType.GeneralAction,
        generalAction.Name.ExtractText(), false, "")
    {
    }

    public MoActionRecord() : this(0, ActionType.None, "", false,"")
    {
    }

}