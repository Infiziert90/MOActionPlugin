#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;

// From: https://github.com/UnknownX7/Hypostasis/blob/master/ImGui/ExcelSheet.cs
namespace MOAction.Windows;

public static class ExcelSheetSelector<T> where T : struct, IExcelRow<T>
{
    private static uint LastJob;
    private static T[]? FilteredSearchSheet;

    private static string SheetSearchText = null!;
    private static string PrevSearchId = null!;
    private static Type PrevSearchType = null!;

    public record ExcelSheetOptions
    {
        public Func<T, string> FormatRow { get; init; } = row => row.ToString()!;
        public Func<T, string, bool>? SearchPredicate { get; init; } = null;
        public Func<T, bool, bool>? DrawSelectable { get; init; } = null;
        public IEnumerable<T>? FilteredSheet { get; init; }
        public Vector2? Size { get; init; } = null;
    }

    public record ExcelSheetComboOptions : ExcelSheetOptions
    {
        public Func<T, string>? GetPreview { get; init; } = null;
        public ImGuiComboFlags ComboFlags { get; init; } = ImGuiComboFlags.None;
    }

    private static void ExcelSheetSearchInput(string id, IEnumerable<T> filteredSheet, Func<T, string, bool> searchPredicate)
    {
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            if (id != PrevSearchId)
            {
                if (typeof(T) != PrevSearchType)
                {
                    SheetSearchText = string.Empty;
                    PrevSearchType = typeof(T);
                }

                FilteredSearchSheet = null;
                PrevSearchId = id;
            }

            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputTextWithHint("##ExcelSheetSearch", "Search", ref SheetSearchText, 128, ImGuiInputTextFlags.AutoSelectAll))
            FilteredSearchSheet = null;

        FilteredSearchSheet ??= filteredSheet.Where(s => searchPredicate(s, SheetSearchText)).ToArray();
    }

    public static bool ExcelSheetCombo(string id, ref uint selectedRow, uint currentJob, ExcelSheetComboOptions? options = null)
    {
        options ??= new ExcelSheetComboOptions();
        var sheet = Plugin.DataManager.GetExcelSheet<T>();

        var getPreview = options.GetPreview ?? options.FormatRow;
        using var combo = ImRaii.Combo(id, sheet.GetRowOrDefault(selectedRow) is { } r ? getPreview(r) : selectedRow.ToString(), options.ComboFlags | ImGuiComboFlags.HeightLargest);
        if (!combo.Success)
            return false;

        // Hacky way of resetting the search sheet if job selection changed
        if (currentJob != LastJob)
        {
            LastJob = currentJob;
            FilteredSearchSheet = null;
        }

        ExcelSheetSearchInput(id, options.FilteredSheet ?? sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

        using var child = ImRaii.Child("ExcelSheetSearchList", options.Size ?? new Vector2(0, 200 * ImGuiHelpers.GlobalScale), true);
        if (!child.Success)
            return false;

        var ret = false;
        var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
        using (var clipper = new ListClipper(FilteredSearchSheet!.Length))
        {
            foreach (var i in clipper.Rows)
            {
                var row = FilteredSearchSheet[i];
                using var pushedId = ImRaii.PushId(i);
                if (!drawSelectable(row, selectedRow == row.RowId))
                    continue;

                selectedRow = row.RowId;
                ret = true;
                break;
            }
        }

        // ImGui issue #273849, children keep popups from closing automatically
        if (ret)
            ImGui.CloseCurrentPopup();

        return ret;
    }
}

public unsafe class ListClipper : IEnumerable<(int, int)>, IDisposable
{
    private ImGuiListClipperPtr Clipper;
    private readonly int CurrentRows;
    private readonly int CurrentColumns;
    private readonly bool TwoDimensional;
    private readonly int ItemRemainder;

    public int FirstRow { get; private set; } = -1;
    public int CurrentRow { get; private set; }
    public int DisplayEnd => Clipper.DisplayEnd;

    public IEnumerable<int> Rows
    {
        get
        {
            while (Clipper.Step()) // Supposedly this calls End()
            {
                if (Clipper.ItemsHeight > 0 && FirstRow < 0)
                    FirstRow = (int)(ImGui.GetScrollY() / Clipper.ItemsHeight);
                for (int i = Clipper.DisplayStart; i < Clipper.DisplayEnd; i++)
                {
                    CurrentRow = i;
                    yield return TwoDimensional ? i : i * CurrentColumns;
                }
            }
        }
    }

    public IEnumerable<int> Columns
    {
        get
        {
            var cols = (ItemRemainder == 0 || CurrentRows != DisplayEnd || CurrentRow != DisplayEnd - 1) ? CurrentColumns : ItemRemainder;
            for (int j = 0; j < cols; j++)
                yield return j;
        }
    }

    public ListClipper(int items, int cols = 1, bool twoD = false, float itemHeight = 0)
    {
        TwoDimensional = twoD;
        CurrentColumns = cols;
        CurrentRows = TwoDimensional ? items : (int)MathF.Ceiling((float)items / CurrentColumns);
        ItemRemainder = !TwoDimensional ? items % CurrentColumns : 0;
        Clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        Clipper.Begin(CurrentRows, itemHeight);
    }

    public IEnumerator<(int, int)> GetEnumerator() => (from i in Rows from j in Columns select (i, j)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        Clipper.Destroy(); // This also calls End() but I'm calling it anyway just in case
        GC.SuppressFinalize(this);
    }
}
