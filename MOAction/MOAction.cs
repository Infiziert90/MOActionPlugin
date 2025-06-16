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
        Plugin.PluginLog.Verbose($"Execution Action {action.Name.ExtractText()} with ActionID {action.RowId} on object with ObjectId {objectId}");

        var ret = RequestActionHook.Original(thisPtr, actionType, action.RowId, objectId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        Plugin.PluginLog.Verbose($"Executed Action {action.Name.ExtractText()} with ActionID {action.RowId} on object with ObjectId {objectId}, response: {ret}");

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

    private unsafe (Lumina.Excel.Sheets.Action action, IGameObject target) GetActionTarget(uint actionID, ActionType actionType)
    {
        if (!Sheets.ActionSheet.TryGetRow(actionID, out var action))
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Lumina Excel did not succesfully retrieve row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (action.RowId == 0)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Lumina Excel returned default row.\nFailsafe triggering early return");
            return (default, null);
        }

        if (Plugin.ClientState.LocalPlayer == null)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Dalamud has no reference to LocalPlayer.\nFailsafe triggering early return");
            return (default, null);
        }

        if (Plugin.ClientState.LocalPlayer.ClassJob.RowId == 0)
        {
            Plugin.PluginLog.Verbose("ILLEGAL STATE: Dalamud thinks you're an ADV\nFailsafe triggering early return");
            return (default, null);
        }

        var actionManager = ActionManager.Instance();
        var adjusted = actionManager->GetAdjustedActionId(actionID);

        var applicableActions = Stacks.Where(entry =>
            (entry.BaseAction.RowId == action.RowId ||
            entry.BaseAction.RowId == adjusted ||
            actionManager->GetAdjustedActionId(entry.BaseAction.RowId) == adjusted)
            && VerifyJobEqualsOrEqualsParentJob(entry.Job, Plugin.ClientState.LocalPlayer.ClassJob.RowId)
            );

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
            Plugin.PluginLog.Verbose($"unadjusted entry action, {entry.Action.RowId}, {entry.Action.Name.ExtractText()}");
            var (response, target) = CanUseAction(entry, actionType);
            if (response)
                return (entry.Action, target);
        }

        Plugin.PluginLog.Verbose("Chosen MoAction Entry stack did not have any usable actions.");
        return (default, null);
    }

    private unsafe (bool, IGameObject Target) CanUseAction(StackEntry targ, ActionType actionType)
    {
        if (targ.Target == null || targ.Action.RowId == 0 || Plugin.ClientState.LocalPlayer == null)
            return (false, null);

        var actionManager = ActionManager.Instance();
        if (!Sheets.ActionSheet.TryGetRow(actionManager->GetAdjustedActionId(targ.Action.RowId), out var action))
            return (false, null); // just in case

        var target = targ.Target.GetTarget();
        if (target == null)
            return targ.Target.ObjectNeeded ? (false, Plugin.ClientState.LocalPlayer) : (true, null);

        // Check if ability is on CD or not (charges are fun!)
        var abilityOnCoolDownResponse = actionManager->IsActionOffCooldown(actionType, action.RowId);
        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} off cooldown? : {abilityOnCoolDownResponse}");
        if (!abilityOnCoolDownResponse)
            return (false, target);

        var player = Plugin.ClientState.LocalPlayer;
        var targetPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
        if (Plugin.Configuration.RangeCheck)
        {
            var playerPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;
            var err = ActionManager.GetActionInRangeOrLoS(action.RowId, playerPtr, targetPtr);
            if (action.TargetArea)
                return (true, target);
            if (err != 0 && err != 565)
                return (false, target);
        }

        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} a role action?: {action.IsRoleAction}");
        if (!action.IsRoleAction)
        {
            Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} usable at level: {action.ClassJobLevel} available for player {player.Name} with {player.Level}?");
            if (action.ClassJobLevel > Plugin.ClientState.LocalPlayer.Level)
                return (false, target);
        }

        Plugin.PluginLog.Verbose($"Is {action.Name.ExtractText()} a area spell/ability? {action.TargetArea}");
        if (action.TargetArea)
            return (true, target);

        var selfOnlyTargetAction = !action.CanTargetAlly && !action.CanTargetHostile && !action.CanTargetParty;
        Plugin.PluginLog.Verbose($"Can {action.Name.ExtractText()} target: friendly - {action.CanTargetAlly}, hostile  - {action.CanTargetHostile}, party  - {action.CanTargetParty}, dead - {action.DeadTargetBehaviour == 0}, self - {action.CanTargetSelf}");
        if (selfOnlyTargetAction)
        {
            Plugin.PluginLog.Verbose("Can only use this action on player, setting player as target");
            target = Plugin.ClientState.LocalPlayer;
        }

        var gameCanUseActionResponse = ActionManager.CanUseActionOnTarget(action.RowId, (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address);
        Plugin.PluginLog.Verbose($"Can I use action: {action.RowId} with name {action.Name.ExtractText()} on target {target.DataId} with name {target.Name} : {gameCanUseActionResponse}");

        return (gameCanUseActionResponse, target);
    }

    public unsafe IGameObject GetGuiMoPtr() =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->UiMouseOverTarget);

    public IGameObject GetFieldMo() =>
        Plugin.TargetManager.MouseOverTarget;

    public unsafe IGameObject GetActorFromPlaceholder(string placeholder) =>
        Plugin.Objects.CreateObjectReference((nint)PronounModule.Instance()->ResolvePlaceholder(placeholder, 1, 0));


    public unsafe IGameObject GetActorFromCrosshairLocation() =>
        Plugin.Objects.CreateObjectReference((nint)TargetSystem.Instance()->GetMouseOverObject(Plugin.Configuration.CrosshairWidth, Plugin.Configuration.CrosshairHeight));


    public static bool VerifyJobEqualsOrEqualsParentJob(uint job, uint LocalPlayerRowID) =>
    LocalPlayerRowID == job || (Sheets.ClassJobSheet.TryGetRow(job, out var classjob) && LocalPlayerRowID == classjob.ClassJobParent.RowId);

}