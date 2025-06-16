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