using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Keys;
using MOAction.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace MOAction;

public class MOAction
{
    private readonly Plugin Plugin;
    private readonly MOActionAddressResolver Address;

    public readonly List<MoActionStack> Stacks = [];

    private Hook<ActionManager.Delegates.UseAction> RequestActionHook;

    public MOAction(Plugin plugin)
    {
        Plugin = plugin;
        Address = new MOActionAddressResolver();
    }

    public unsafe void Enable()
    {
        // read current bytes at GtQueuePatch for Dispose
        SafeMemory.ReadBytes(Address.GtQueuePatch, 3, out var prePatch);
        Address.PreGtQueuePatchData = prePatch;

        //writing a AL operator to overwrite existing XOR operator
        SafeMemory.WriteBytes(Address.GtQueuePatch, [0x90, 0x32, 0xC0]);

        RequestActionHook = Plugin.HookProvider.HookFromAddress<ActionManager.Delegates.UseAction>(ActionManager.MemberFunctionPointers.UseAction, HandleRequestAction);
        RequestActionHook.Enable();
    }

    public void Dispose()
    {
        if (RequestActionHook.IsEnabled)
        {
            RequestActionHook.Dispose();
            //re-write the original 2 bytes that were there
            SafeMemory.WriteBytes(Address.GtQueuePatch, Address.PreGtQueuePatchData);
        }
    }

    /// <summary>
    /// Main hooked function for the Mouse over action plugin, it intercepts the requested action
    /// </summary>
    private unsafe bool HandleRequestAction(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        // Only care about "real" actions. Not doing anything dodgy
        if (actionType != ActionType.Action)
            return RequestActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        Plugin.PluginLog.Verbose($"Receiving handling request for Action: {actionId}");

        var (action, target) = GetActionTarget(actionId, actionType);
        if (action.RowId == 0)
            return RequestActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        var objectId = target?.GameObjectId ?? 0xE0000000;
        Plugin.PluginLog.Verbose($"Execution Action {action.Name.ToString()} with ActionID {action.RowId} on object with ObjectId {objectId}");

        var ret = RequestActionHook.Original(thisPtr, actionType, action.RowId, objectId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        Plugin.PluginLog.Verbose($"Executed Action {action.Name.ToString()} with ActionID {action.RowId} on object with ObjectId {objectId}, response: {ret}");

        // Enqueue GT action
        var actionManager = ActionManager.Instance();
        if (action.TargetArea)
        {
            Plugin.PluginLog.Verbose($"setting actionmanager areaTargetingExecuteAtObject to {objectId}");
            actionManager->AreaTargetingExecuteAtObject = objectId;
            Plugin.PluginLog.Verbose($"setting actionmanager AreaTargetingExecuteAtCursor to true");
            actionManager->AreaTargetingExecuteAtCursor = true;
        }

        Plugin.PluginLog.Verbose("finishing MoActionHook");
        return ret;
    }

    /// <summary>
    ///  gets the target and the action to use.
    /// </summary>
    /// <param name="actionId">the action id being handled</param>
    /// <param name="actionType">action type is only used in the off-cooldown check, should always be "Action"</param>

    private unsafe (Lumina.Excel.Sheets.Action action, IGameObject? target) GetActionTarget(uint actionId, ActionType actionType)
    {
        if (!Sheets.ActionSheet.TryGetRow(actionId, out var action))
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Lumina Excel did not succesfully retrieve row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (action.RowId == 0)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Lumina Excel returned default row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (!Plugin.PlayerState.IsLoaded)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Dalamud has no reference to LocalPlayer.\nFailsafe triggering early return");
            return (default, null);
        }

        if (Plugin.PlayerState.ClassJob.RowId == 0)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Dalamud thinks you're an ADV\nFailsafe triggering early return");
            return (default, null);
        }

        var actionManager = ActionManager.Instance();
        var adjusted = actionManager->GetAdjustedActionId(actionId);

        //Loop through Duty actions 0 -> slots of duty actions
        //NumValidSlots is at most 4, this is in Occult Cresent
        var applicableActions = Enumerable.Empty<MoActionStack>();
        var isDutyAction = false;
        var dutyActionManager = DutyActionManager.GetInstanceIfReady();
        if (dutyActionManager != null)
        {
            for (ushort dutyActionSlot = 0; dutyActionSlot < dutyActionManager->NumValidSlots; dutyActionSlot++)
            {
                if (action.RowId != DutyActionManager.GetDutyActionId(dutyActionSlot))
                    continue;

                Plugin.PluginLog.Verbose("We're dealing with a duty action");
                isDutyAction = true;
                //Fetch the stacks we linked to phantom actions 1-5 to match between duty actions 0-4
                applicableActions = Stacks.Where(entry =>
                    entry.BaseAction.ActionType == ActionType.GeneralAction &&
                    entry.BaseAction.RowId == 1 + dutyActionSlot);
                break;
            }
        }

        if (!isDutyAction)
        {
            applicableActions = Stacks.Where(entry =>
                (entry.BaseAction.RowId == action.RowId ||
                 entry.BaseAction.RowId == adjusted ||
                 actionManager->GetAdjustedActionId(entry.BaseAction.RowId) == adjusted)
                && VerifyJobEqualsOrEqualsParentJob(entry.Job, Plugin.PlayerState.ClassJob.RowId));

        }

        MoActionStack? stackToUse = null;
        foreach (var entry in applicableActions)
        {
            if (entry.Modifier == VirtualKey.NO_KEY)
            {
                stackToUse = entry;
            }
            else if (Plugin.KeyState[entry.Modifier])
            {
                stackToUse = entry;
                break;
            }
        }

