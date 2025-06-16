using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MOAction.Configuration;

[Serializable]
public class ConfigurationEntry
{
    public uint BaseId;

    public List<(string, uint)> Stack;

    public VirtualKey Modifier;
    public uint JobIdx;

    public ConfigurationEntry(uint baseId, List<(string, uint)> stack, VirtualKey modifier, uint job)
    {
        BaseId = baseId;
        Stack = stack;
        Modifier = modifier;
        JobIdx = job;
    }

    [JsonConstructor]
    [Obsolete("This constructor is a one-time migration from the old job string to jobIdx uint. Added 16/12/2024")]
    public ConfigurationEntry(uint baseId, List<(string, uint)> stack, VirtualKey modifier, uint jobIdx, string job = null)
    {
        BaseId = baseId;
        Stack = stack;
        Modifier = modifier;

        if (job == null)
            JobIdx = jobIdx;
        else
            JobIdx = uint.TryParse(job, out var num) ? num : 0;
    }
}