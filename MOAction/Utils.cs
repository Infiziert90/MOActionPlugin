﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lumina.Excel.Sheets;
using MOAction.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MOAction;

public static class Utils
{
    /// <summary> Gets the name and abbreviation of all jobs. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IEnumerable<(string Name, string Abr)> GetNames(this IEnumerable<ClassJob> list)
        => list.Select(c => (c.Name.ExtractText(), c.Abbreviation.ExtractText()));

    /// <summary> Iterate over enumerables with additional index. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IEnumerable<(T Value, int Index)> WithIndex<T>(this IEnumerable<T> list)
        => list.Select((x, i) => (x, i));

    /// <summary> Swaps two items in a list. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swap<T>(this List<T> list, int i, int j)
    {
        (list[i], list[j]) = (list[j], list[i]);
    }

    /// <summary>
    /// Grabs whatever actionId is currently inside the duty action slot with index 0-4
    /// </summary>
    /// <param name="rowId">the rowId of the general actions used as placeholder, ranging from 31 to 35</param>
    /// <param name="action">out parameter, the duty action</param>
    /// <returns>success or not</returns>
    public static unsafe bool GetDutyActionRow(uint rowId, out Action action)
    {
        var actionManager = ActionManager.Instance();
        if (rowId is < 30 or > 36)
        {
                action = default;
                return false;
        }
        //Phantom actions are 31-35, so to get slot 0-4 from 31-35
        var dutyActionSlot = rowId - 31;
        var id = DutyActionManager.GetDutyActionId((ushort)dutyActionSlot);

        if (id > 0)
        {
            Plugin.PluginLog.Verbose($"Duty Action with rowID {id} selected from duty action slot {dutyActionSlot}");
            return Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(id), out action);
        }

        action = default;
        return false;
    }
}

public class ActionWrapperComparer : IEqualityComparer<MOActionWrapper>
{
    bool IEqualityComparer<MOActionWrapper>.Equals(MOActionWrapper x, MOActionWrapper y)
    {
        return x.RowId() == y.RowId() && x.actionType == y.actionType;
    }

    int IEqualityComparer<MOActionWrapper>.GetHashCode(MOActionWrapper obj)
    {
        return obj.RowId().GetHashCode();
    }
}

