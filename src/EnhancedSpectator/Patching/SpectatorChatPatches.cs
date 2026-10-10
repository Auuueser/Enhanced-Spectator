using EnhancedSpectator.GameInterop;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace EnhancedSpectator.Patching;

// Confirmed in V81 HUDManager: SubmitChat_performed refuses dead players and sends to the players in range,
// EnableChat_performed opens the line only for the living, and AddPlayerChatMessageClientRpc shows a line only when
// the sender is as dead as the receiver. Other chat mods come first: ChatCommandAPI (highest priority) runs its
// commands and NiceChat keeps Shift+Enter for new lines, so the group takes a line only after them, and only one
// they let through (__runOriginal); "/" lines are never the group's.
[HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
internal static class SpectatorChatSubmitPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    private static bool Prefix(HUDManager __instance, InputAction.CallbackContext context, bool __runOriginal, out bool __state)
    {
        // Whether there was a line to send, for the postfix (the game clears the field).
        __state = context.performed && __instance.chatTextField.text.Trim().Length > 0;
        return __runOriginal && !(context.performed && LethalCompanyChat.SubmitToGroup(__instance));
    }
    [HarmonyPostfix]
    private static void Postfix(HUDManager __instance, InputAction.CallbackContext context, bool __runOriginal, bool __state)
    { if (__runOriginal && context.performed) LethalCompanyChat.AfterSubmit(__instance, __state); }
}

// LC Chinese Project's chat input-method guard (its postfix at Priority.Last) starts watching the field only when the
// chat is open, so a dead player's chat opens here first. Its Enter-while-composing guard on SubmitChat_performed
// (priority 800) runs before the group's prefix, which then sees __runOriginal false and leaves the line alone.
[HarmonyPatch(typeof(HUDManager), nameof(HUDManager.EnableChat_performed))]
internal static class SpectatorChatOpenPatch
{
    [HarmonyPostfix, HarmonyBefore("Aueser.LCChineseProject")]
    private static void Postfix(InputAction.CallbackContext context, bool __runOriginal)
    { if (__runOriginal && context.performed) LethalCompanyChat.OpenForDead(); }
}

[HarmonyPatch(typeof(HUDManager), nameof(HUDManager.AddPlayerChatMessageClientRpc))]
internal static class SpectatorChatHearPatch
{
    [HarmonyPrefix]
    private static void Prefix(HUDManager __instance, int playerId, out LethalCompanyChat.Heard __state)
        => __state = LethalCompanyChat.BeforeHeard(__instance, playerId);
    [HarmonyPostfix]
    private static void Postfix(HUDManager __instance, string chatMessage, int playerId, bool __runOriginal, LethalCompanyChat.Heard __state)
    { if (__runOriginal) LethalCompanyChat.AfterHeard(__instance, chatMessage, playerId, __state); }
}
