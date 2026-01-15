using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lumina.Excel.Sheets;

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
}

public class ActionComparer : IEqualityComparer<Action>
{
    bool IEqualityComparer<Action>.Equals(Action x, Action y)
    {
        return x.RowId == y.RowId;
    }

    int IEqualityComparer<Action>.GetHashCode(Action obj)
    {
        return obj.RowId.GetHashCode();
    }
}