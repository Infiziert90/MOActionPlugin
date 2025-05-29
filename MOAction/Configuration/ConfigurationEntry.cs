using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System.Linq;

namespace MOAction.Configuration;

[Serializable]
public class ConfigurationEntry
{
    public uint BaseId;
    public ActionType ActionType;

    public List<(string, uint, ActionType)> Stack;

    public VirtualKey Modifier;
    public uint JobIdx;


    public ConfigurationEntry(uint baseId, List<(string, uint, ActionType)> stack, VirtualKey modifier, uint job, ActionType actionType)
    {
        BaseId = baseId;
        Stack = stack;
        Modifier = modifier;
        JobIdx = job;
        ActionType = actionType;
    }

    [JsonConstructor]
    [Obsolete("This constructor is a one-time migration from the old job string to jobIdx uint and/or from the old stack to the new stack Added 28/05/2025")]
    public ConfigurationEntry(uint baseId, List<(string, uint, ActionType)> stack, VirtualKey modifier, uint jobIdx, List<(string, uint)> oldstack = null, string job = null, ActionType? actionType = null)
    {
        BaseId = baseId;
        Modifier = modifier;
        if (job == null)
            JobIdx = jobIdx;
        else
            JobIdx = uint.TryParse(job, out var num) ? num : 0;
        if (oldstack == null)
            Stack = stack;
        else
            Stack = [.. stack.Select(item => (item.Item1, item.Item2, ActionType.Action))];
        if (actionType == null)
            ActionType = ActionType.Action;
        else
            ActionType = actionType!.Value;
    }

}