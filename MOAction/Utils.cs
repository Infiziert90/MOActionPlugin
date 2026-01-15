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
        => list.Select(c => (c.Name.ToString(), c.Abbreviation.ToString()));

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
    /// Small helper method to convert a ARR job to it's class (parent job), non-ARR jobs return themselves as parent.
    /// </summary>
    /// <param name="jobId"></param>
    /// <returns></returns>
    public static uint ConvertARRJobToClass(uint jobId)
    {
        //ClassJobCategorySheet.RowId = 110 is the Disciple of War or Magic JOBS category
        if (Sheets.ClassJobCategorySheet.TryGetRow(110, out var category) &&
            Sheets.ClassJobSheet.TryGetRow(jobId, out var cj) && HasJob(category, cj))
        {
            //If it's a job, grab its parent, non-ARR jobs return themselves as parent.
            Plugin.PluginLog.Verbose($"Clasjob rowid {cj.RowId} - parent rowid {cj.ClassJobParent.RowId}");
            return cj.ClassJobParent.RowId;
        }

        return jobId;
    }

    /// <summary>
    /// Small helper method to check if a job id is a Disciple of War or Magic job.
    /// </summary>
    /// <param name="jobId"></param>
    /// <returns></returns>
    public static bool IsADiscipleOfWarOrMagicJob(uint jobId)
    {
        //ClassJobCategorySheet.RowId = 110 is the Disciple of War or Magic JOBS category
        if (Sheets.ClassJobCategorySheet.TryGetRow(110, out var category) &&
            Sheets.ClassJobSheet.TryGetRow(jobId, out var cj))
            return HasJob(category, cj);

        return false;
    }

    /// <summary>
    /// Excel Searcher/validator to get the bool value from a specific row in a specific category.
    /// </summary>
    /// <param name="category"></param>
    /// <param name="cj"></param>
    /// <returns></returns>
    private static bool HasJob(this ClassJobCategory category, ClassJob cj)
        => category.ExcelPage.ReadBool(category.RowOffset + cj.RowId + 4);

    /// <summary>
    /// Grabs whatever actionId is currently inside the duty action slot with index 0-4
    /// </summary>
    /// <param name="rowId">the rowId of the general actions used as placeholder, ranging from 1-5</param>
    /// <param name="action">out parameter, the duty action</param>
    /// <returns>success or not</returns>
    public static unsafe bool GetDutyActionRow(uint rowId, out Action action)
    {
        var actionManager = ActionManager.Instance();
        if (rowId > 6)
        {
                action = default;
                return false;
        }
        var id = DutyActionManager.GetDutyActionId((ushort)(rowId - 1));
        if (id > 0)
        {
            Plugin.PluginLog.Verbose($"Duty Action with rowID {id} selected from duty action slot {rowId-1}");
            return Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(id), out action);
        }

        action = default;
        return false;
    }
}

public class MoActionRecordComparer : IEqualityComparer<MoActionRecord>
{
    bool IEqualityComparer<MoActionRecord>.Equals(MoActionRecord? x, MoActionRecord? y)
    {
        if(x is null || y is null)
            return false;
        return x.RowId == y.RowId && x.ActionType == y.ActionType;
    }

    int IEqualityComparer<MoActionRecord>.GetHashCode(MoActionRecord obj)
    {
        return obj.RowId.GetHashCode();
    }
}

