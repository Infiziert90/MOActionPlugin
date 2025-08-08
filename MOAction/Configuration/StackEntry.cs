using MOAction.Target;

namespace MOAction.Configuration;

public class StackEntry(MoActionRecord action, TargetType targ)
{
    public MoActionRecord Action = action;
    public TargetType Target { get; set; } = targ;

    public override string ToString() => $"{Action.Name}@{Target}";
}