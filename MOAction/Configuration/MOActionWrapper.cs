namespace MOAction.Configuration;
using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

public class MOActionWrapper
{
    public Lumina.Excel.Sheets.Action? Action { get; private set; } = null;
    public Lumina.Excel.Sheets.GeneralAction? GeneralAction { get; private set; } = null;

    public ActionType actionType { get; private set; }

    public MOActionWrapper(Lumina.Excel.Sheets.Action action)
    {
        this.Action = action;
        this.actionType = ActionType.Action;
    }

    public MOActionWrapper(Lumina.Excel.Sheets.GeneralAction generalAction)
    {
        this.GeneralAction = generalAction;
        this.actionType = ActionType.GeneralAction;
    }

    public MOActionWrapper()
    {
        this.actionType = ActionType.None;
    }

    public uint RowId()
    {
        if (Action != null)
        {
            return Action!.Value.RowId;
        }
        if (GeneralAction != null)
        {
            return GeneralAction!.Value.RowId;
        }
        return default;
    }

    public bool TargetArea()
    {
        if (Action != null)
        {
            return Action.Value.TargetArea;
        }
        return false;
    }

    public string Name()
    {
        if (Action != null)
        {
            return Action!.Value.Name.ExtractText();
        }
        if (GeneralAction != null)
        {
            return GeneralAction!.Value.Name.ExtractText();
        }
        return string.Empty;
    }

    public string ClassJobCategory()
    {
        if (Action != null)
        {
            return Action!.Value.ClassJobCategory.Value.Name.ExtractText();
        }
        return string.Empty;
    }
}