using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace MOAction.Windows;

public static class Helper
{
    public static bool DrawArrows(ref int selected, int length, int id = 0)
    {
        var changed = false;

        // Prevents changing values from triggering EndDisable
        var isMin = selected == 0;
        var isMax = selected + 1 == length;

        ImGui.SameLine(0, 2);

        using (ImRaii.Disabled(isMin))
        {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowUp))
            {
                selected--;
                changed = true;
            }
        }

        ImGui.SameLine(0, 2);

        using (ImRaii.Disabled(isMax))
        {
            if (ImGuiComponents.IconButton(id + 1, FontAwesomeIcon.ArrowDown))
            {
                selected++;
                changed = true;
            }
        }

        return changed;
    }

    public static void Tooltip(string tooltip)
    {
        using (ImRaii.Tooltip())
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f))
        {
            ImGui.TextUnformatted(tooltip);
        }
    }

    public static bool CtrlShiftButton(string label, string tooltip = "")
    {
        var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

        bool ret;
        using (ImRaii.Disabled(!ctrlShiftHeld))
            ret = ImGui.Button(label) && ctrlShiftHeld;

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tooltip(tooltip);

        return ret;
    }

    public static bool ColorPickerWithReset(string name, ref Vector4 current, Vector4 reset, float spacing)
    {
        var changed = ImGui.ColorEdit4($"##{name}ColorPicker", ref current, ImGuiColorEditFlags.NoInputs);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(name);
        ImGui.SameLine(spacing);
        if (ImGui.Button($"Reset##{name}Reset"))
        {
            current = reset;
            changed = true;
        }

        return changed;
    }

    public static void DrawCrosshair(Plugin plugin)
    {
        if (plugin.Configuration.DrawCrosshair)
        {
            if (Plugin.ClientState.LocalPlayer == null)
                return;

            var drawlist = ImGui.GetBackgroundDrawList();
            var center = new Vector2(plugin.Configuration.CrosshairWidth, plugin.Configuration.CrosshairHeight);

            var upper = center with { Y = center.Y - plugin.Configuration.CrosshairSize };
            var lower = center with { Y = center.Y + plugin.Configuration.CrosshairSize };

            var left = center with { X = center.X - plugin.Configuration.CrosshairSize };
            var right = center with { X = center.X + plugin.Configuration.CrosshairSize };

            var color = ImGui.ColorConvertFloat4ToU32(
                Plugin.ClientState.LocalPlayer.IsCasting
                    ? plugin.Configuration.CrosshairCastColor : plugin.MoAction.GetActorFromCrosshairLocation()?.IsValid() ?? false
                        ? plugin.Configuration.CrosshairValidColor : plugin.Configuration.CrosshairInvalidColor);

            drawlist.AddLine(upper, lower, color, plugin.Configuration.CrosshairThickness);
            drawlist.AddLine(left, right, color, plugin.Configuration.CrosshairThickness);
        }
    }
}
