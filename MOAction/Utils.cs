using System.Collections.Generic;
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

    public static unsafe bool getActionFromGeneralDutyAction(GeneralAction generalAction, out Action action)
    {
        var actionManager = ActionManager.Instance();
        uint id = 0;
        if (generalAction.RowId == 26)
        {
            id = DutyActionManager.GetDutyActionId(0);
        }
        else if (generalAction.RowId == 27)
        {
            id = DutyActionManager.GetDutyActionId(1);
        }

        if (id == 0)
        {
            action = default;
            return false;
        }

        return Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(id), out action);

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

