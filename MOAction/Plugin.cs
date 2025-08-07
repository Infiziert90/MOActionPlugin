using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using MOAction.Target;
using MOAction.Configuration;
using Dalamud.Game.ClientState.Objects;
using System.Text;
using Newtonsoft.Json;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using MOAction.Windows;
using MOAction.Windows.Config;

namespace MOAction;

public class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider HookProvider { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IChatGui Ichatgui { get; private set; } = null!;


    public MOActionConfiguration Configuration;

    public readonly WindowSystem WindowSystem = new("MoActionPlugin");
    public ConfigWindow ConfigWindow { get; }

    public readonly MOAction MoAction;
    private List<MOActionWrapper> ApplicableActions;

    public readonly List<TargetType> TargetTypes;
    public readonly TargetType GroundTargetTypes;
    public readonly List<MoActionStack> NewStacks = [];
    public readonly Dictionary<uint, HashSet<MoActionStack>> SavedStacks = [];
    public readonly Dictionary<uint, List<MoActionStack>> SortedStacks = [];
    public readonly List<Lumina.Excel.Sheets.ClassJob> JobAbbreviations;
    public Dictionary<uint, List<MOActionWrapper>> JobActions = [];

    //31-35 are phantom actions 1-5, they're used as flags to be able to build stacks on duty action 1-5
    //A more elegant solution is something I did not immediately find.
    public static readonly uint[] UsedDutyActionRowIds = [31, 32, 33, 34, 35];

    public Plugin()
    {
        CommandManager.AddHandler("/pmoaction", new CommandInfo(OnCommandDebugMouseover)
        {
            HelpMessage = "Open a window to edit mouseover action settings.",
            ShowInHelp = true
        });
        CommandManager.AddHandler("/moaction", new CommandInfo(OnCommandDebugMouseover)
        {
            HelpMessage = "Alias for /pmoaction.",
            ShowInHelp = true
        });
        CommandManager.AddHandler("/mo", new CommandInfo(OnCommandDebugMouseover)
        {
            HelpMessage = "Alias for /pmoaction.",
            ShowInHelp = true
        });
        IPCProvider.RegisterIPC(this, PluginInterface);
        var config = PluginInterface.GetPluginConfig() as MOActionConfiguration ?? new MOActionConfiguration();
        MoAction = new MOAction(this);
        JobAbbreviations = [.. Sheets.ClassJobSheet.Where(x => x.JobIndex > 0).OrderBy(c => c.Abbreviation.ExtractText())];

        TargetTypes =
        [
            new EntityTarget(MoAction.GetGuiMoPtr, "UI Mouseover"),
            new EntityTarget(MoAction.GetFieldMo, "Field Mouseover"),
            new EntityTarget(MoAction.GetActorFromCrosshairLocation,"Crosshair"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<t>"), "Target"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<f>"), "Focus Target"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<tt>"), "Target of Target"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<me>"), "Self"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<2>"), "<2>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<3>"), "<3>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<4>"), "<4>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<5>"), "<5>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<6>"), "<6>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<7>"), "<7>"),
            new EntityTarget(() => MoAction.GetActorFromPlaceholder("<8>"), "<8>")
        ];

        GroundTargetTypes = new EntityTarget(() => null, "Mouse Location", false);

        foreach (var entry in config.Stacks.ToArray())
        {
            if (entry.JobIdx == 0)
                continue;

            if (!Sheets.ClassJobSheet.TryGetRow(entry.JobIdx, out var row) || row.RowId == 0)
                config.Stacks.Remove(entry);
        }
        Configuration = config;
        InitUsableActions();

        SavedStacks = SortStacks(RebuildStacks(Configuration.Stacks));
        foreach (var (k, v) in SavedStacks)
        {
            var tmp = v.ToList();
            tmp.Sort();
            SortedStacks[k] = tmp;
        }

