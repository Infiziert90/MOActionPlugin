using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using MOAction.Target;
using MOAction.Configuration;
using System.Text;
using Newtonsoft.Json;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Client.Game;
using MOAction.Windows;
using MOAction.Windows.Config;

namespace MOAction;

public class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider HookProvider { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;


    public readonly MOActionConfiguration Configuration;

    private readonly WindowSystem WindowSystem = new("MoActionPlugin");
    private ConfigWindow ConfigWindow { get; }

    public readonly MOAction MoAction;
    private List<MoActionRecord> ApplicableActions = [];

    public readonly List<TargetType> TargetTypes;
    public readonly TargetType GroundTargetTypes;
    public readonly List<MoActionStack> NewStacks = [];
    public readonly Dictionary<uint, HashSet<MoActionStack>> SavedStacks = [];
    public readonly Dictionary<uint, List<MoActionStack>> SortedStacks = [];
    public readonly List<Lumina.Excel.Sheets.ClassJob> JobAbbreviations;
    public Dictionary<uint, List<MoActionRecord>> JobActions = [];

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
        JobAbbreviations = Sheets.ClassJobSheet.Where(x => x.JobIndex > 0).OrderBy(c => c.Abbreviation.ToString()).ToList();

        SortActions();
        MoAction = new MOAction(this);

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
        PluginLog.Information($"Loading in {Configuration.Stacks.Count} stacks.");
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
        List<ConfigurationEntry> entries = [];
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
        Dictionary<uint, HashSet<MoActionStack>> toReturn = [];
        foreach (var c in JobAbbreviations)
        {
            var jobstack = list.Where(s => s.Job == c.RowId).ToList();
            if (jobstack.Count > 0)
                toReturn[c.RowId] = [..jobstack];
            else
                toReturn[c.RowId] = [];
        }
        return toReturn;
    }

    public void SaveStacks()
    {
        SortStacks();
        MoAction.Stacks.Clear();
        foreach (var entry in SavedStacks.SelectMany(x => x.Value))
            MoAction.Stacks.Add(entry);

        Configuration.Stacks.Clear();
        foreach (var x in MoAction.Stacks)
            Configuration.Stacks.Add(new ConfigurationEntry(x.BaseAction.RowId, [.. x.Entries.Select(y => new ConfigurationEntry.ConfigurationActionStack(y.Target.TargetName,y.Action.RowId,y.Action.ActionType))], x.Modifier, x.Job, x.BaseAction.ActionType));

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
        if (configurationEntries.Count == 0)
            return [];

        var toReturn = new List<MoActionStack>();
        foreach (var entry in configurationEntries)
        {
            PluginLog.Verbose("entry: {entry}", entry);
            var action = ApplicableActions.FirstOrDefault(x => x.RowId == entry.BaseId);
            if (action is null || action.RowId == 0)
                continue;

            var job = entry.JobIdx;
            List<StackEntry> entries = [];
            PluginLog.Verbose("entry: {entry}",entry);
            foreach (var stackEntry in entry.ConfigurationActionStacks)
            {
                PluginLog.Verbose("stack entry: {stackEntry}", stackEntry);
                var targ = TargetTypes.FirstOrDefault(x => x.TargetName == stackEntry.Target) ?? GroundTargetTypes;
                var action1 = ApplicableActions.FirstOrDefault(x => x.RowId == stackEntry.ActionId);
                if (action1.RowId == 0)
                    continue;

                entries.Add(new StackEntry(action1, targ));
            }

            toReturn.Add(new MoActionStack(action, entries)
            {
                Job = job,
                Modifier = entry.Modifier
            });
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
        var tmp = new HashSet<MoActionRecord>(new MoActionRecordComparer());
        foreach (var action in ApplicableActions)
            tmp.Add(action);
        ApplicableActions = [.. tmp.OrderBy(c => c.Name)];
    }

    //Create a set of "Duty Actions" for configuration use
    private List<MoActionRecord> CreateDutyActions()
    {
        List<MoActionRecord> dutyActions = [];
        for (uint i = 1; i < 6; i++)
        {
            dutyActions.Add(new MoActionRecord(i,ActionType.GeneralAction,"Duty Action " + i,false,""));
        }
        return dutyActions;
    }

    public void InitUsableActions()
    {
        JobActions = [];
        ApplicableActions = [.. Sheets.ActionSheet.Where(row => row is { IsPlayerAction: true, IsPvP: false, ClassJobLevel: > 0 }).Where(a => a.RowId != 212).Select(y => { return new MoActionRecord(y); })];
        if (Configuration.IncludeDutyActions)
        {
            ApplicableActions.AddRange(CreateDutyActions());
        }
        SortActions();

        foreach (var availableJobs in JobAbbreviations)
        {
            var availableActions = ApplicableActions.Where(action =>
            {
                var names = action.ClassJobCategory;
                return names.Contains(availableJobs.Name.ToString()) || names.Contains(availableJobs.Abbreviation.ToString());
            }).ToList();
            if (Configuration.IncludeDutyActions)
            {
                availableActions.AddRange(CreateDutyActions());
            }
            JobActions.Add(availableJobs.RowId, availableActions);
        }
    }
}