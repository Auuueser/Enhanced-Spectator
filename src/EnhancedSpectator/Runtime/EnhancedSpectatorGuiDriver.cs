using UnityEngine;

namespace EnhancedSpectator.Runtime;

/// <summary>
/// IMGUI entry point, attached only while a feature draws with OnGUI. Unity dispatches
/// OnGUI events to every component that defines it, so the main driver does not.
/// </summary>
internal sealed class EnhancedSpectatorGuiDriver : MonoBehaviour
{
    private void Awake() => useGUILayout = false;
    private void OnGUI() => EnhancedSpectatorRuntimeDriver.GuiTick();
}
