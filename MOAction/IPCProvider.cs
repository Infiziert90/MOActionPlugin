using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace MOAction;

public static class IPCProvider
{
    private static Plugin? Plugin;
    private static ICallGateProvider<uint[]> method_RetargetedActions = null!;

    public static void RegisterIPC(Plugin plugin, IDalamudPluginInterface pluginInterface)
    {
        Plugin = plugin;

        method_RetargetedActions = pluginInterface.GetIpcProvider<uint[]>("MOAction.RetargetedActions");
        method_RetargetedActions.RegisterFunc(GetRetargetedActions);
    }

    private static uint[] GetRetargetedActions()
    {
        if (Plugin == null) return [];

        List<uint> retargetedActions = [];
        Plugin.MoAction.Stacks.ForEach(stack =>
        {
            // Add the base action
            var baseAction = stack.BaseAction.RowId;
            if (retargetedActions.Contains(baseAction)) return;
            retargetedActions.Add(baseAction);

            stack.Entries.ForEach(entry =>
            {
                // Add the action from the stack entry
                var actionId = entry.Action.RowId;
                if (actionId == baseAction || retargetedActions.Contains(actionId)) return;
                retargetedActions.Add(actionId);
            });
        });

        return retargetedActions.ToArray();
    }

    public static void Dispose()
    {
        method_RetargetedActions.UnregisterFunc();
        Plugin = null;
    }
}