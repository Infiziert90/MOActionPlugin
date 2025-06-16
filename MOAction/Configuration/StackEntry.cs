using MOAction.Target;

namespace MOAction.Configuration;

public class StackEntry
{
    public Lumina.Excel.Sheets.Action Action;
    public TargetType Target { get; set; }

    public StackEntry(Lumina.Excel.Sheets.Action action, TargetType targ)
    {
        Action = action;
        Target = targ;
    }

    public override string ToString() => $"{Action.Name.ExtractText()}@{Target}";
}