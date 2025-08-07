using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;

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
            var baseRecord = stack.BaseAction;
            if (retargetedActions.Contains(baseRecord.RowId))
                return;

            if (baseRecord.ActionType == ActionType.GeneralAction)
            {
                if (Utils.GetDutyActionRow(baseRecord.RowId, out var action))
                {
                    retargetedActions.Add(action.RowId);
                }
            }
            else
            {
                retargetedActions.Add(baseRecord.RowId);
            }


            stack.Entries.ForEach(entry =>
            {
                // Add the action from the stack entry
                var record = entry.Action;
                if (record.RowId == baseRecord.RowId || retargetedActions.Contains(record.RowId))
                    return;

                if (record.ActionType == ActionType.GeneralAction)
                {
                    if (Utils.GetDutyActionRow(baseRecord.RowId, out var action))
                    {
                        retargetedActions.Add(action.RowId);
                    }
                }
                else
                {
                    retargetedActions.Add(record.RowId);
                }
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