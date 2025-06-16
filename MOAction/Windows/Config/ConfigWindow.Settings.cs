using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using MOAction.Configuration;
using Newtonsoft.Json;

namespace MOAction.Windows.Config;

public partial class ConfigWindow
{
    private Tabs Settings()
    {
        using var tabItem = ImRaii.TabItem("Settings");
        if (!tabItem.Success)
            return Tabs.None;

        ImGui.TextUnformatted("This window allows you to set up your action stacks.");
        ImGui.TextUnformatted("What is an action stack? ");
        ImGui.SameLine();
        if (ImGui.Button("Click me to learn!"))
            Dalamud.Utility.Util.OpenLink("https://youtu.be/pm4eCxD90gs");

        ImGui.Checkbox("Stack entry fails if target is out of range.", ref Plugin.Configuration.RangeCheck);
        ImGui.TextUnformatted("MoAction Crosshair location (you'll have to draw it yourself with an overlay)");
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("X-coordinate",ref Plugin.Configuration.CrosshairWidth);
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Y-coordinate",ref Plugin.Configuration.CrosshairHeight);
        ImGui.Checkbox("Enable Crosshair Draw", ref Plugin.Configuration.DrawCrosshair);
        if (Plugin.Configuration.DrawCrosshair)
        {
            using var indent = ImRaii.PushIndent(10.0f);
            ImGui.SetNextItemWidth(100);
            ImGui.InputFloat("Size",ref Plugin.Configuration.CrosshairSize);
            ImGui.SetNextItemWidth(100);
            ImGui.InputFloat("Thickness",ref Plugin.Configuration.CrosshairThickness);

            var spacing = ImGui.CalcTextSize("Target Acquired").X + (ImGui.GetFrameHeightWithSpacing() * 2);
            Helper.ColorPickerWithReset("No Target", ref Plugin.Configuration.CrosshairInvalidColor, ImGuiColors.DalamudRed, spacing);
            Helper.ColorPickerWithReset("Target Acquired", ref Plugin.Configuration.CrosshairValidColor, ImGuiColors.DalamudOrange, spacing);
            Helper.ColorPickerWithReset("Target Locked", ref Plugin.Configuration.CrosshairCastColor, ImGuiColors.ParsedGreen, spacing);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Copy all stacks to clipboard"))
            Plugin.CopyToClipboard(Plugin.MoAction.Stacks);

        ImGui.SameLine();
        if (ImGui.Button("Import stacks from clipboard"))
        {
            ImportStringToMouseOverActions(ImGui.GetClipboardText());
        }

        using var child = ImRaii.Child("scrolling", Vector2.Zero, true);
        if (!child.Success)
            return Tabs.Settings;

        // sorted stacks are grouped by job.
        using (ImRaii.PushId("Sorted Stacks"))
        {
            foreach (var c in Plugin.JobAbbreviations)
            {
                var jobName = c.Abbreviation.ExtractText();
                var entries = Plugin.SavedStacks[c.RowId];
                if (entries.Count == 0)
                    continue;

                using var innerId = ImRaii.PushId(jobName);
                ImGui.SetNextItemWidth(300);
                if (ImGui.CollapsingHeader(jobName))
                {
                    if (ImGui.Button("Copy All to Clipboard"))
                        Plugin.CopyToClipboard(entries.ToList());

                    using var indent = ImRaii.PushIndent();
                    DrawConfigForList(Plugin.SortedStacks[c.RowId]);
                }
            }
        }

        using (ImRaii.PushId("Unsorted Stacks"))
        {
            // Unsorted stacks are created when "Add stack" is clicked.
            DrawConfigForList(Plugin.NewStacks);
        }

        return Tabs.Settings;
    }

    private void DrawSettingsButtons()
    {
        if (ImGui.Button("Save"))
        {
            Plugin.SaveStacks();
        }

        ImGui.SameLine();
        if (ImGui.Button("Save and Close"))
        {
            IsOpen = false;
            Plugin.SaveStacks();
        }

        ImGui.SameLine();
        if (ImGui.Button("New Stack"))
        {
            if (Plugin.ClientState.LocalPlayer != null)
            {
                MoActionStack stack = new(default, null);
                var job = Plugin.ClientState.LocalPlayer.ClassJob.RowId;

                stack.Job = job;
                Plugin.NewStacks.Add(stack);
                Plugin.PluginLog.Debug($"Localplayer job was {job}");
            }
            else
            {
                Plugin.NewStacks.Add(new MoActionStack(default, []));
            }
        }
    }

    private void DrawConfigForList(ICollection<MoActionStack> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            var targetComboLength = ImGui.CalcTextSize("Target of Target   ").X + ImGui.GetFrameHeightWithSpacing();

            var entry = list.ElementAt(i);
            if (!ImGui.CollapsingHeader(entry.BaseAction.RowId == 0 ? "Unset Action###" : $"{entry.BaseAction.Name.ExtractText()}###"))
                continue;

            // Require user to select a job, filtering actions down.
            ImGui.SetNextItemWidth(100);
            using (var combo = ImRaii.Combo("Job", entry.GetJobAbr()))
            {
                if (combo.Success)
                {
                    foreach (var c in Plugin.JobAbbreviations)
                    {
                        if (!ImGui.Selectable(c.Abbreviation.ExtractText()))
                            continue;

                        var job = c.RowId;
                        if (entry.Job != job)
                        {
                            entry.BaseAction = default;
                            foreach (var stackentry in entry.Entries)
                                stackentry.Action = default;
                        }

                        entry.Job = job;
                    }
                }
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            using (var combo = ImRaii.Combo("Held Modifier Key", entry.Modifier.ToString()))
            {
                if (combo.Success)
                {
                    foreach (var vk in MoActionStack.AllKeys)
                        if (ImGui.Selectable(vk.ToString().Replace("MENU", "ALT")))
                            entry.Modifier = vk;
                }
            }

            if (entry.Job is > 0 and < uint.MaxValue)
            {
                using var indent = ImRaii.PushIndent();
                ExcelSheetSelector<Lumina.Excel.Sheets.Action>.ExcelSheetComboOptions actionOptions = new()
                {
                    FormatRow = a => a.RowId switch { _ => a.Name.ExtractText() },
                    FilteredSheet = Plugin.JobActions[entry.Job],
                };

                // Select base action.
                ImGui.SetNextItemWidth(200);
                var baseSelected = entry.BaseAction.RowId;
                if (ExcelSheetSelector<Lumina.Excel.Sheets.Action>.ExcelSheetCombo("Base Action", ref baseSelected, entry.Job, actionOptions))
                {
                    var ability = Sheets.ActionSheet.GetRow(baseSelected);

                    // By default, add UI mouseover as the first TargetType
                    entry.BaseAction = ability;
                    if (entry.Entries.Count == 0)
                        entry.Entries.Add(new StackEntry(ability, Plugin.TargetTypes[0]));
                    else
                        entry.Entries[0].Action = ability;
                }

                if (entry.BaseAction.RowId == 0)
                    continue;

                using (ImRaii.PushIndent())
                {
                    var deleteIdx = -1;
                    var changedOrder = (OrgIdx: -1, NewIdx: -1);
                    foreach (var (stackEntry, idx) in entry.Entries.WithIndex())
                    {
                        using var innerId = ImRaii.PushId(idx); // push stack entry number

                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted($"#{entry.Entries.IndexOf(stackEntry) + 1}");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(targetComboLength);
                        using (var innerCombo = ImRaii.Combo("Target", stackEntry.Target == null ? "" : stackEntry.Target.TargetName))
                        {
                            if (innerCombo.Success)
                            {
                                foreach (var target in stackEntry.Action.TargetArea ? Plugin.TargetTypes.Append(Plugin.GroundTargetTypes) : Plugin.TargetTypes)
                                    if (ImGui.Selectable(target.TargetName))
                                        stackEntry.Target = target;
                            }
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(200);
                        var selected = stackEntry.Action.RowId;
                        if (ExcelSheetSelector<Lumina.Excel.Sheets.Action>.ExcelSheetCombo("Ability", ref selected, entry.Job, actionOptions))
                        {
                            var ability = Sheets.ActionSheet.GetRow(selected);

                            stackEntry.Action = ability;
                            if (ability.TargetArea && Plugin.GroundTargetTypes == stackEntry.Target)
                                stackEntry.Target = null;
                        }

                        // Only show delete and reorder buttons if more than 1 entry
                        if (entry.Entries.Count <= 1)
                            continue;

                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                            deleteIdx = idx;

                        var newIdx = idx;
                        if (Helper.DrawArrows(ref newIdx, entry.Entries.Count))
                            changedOrder = (idx, newIdx);
                    }

                    if (deleteIdx != -1)
                        entry.Entries.RemoveAt(deleteIdx);

                    if (changedOrder.OrgIdx != -1)
                        entry.Entries.Swap(changedOrder.OrgIdx, changedOrder.NewIdx);
                }

                // Add new entry to bottom of stack.
                if (ImGui.Button("Add new stack entry"))
                    entry.Entries.Add(new StackEntry(entry.BaseAction, null));

                ImGui.SameLine();
                if (ImGui.Button("Copy stack to clipboard"))
                    Plugin.CopyToClipboard([entry]);

                ImGui.SameLine();
                if (Helper.CtrlShiftButton("Delete Stack", "Hold Ctrl+Shift to delete the stack."))
                {
                    list.Remove(entry);
                    Plugin.SavedStacks[entry.Job].Remove(entry);
                    i--;
                }
            }
        }
    }
}
