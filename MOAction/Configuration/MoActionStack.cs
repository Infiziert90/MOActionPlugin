using System;
using Dalamud.Game.ClientState.Keys;
using System.Collections.Generic;
using System.Linq;

namespace MOAction.Configuration;

public class MoActionStack(MoActionRecord baseAction, List<StackEntry> list) : IEquatable<MoActionStack>, IComparable<MoActionStack>
{
    public static readonly VirtualKey[] AllKeys = [VirtualKey.NO_KEY, VirtualKey.SHIFT, VirtualKey.MENU, VirtualKey.CONTROL];
    public MoActionRecord BaseAction { get; set; } = baseAction;
    public List<StackEntry> Entries { get; set; } = list ?? [];
    public uint Job { get; set; } = uint.MaxValue;

    public VirtualKey Modifier { get; set; } = 0;

    public bool Equals(ConfigurationEntry c)
    {
        if (c.ConfigurationActionStacks.Count != Entries.Count)
            return false;

        for (var i = 0; i < Entries.Count; i++)
        {
            var myEntry = Entries[i];
            var theirEntry = c.ConfigurationActionStacks[i];
            if (myEntry.Target.TargetName != theirEntry.Target && myEntry.Action.RowId != theirEntry.ActionId)
                return false;
        }

        if (Modifier != c.Modifier)
            return false;

        if (BaseAction.RowId != c.BaseId)
            return false;

        return true;
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public int CompareTo(MoActionStack? other)
    {
        if (other == null)
            return 1;

        return string.Compare(BaseAction.Name.ToString(), other.BaseAction.Name.ToString(), StringComparison.Ordinal);
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public override int GetHashCode()
    {
        return (int)(BaseAction.RowId + Job.GetHashCode() + (int)Modifier);
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var x = (MoActionStack)obj;
        return BaseAction.RowId == x.BaseAction.RowId && Job == x.Job;
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
     public bool Equals(MoActionStack? other)
    {
        if (other == null)
            return false;

        return GetHashCode() == other.GetHashCode();
    }

    public string GetJobAbr()
        => Job == uint.MaxValue ? "Unset Job" : Sheets.ClassJobSheet.First(x => x.RowId == Job).Abbreviation.ToString();

    public string ToJobString()
        => Job == uint.MaxValue ? "Unset Job" : Job.ToString();

    public override string ToString()
        => $"{BaseAction.Name.ToString()} - {string.Join(", ",Entries.Select(entry => $"[{entry}]"))}";
}