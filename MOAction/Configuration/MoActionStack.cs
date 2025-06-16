using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOAction.Configuration;

public class MoActionStack : IEquatable<MoActionStack>, IComparable<MoActionStack>
{
    public static readonly VirtualKey[] AllKeys = [VirtualKey.NO_KEY, VirtualKey.SHIFT, VirtualKey.MENU, VirtualKey.CONTROL];
    public Lumina.Excel.Sheets.Action BaseAction { get; set; }
    public List<StackEntry> Entries { get; set; }
    public uint Job { get; set; }

    public VirtualKey Modifier { get; set; }

    public MoActionStack(Lumina.Excel.Sheets.Action baseAction, List<StackEntry> list)
    {
        BaseAction = baseAction;
        Entries = list ?? [];
        Job = uint.MaxValue;
        Modifier = 0;
    }

    public bool Equals(ConfigurationEntry c)
    {
        if (c.Stack.Count != Entries.Count)
            return false;

        for (var i = 0; i < Entries.Count; i++)
        {
            var myEntry = Entries[i];
            var theirEntry = c.Stack[i];
            if (myEntry.Target.TargetName != theirEntry.Item1 && myEntry.Action.RowId != theirEntry.Item2)
                return false;
        }

        if (Modifier != c.Modifier)
            return false;

        if (BaseAction.RowId != c.BaseId)
            return false;

        return true;
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public int CompareTo(MoActionStack other)
    {
        if (other == null)
            return 1;

        return string.Compare(BaseAction.Name.ExtractText(), other.BaseAction.Name.ExtractText(), StringComparison.Ordinal);
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public override int GetHashCode()
    {
        return (int)(BaseAction.RowId + Job.GetHashCode() + (int)Modifier);
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
    public override bool Equals(object obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var x = (MoActionStack)obj;
        return BaseAction.RowId == x.BaseAction.RowId && Job == x.Job;
    }

    //TODO make the overwritten equals and hashcodes a bit more smart, to not ignore the deeper stackentry list
     public bool Equals(MoActionStack other)
    {
        if (other == null)
            return false;

        return GetHashCode() == other.GetHashCode();
    }

    public string GetJobAbr()
    {
        return Job == uint.MaxValue ? "Unset Job" : Sheets.ClassJobSheet.First(x => x.RowId == Job).Abbreviation.ExtractText();
    }

    public string ToJobString()
    {
        return Job == uint.MaxValue ? "Unset Job" : Job.ToString();
    }

    public override string ToString() => $"{BaseAction.Name.ExtractText()} - {string.Join(", ",Entries.Select(entry => $"[{entry}]"))}";
}