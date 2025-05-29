using MOAction.Target;

namespace MOAction.Configuration;

public class StackEntry
{
    public MOActionWrapper Action;
    public TargetType Target { get; set; }

    public StackEntry(Lumina.Excel.Sheets.Action action, TargetType targ)
    {
        Action = new(action);
        Target = targ;
    }

    public StackEntry(Lumina.Excel.Sheets.GeneralAction action, TargetType targ)
    {
        Action = new(action);
        Target = targ;
    }
    public StackEntry(MOActionWrapper action, TargetType targ)
    {
        Action = action;
        Target = targ;
    }

    public override string ToString() => $"{Action.Name}@{Target}";
}