        MoAction.Enable();
        foreach (var entry in SavedStacks)
            MoAction.Stacks.AddRange(entry.Value);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        PluginInterface.UiBuilder.OpenMainUi += OpenUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenUi;
        PluginInterface.UiBuilder.Draw += Draw;
    }

    private void OpenUi()
    {
        ConfigWindow.Toggle();
    }

    private void Draw()
    {
        WindowSystem.Draw();
        Helper.DrawCrosshair(this);

        if (!ConfigWindow.IsOpen && NewStacks.Count != 0)
            SortStacks();
    }

    public void CopyToClipboard(List<MoActionStack> list)
    {
        List<ConfigurationEntry> entries = new();
        foreach (var elem in list)
        {
            var x = Configuration.Stacks.FirstOrDefault(e => elem.Equals(e));
            if (x == null)
                continue;

            entries.Add(x);
        }
        var json = JsonConvert.SerializeObject(entries);
        ImGui.SetClipboardText(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    public Dictionary<uint, HashSet<MoActionStack>> SortStacks(List<MoActionStack> list)
    {
        Dictionary<uint, HashSet<MoActionStack>> toReturn = new();
        foreach (var c in JobAbbreviations)
        {
            var jobstack = list.Where(s => s.Job == c.RowId).ToList();
            if (jobstack.Count > 0)
                toReturn[c.RowId] = [.. jobstack];
            else
                toReturn[c.RowId] = [];
        }
        return toReturn;
    }

    public void SaveStacks()
    {
        SortStacks();
        MoAction.Stacks.Clear();
        foreach (var x in SavedStacks)
            foreach (var entry in x.Value)
                MoAction.Stacks.Add(entry);

        Configuration.Stacks.Clear();
        foreach (var x in MoAction.Stacks)
            Configuration.Stacks.Add(new ConfigurationEntry(x.BaseAction.RowId(), [.. x.Entries.Select(y => new ConfigurationEntry.ConfigurationActionStack(y.Target.TargetName,y.Action.RowId(),y.Action.actionType))], x.Modifier, x.Job, x.BaseAction.actionType));

        PluginInterface.SavePluginConfig(Configuration);
    }

    private void SortStacks()
    {
        foreach (var stack in NewStacks.Where(s => s.Job != uint.MaxValue && s.Entries.Count > 0))
            SavedStacks[stack.Job].Add(stack);

        NewStacks.Clear();
        foreach (var (k, v) in SavedStacks)
        {
            var tmp = v.ToList();
            tmp.Sort();
            SortedStacks[k] = tmp;
        }
    }

    public List<MoActionStack> RebuildStacks(List<ConfigurationEntry> configurationEntries)
    {
        if (configurationEntries == null)
            return [];

        var toReturn = new List<MoActionStack>();
        foreach (var entry in configurationEntries)
        {
            var action = ApplicableActions.FirstOrDefault(x => x.RowId() == entry.BaseId && x.actionType == entry.ActionType);
            if (action == null || action.RowId() == 0)
                continue;

            var job = entry.JobIdx;
            List<StackEntry> entries = [];
            foreach (var stackEntry in entry.ConfigurationActionStacks)
            {
                var targ = TargetTypes.FirstOrDefault(x => x.TargetName == stackEntry.Target) ?? GroundTargetTypes;
                var action1 = ApplicableActions.FirstOrDefault(x => x.RowId() == stackEntry.ActionId && x.actionType == stackEntry.ActionType);
                if (action1 == null || action1.RowId() == 0)
                    continue;

                entries.Add(new StackEntry(action1, targ));
            }
            if (entries.Count > 0)
            {
                toReturn.Add(new MoActionStack(action, entries)
                {
                    Job = job,
                    Modifier = entry.Modifier
                });
            }
        }

        return toReturn;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.OpenMainUi -= OpenUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenUi;
        PluginInterface.UiBuilder.Draw -= Draw;

        IPCProvider.Dispose();
        MoAction.Dispose();
        CommandManager.RemoveHandler("/pmoaction");
        CommandManager.RemoveHandler("/moaction");
        CommandManager.RemoveHandler("/mo");

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
    }

    private void OnCommandDebugMouseover(string command, string arguments)
    {
        ConfigWindow.Toggle();
    }

    private void SortActions()
    {
        // HashSet is to ensure actions are unique
        var tmp = new HashSet<MOActionWrapper>(new ActionWrapperComparer());
        foreach (var action in ApplicableActions)
            tmp.Add(action);
        ApplicableActions = [.. tmp.OrderBy(c => c.Name())];
    }

    public void InitUsableActions()
    {
        JobActions = [];
        ApplicableActions = [.. Sheets.ActionSheet.Where(row => row is { IsPlayerAction: true, IsPvP: false, ClassJobLevel: > 0 }).Where(a => a.RowId != 212).Select(y => { return new MOActionWrapper(y); })];
        if (Configuration.IncludeDutyActions)
        {
            foreach (uint dutyActionRowId in UsedDutyActionRowIds)
            {
                ApplicableActions.Add(new MOActionWrapper(Sheets.GeneralActions.GetRow(dutyActionRowId)));
            }
        }
        SortActions();

        foreach (var availableJobs in JobAbbreviations)
        {
            var availableActions = ApplicableActions.Where(action =>
            {
                var names = action.ClassJobCategory();
                return names.Contains(availableJobs.Name.ExtractText()) || names.Contains(availableJobs.Abbreviation.ExtractText());
            }).ToList();
            if (Configuration.IncludeDutyActions)
            {
                foreach (uint dutyActionRowId in UsedDutyActionRowIds)
                {
                    availableActions.Add(new MOActionWrapper(Sheets.GeneralActions.GetRow(dutyActionRowId)));
                }
            }
            JobActions.Add(availableJobs.RowId, availableActions);
        }
    }
}