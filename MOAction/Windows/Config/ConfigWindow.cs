using System;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System.Collections.Generic;
using System.Text;
using MOAction.Configuration;
using Newtonsoft.Json;
using Dalamud.Plugin.Services;
using System.Linq;
using Dalamud.Interface.Style;

namespace MOAction.Windows.Config;

[Flags]
public enum Tabs
{
    None = 0,
    Settings = 1,
    Wizard = 2,
    About = 3
}

public partial class ConfigWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private const float SeparatorPadding = 1.0f;
    private static float GetSeparatorPaddingHeight => SeparatorPadding * ImGuiHelpers.GlobalScale;

    public ConfigWindow(Plugin plugin) : base("Action Stack Setup##MOAction")
    {
        Plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 800),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var open = Tabs.None;
        var bottomContentHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + GetSeparatorPaddingHeight;
        using (var contentChild = ImRaii.Child("ConfigContent", new Vector2(0, -bottomContentHeight)))
        {
            if (contentChild.Success)
            {
                using var tabBar = ImRaii.TabBar("##ConfigTabBar");
                if (tabBar.Success)
                {
                    open |= Settings();

                    open |= Wizard();

                    open |= About();
                }
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(SeparatorPadding);

        using var bottomChild = ImRaii.Child("ConfigBottomBar", new Vector2(0, 0), false, 0);
        if (!bottomChild.Success)
            return;

        switch (open)
        {
            case Tabs.Settings:
                DrawSettingsButtons();
                break;
            case Tabs.About:
                DrawAboutButtons();
                break;
        }
    }

    private void ImportStringToMouseOverActions(String import)
    {
        try
        {
            var importString = Encoding.UTF8.GetString(Convert.FromBase64String(import));
            Plugin.PluginLog.Verbose($"import: {importString}");
            var jsonDeserializeObject = JsonConvert.DeserializeObject<List<ConfigurationEntry>>(importString);
            Plugin.PluginLog.Verbose($"jsonDeserializedObject: {JsonConvert.SerializeObject(jsonDeserializeObject)}");
            var tempStacks = Plugin.SortStacks(Plugin.RebuildStacks(jsonDeserializeObject));
            Plugin.PluginLog.Information("Imported stacks on base actions will never overwrite existing stacks and are thus not imported.");
            Plugin.Ichatgui.Print("Imported stacks on base actions will never overwrite existing stacks and are thus not imported.", "MoAction", 0x1F);
            foreach (var (classjob, v) in tempStacks)
            {
                //no need to import if there's nothing to import for that specific classjob
                if (v.Count > 0)
                {
                    Plugin.PluginLog.Information("importing: {import}", v);
                    Plugin.Ichatgui.Print("importing for " + Sheets.ClassJobSheet.GetRow(classjob).Abbreviation.ExtractText() + ":\n" + string.Join("\n", v.Select(entry => $"[{entry}]")));
                    if (Plugin.SavedStacks.TryGetValue(classjob, out var value))
                    {
                        //TODO: union currently ignores new stacks of baseactions already configured with any stack and does not do a deeper union on the lists within the stack
                        Plugin.PluginLog.Verbose("old: {old}", value);
                        value.UnionWith(v);
                        Plugin.PluginLog.Verbose("union: {union}", value);
                    }
                    else
                    {
                        Plugin.SavedStacks[classjob] = v;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Importing stacks failed.");
        }
    }
}