        if (stackToUse == null)
        {
            Plugin.PluginLog.Verbose($"No action stack applicable for action: {action.Name.ToString()}");
            return (default, null);
        }

        foreach (var entry in stackToUse.Entries)
        {
            Plugin.PluginLog.Verbose($"unadjusted entry action, {entry.Action.RowId}, {entry.Action.Name}");
            if (CanUseAction(entry, actionType, out var target, out var usedAction))
                return (usedAction, target);
        }

        Plugin.PluginLog.Verbose("Chosen MoAction Entry stack did not have any usable actions.");
        return (default, null);
    }

    /// <summary>
    /// Figures out if you are able to cast the action inside stackentry at the target inside the stack entry.
    /// </summary>
    /// <param name="stackEntry">stack entry to be checked</param>
    /// <param name="actionType">used for the cooldown check, should always be "Action"</param>
    /// <param name="target">out parameter, the target to return to the hook to fire the spell at</param>
    /// <param name="action">out parameter, the spell to return to the hook to fire at the target</param>
    private unsafe bool CanUseAction(StackEntry stackEntry, ActionType actionType, out IGameObject? target, out Lumina.Excel.Sheets.Action action)
    {
        target = stackEntry.Target.GetTarget();
        var id = stackEntry.Action.RowId;
        //Early sanity checks
        if (id == 0 || !Plugin.PlayerState.IsLoaded || stackEntry.Action.ActionType is not (ActionType.GeneralAction or ActionType.Action))
        {
            Plugin.PluginLog.Verbose("Invalid action or player state not loaded, returning false");
            action = default;
            return false;
        }

        var actionManager = ActionManager.Instance();
        switch (stackEntry.Action.ActionType)
        {
            //assign the out action to the action to be checked if can be used
            case ActionType.Action:
            {
                if (!Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(id), out action))
                    return false; // just in case
                break;
            }
            case ActionType.GeneralAction:
            {
                //From the GeneralActions saved, we handle duty action 1-5
                if (!Utils.GetDutyActionRow(id, out action))
                    return false;
                break;
            }
            default:
                action = default;
                return false;
        }

        //if there's no target, return false unless it is a ground target action at mouse point.
        if (target is null)
            return !stackEntry.Target.ObjectNeeded;

        // Check if ability is on CD or not (charges are fun!)
        var abilityOnCoolDownResponse = actionManager->IsActionOffCooldown(actionType, action.RowId);
        Plugin.PluginLog.Verbose($"Is {action.Name.ToString()} off cooldown? : {abilityOnCoolDownResponse}");
        if (!abilityOnCoolDownResponse)
            return false;

        var player = Plugin.ObjectTable.LocalPlayer;
        var targetPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
        if (Plugin.Configuration.RangeCheck)
        {
            var playerPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;
            var err = ActionManager.GetActionInRangeOrLoS(action.RowId, playerPtr, targetPtr);
            if (action.TargetArea)
                return false;
            if (err != 0 && err != 565)
                return false;
        }

        Plugin.PluginLog.Verbose($"Is {action.Name.ToString()} a role action?: {action.IsRoleAction}");
        if (!action.IsRoleAction)
        {
            Plugin.PluginLog.Verbose($"Is {action.Name.ToString()} usable at level: {action.ClassJobLevel} available for player {player.Name} with {player.Level}?");
            if (action.ClassJobLevel > player.Level)
                return false;
        }

        Plugin.PluginLog.Verbose($"Is {action.Name.ToString()} a area spell/ability? {action.TargetArea}");
        if (action.TargetArea)
            return true;

        var selfOnlyTargetAction = action is { CanTargetAlly: false, CanTargetHostile: false, CanTargetParty: false };
        Plugin.PluginLog.Verbose($"Can {action.Name.ToString()} target: friendly - {action.CanTargetAlly}, hostile  - {action.CanTargetHostile}, party  - {action.CanTargetParty}, dead - {action.DeadTargetBehaviour == 0}, self - {action.CanTargetSelf}");
        if (selfOnlyTargetAction)
        {
            Plugin.PluginLog.Verbose("Can only use this action on player, setting player as target");
            target = player;
        }

        var gameCanUseActionResponse = ActionManager.CanUseActionOnTarget(action.RowId, (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address);
        Plugin.PluginLog.Verbose($"Can I use action: {action.RowId} with name {action.Name.ToString()} on target {target.BaseId} with name {target.Name} : {gameCanUseActionResponse}");
        return gameCanUseActionResponse;
    }

    public unsafe IGameObject? GetGuiMoPtr() =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->UiMouseOverTarget);

    public IGameObject? GetFieldMo() =>
        Plugin.TargetManager.MouseOverTarget;

    public unsafe IGameObject? GetActorFromPlaceholder(string placeholder) =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->ResolvePlaceholder(placeholder, 1, 0));


    public unsafe IGameObject? GetActorFromCrosshairLocation() =>
        Plugin.Objects.CreateObjectReference((nint)TargetSystem.Instance()->GetMouseOverObject(Plugin.Configuration.CrosshairWidth, Plugin.Configuration.CrosshairHeight));

    private static bool VerifyJobEqualsOrEqualsParentJob(uint job, uint localPlayerRowId)
    {
        if (localPlayerRowId == job) return true;
        var parentJob = Utils.ConvertARRJobToClass(job);
        return parentJob == localPlayerRowId;
    }
}