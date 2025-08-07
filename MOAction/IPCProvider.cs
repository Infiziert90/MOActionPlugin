using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace MOAction;

public static class IPCProvider
{
    private static Plugin? Plugin;
    private static ICallGateProvider<uint[]> MethodRetargetedActions = null!;

    public static void RegisterIPC(Plugin plugin, IDalamudPluginInterface pluginInterface)
    {
        Plugin = plugin;

        MethodRetargetedActions = pluginInterface.GetIpcProvider<uint[]>("MOAction.RetargetedActions");
        MethodRetargetedActions.RegisterFunc(GetRetargetedActions);
    }

    private static uint[] GetRetargetedActions()
    {
        if (Plugin == null)
            return [];

        List<uint> retargetedActions = [];
        Plugin.MoAction.Stacks.ForEach(stack =>
        {
            // Add the base action
            var baseAction = stack.BaseAction;
            if (retargetedActions.Contains(baseAction.RowId) || baseAction.ActionType != FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action)
                return;

            retargetedActions.Add(baseAction.RowId);

            stack.Entries.ForEach(entry =>
            {
                // Add the action from the stack entry
                var actionId = entry.Action.RowId;
                if (actionId == baseAction.RowId || retargetedActions.Contains(actionId))
                    return;

                retargetedActions.Add(actionId);
            });
        });

        return retargetedActions.ToArray();
    }

    public static void Dispose()
    {
        MethodRetargetedActions.UnregisterFunc();
        Plugin = null;
    }
}