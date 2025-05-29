namespace MOAction.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

/// <summary>
/// Helper wrapper class to be able to act on an Action or GeneralAction in an identical fashion.
/// </summary>
public class MOActionWrapper
{
    private Action? Action { get; }
    private GeneralAction? GeneralAction { get; }

    public ActionType actionType { get; private set; }

    public MOActionWrapper(Action action)
    {
        Action = action;
        actionType = ActionType.Action;
    }

    public MOActionWrapper(GeneralAction generalAction)
    {
        GeneralAction = generalAction;
        actionType = ActionType.GeneralAction;
    }

    public MOActionWrapper()
    {
        actionType = ActionType.None;
    }

    public uint RowId()
    {
        if (Action != null)
            return Action!.Value.RowId;
        if (GeneralAction != null)
            return GeneralAction!.Value.RowId;
        return 0;
    }

    public bool TargetArea()
    {
        if (Action != null)
            return Action.Value.TargetArea;

        return false;
    }

    public string Name()
    {
        if (Action != null)
            return Action!.Value.Name.ExtractText();
        if (GeneralAction != null)
            return GeneralAction!.Value.Name.ExtractText();
        return string.Empty;
    }

    public string ClassJobCategory()
    {
        if (Action != null)
            return Action!.Value.ClassJobCategory.Value.Name.ExtractText();
        return string.Empty;
    }
}