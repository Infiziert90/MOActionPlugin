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
        Address = new MOActionAddressResolver(Plugin.SigScanner);

        Plugin.PluginLog.Info("===== M O A C T I O N =====");
    }

    public unsafe void Enable()
    {
        // read current bytes at GtQueuePatch for Dispose
        SafeMemory.ReadBytes(Address.GtQueuePatch, 3, out var prePatch);
        Address.PreGtQueuePatchData = prePatch;

        //writing a AL operator to overwrite existing XOR operator
        SafeMemory.WriteBytes(Address.GtQueuePatch, [0x90, 0x32, 0xC0]);

        RequestActionHook =
            Plugin.HookProvider.HookFromAddress<ActionManager.Delegates.UseAction>(
                ActionManager.MemberFunctionPointers.UseAction, HandleRequestAction);
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
    private unsafe bool HandleRequestAction(ActionManager* thisPtr, ActionType actionType, uint actionId,
        ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        // Only care about "real" actions. Not doing anything dodgy
        if (actionType != ActionType.Action)
            return RequestActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId,
                outOptAreaTargeted);
        Plugin.PluginLog.Verbose($"Receiving handling request for Action: {actionId}");

        var (action, target) = GetActionTarget(actionId, actionType);
        if (action.RowId == 0)
            return RequestActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId,
                outOptAreaTargeted);

        var objectId = target?.GameObjectId ?? 0xE0000000;
        Plugin.PluginLog.Verbose(
            $"Execution Action {action.Name.ExtractText()} with ActionID {action.RowId} on object with ObjectId {objectId}");

        var ret = RequestActionHook.Original(thisPtr, actionType, action.RowId, objectId, extraParam, mode,
            comboRouteId, outOptAreaTargeted);

        Plugin.PluginLog.Verbose(
            $"Executed Action {action.Name.ExtractText()} with ActionID {action.RowId} on object with ObjectId {objectId}, response: {ret}");

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
    private unsafe (Lumina.Excel.Sheets.Action action, IGameObject target) GetActionTarget(uint actionId,
        ActionType actionType)
    {
        if (!Sheets.ActionSheet.TryGetRow(actionId, out var action))
        {
            Plugin.PluginLog.Verbose(
                "ILLEGAL STATE: Lumina Excel did not succesfully retrieve row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (action.RowId == 0)
        {
            Plugin.PluginLog.Verbose(
                "ILLEGAL STATE: Lumina Excel returned default row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (Plugin.ClientState.LocalPlayer == null)
        {
            Plugin.PluginLog.Verbose(
                "ILLEGAL STATE: Dalamud has no reference to LocalPlayer.\nFailsafe triggering early return");
            return (default, null);
        }

        if (Plugin.ClientState.LocalPlayer.ClassJob.RowId == 0)
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
                entry.BaseAction.ActionType == ActionType.Action &&
                (
                    entry.BaseAction.RowId == action.RowId ||
                    entry.BaseAction.RowId == adjusted ||
                    actionManager->GetAdjustedActionId(entry.BaseAction.RowId) == adjusted
                )
                && VerifyJobEqualsOrEqualsParentJob(entry.Job, Plugin.ClientState.LocalPlayer.ClassJob.RowId)
            );
        }

        MoActionStack stackToUse = null;
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
            Plugin.PluginLog.Verbose($"No action stack applicable for action: {action.Name.ExtractText()}");
            return (default, null);
        }

        foreach (var entry in stackToUse.Entries)
        {
            Plugin.PluginLog.Verbose($"unadjusted entry action, {entry.Action.RowId}, {entry.Action.Name}");
            if (CanUseAction(entry, actionType, out var target, out var usedAction))
            {
                return (usedAction, target);
            }
        }

        Plugin.PluginLog.Verbose("Chosen MoAction Entry stack did not have any usable actions.");
        return (default, null);
    }

    /// <summary>
    /// Figures out if you are able to cast the action inside stackentry at the target inside the stack entry.
    /// </summary>
    /// <param name="stackentry">stack entry to be checked</param>
    /// <param name="actionType">used for the cooldown check, should always be "Action"</param>
    /// <param name="target">out parameter, the target to return to the hook to fire the spell at</param>
    /// <param name="action">out parameter, the spell to return to the hook to fire at the target</param>
    private unsafe bool CanUseAction(StackEntry stackentry, ActionType actionType, out IGameObject target,
        out Lumina.Excel.Sheets.Action action)
    {
        target = stackentry.Target.GetTarget();
        var id = stackentry.Action.RowId;
        //Early sanity checks
        if (stackentry.Target == null || id == 0 || Plugin.ClientState.LocalPlayer == null ||
            stackentry.Action.ActionType is not (ActionType.GeneralAction or ActionType.Action))
        {
            action = default;
            return false;
        }

        var actionManager = ActionManager.Instance();
        //assign the out action to the action to be checked if can be used
        if (stackentry.Action.ActionType == ActionType.Action)
        {
            if (!Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(id), out action))
                return false; // just in case
        }
        else if (stackentry.Action.ActionType == ActionType.GeneralAction)
        {
            //From the GeneralActions saved, we handle duty action 1-5
            if (!Utils.GetDutyActionRow(id, out action))
                return false;
        }
        else
        {
            action = default;
            return false;
        }

        //if there's no target, return false unless it is a ground target action at mousepoint.
        if (target == null)
            return !stackentry.Target.ObjectNeeded;

        // Check if ability is on CD or not (charges are fun!)
        var abilityOnCoolDownResponse = actionManager->IsActionOffCooldown(actionType, action.RowId);
        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} off cooldown? : {abilityOnCoolDownResponse}");
        if (!abilityOnCoolDownResponse)
            return false;

        var player = Plugin.ClientState.LocalPlayer;
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

        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} a role action?: {action.IsRoleAction}");
        if (!action.IsRoleAction)
        {
            Plugin.PluginLog.Verbose(
                $"Is {action.Name.ExtractText()} usable at level: {action.ClassJobLevel} available for player {player.Name} with {player.Level}?");
            if (action.ClassJobLevel > Plugin.ClientState.LocalPlayer.Level)
                return false;
        }

        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} a area spell/ability? {action.TargetArea}");
        if (action.TargetArea)
            return true;

        var selfOnlyTargetAction = !action.CanTargetAlly && !action.CanTargetHostile && !action.CanTargetParty;
        Plugin.PluginLog.Verbose(
            $"Can {action.Name.ExtractText()} target: friendly - {action.CanTargetAlly}, hostile  - {action.CanTargetHostile}, party  - {action.CanTargetParty}, dead - {action.DeadTargetBehaviour == 0}, self - {action.CanTargetSelf}");
        if (selfOnlyTargetAction)
        {
            Plugin.PluginLog.Verbose("Can only use this action on player, setting player as target");
            target = Plugin.ClientState.LocalPlayer;
        }

        var gameCanUseActionResponse = ActionManager.CanUseActionOnTarget(action.RowId,
            (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address);
        Plugin.PluginLog.Verbose(
            $"Can I use action: {action.RowId} with name {action.Name.ExtractText()} on target {target.DataId} with name {target.Name} : {gameCanUseActionResponse}");
        return gameCanUseActionResponse;
    }

    public unsafe IGameObject GetGuiMoPtr() =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->UiMouseOverTarget);

    public IGameObject GetFieldMo() =>
        Plugin.TargetManager.MouseOverTarget;

    public unsafe IGameObject GetActorFromPlaceholder(string placeholder) =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->ResolvePlaceholder(placeholder, 1, 0));


    public unsafe IGameObject GetActorFromCrosshairLocation() =>
        Plugin.Objects.CreateObjectReference(
            (nint)TargetSystem.Instance()->GetMouseOverObject(Plugin.Configuration.CrosshairWidth,
                Plugin.Configuration.CrosshairHeight));


    public static bool VerifyJobEqualsOrEqualsParentJob(uint job, uint LocalPlayerRowID) =>
        LocalPlayerRowID == job || (Sheets.ClassJobSheet.TryGetRow(job, out var classjob) &&
                                    LocalPlayerRowID == classjob.ClassJobParent.RowId);
}