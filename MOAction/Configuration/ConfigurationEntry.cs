using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MOAction.Configuration;

[Serializable]
public class ConfigurationEntry
{
    public uint BaseId;
    public List<ConfigurationActionStack> ConfigurationActionStacks;
    public ActionType ActionType;
    public VirtualKey Modifier;
    public uint JobIdx;


    public ConfigurationEntry(uint baseId, List<ConfigurationActionStack> configurationActionStacks, VirtualKey modifier, uint job,
        ActionType actionType)
    {
        BaseId = baseId;
        ConfigurationActionStacks = configurationActionStacks;
        Modifier = modifier;
        JobIdx = job;
        ActionType = actionType;
    }

    [JsonConstructor]
    [Obsolete("This constructor is a one-time migration from the old job string to jobIdx uint and/or from the old stack to the new stack Added 22/12/2025")]
    public ConfigurationEntry(uint baseId,
        List<ConfigurationActionStack>? configurationActionStacks,
        VirtualKey modifier,
        uint? jobIdx,
        string? job,
        List<(string, uint)>? stack,
        ActionType actionType = 0)
    {
        BaseId = baseId;
        Modifier = modifier;
        ActionType = actionType == 0 ? ActionType.Action : actionType;

        if (jobIdx is not null)
        {
            if (jobIdx == 0 && Plugin.PlayerState.IsLoaded)
                JobIdx = Plugin.PlayerState.ClassJob.RowId;
            else
                JobIdx = jobIdx.Value;
        }
        //Legacy configuration parsing: string job containing a number
        else
        {
            JobIdx = uint.TryParse(job, out var num) ? num : 0;
        }

        if (configurationActionStacks is not null)
            ConfigurationActionStacks = configurationActionStacks;

        //Legacy configuration parsing: Tuple(uint string)
        else
        {
            ConfigurationActionStacks = [];
            if (stack is null)
                return;

            foreach (var tuple in stack)
                ConfigurationActionStacks.Add(new ConfigurationActionStack(tuple.Item1, tuple.Item2, ActionType.Action));
        }
    }

    public override string ToString()
        => $"BaseId: {BaseId}, Modifier: {Modifier}, JobIdx: {JobIdx}, Stacks: {ConfigurationActionStacks.Count}";

    [Serializable]
    public class ConfigurationActionStack(string target, uint actionId, ActionType actionType)
    {
        public string Target { get; set; } = target;
        public uint ActionId { get; set; } = actionId;
        public ActionType ActionType { get; set; } = actionType == default ? ActionType.Action : actionType;

        public override string ToString()
            => $"Target: {Target}, ActionId: {ActionId}";
    }
}