using System;
using System.IO;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;


namespace MOAction.Windows.Config;

public partial class ConfigWindow
{
    private const string DirectoryName = "beginner";

    private Tabs Wizard()
    {
        using var tabItem = ImRaii.TabItem("Wizard");
        if (!tabItem.Success)
            return Tabs.None;

        ImGui.TextUnformatted("This window has some beginner friendly stacks for you to import.");
        if (ImGui.Button("Import Gapcloser basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, DirectoryName, "GAPCLOSER.json")))));
            Plugin.SaveStacks();
        }

        if (ImGui.Button("Import Tank Basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, DirectoryName, "TANK.json")))));
            Plugin.SaveStacks();
        }

        if (ImGui.Button("Import White Mage Basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, DirectoryName, "WHM.json")))));
            Plugin.SaveStacks();
        }

        if (ImGui.Button("Import Scholar Basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!,DirectoryName,"SCH.json")))));
            Plugin.SaveStacks();
        }

        if (ImGui.Button("Import Astrologian Basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!,DirectoryName,"AST.json")))));
            Plugin.SaveStacks();
        }

        if (ImGui.Button("Import Sage Basics"))
        {
            ImportStringToMouseOverActions(Convert.ToBase64String(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!,DirectoryName,"SGE.json")))));
             Plugin.SaveStacks();
        }

        return Tabs.Wizard;
    }

}
