using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// VRDrillGrabbable — attach to the drill GameObject (NOT the camera/player).
///
/// Requirements:
///   • Add an XRGrabInteractable component to the same GameObject, OR let this
///     script's Reset() method add one automatically in the Editor.
///   • The drill must have a Rigidbody and at least one non-trigger Collider.
///   • XR Interaction Manager must exist in the scene (XRI Toolkit standard setup).
///
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class VRDrillGrabbable : MonoBehaviour
{
    // ── private state ─────────────────────────────────────────────────────────
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private DrillBit _drillBit;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _drillBit = GetComponentInChildren<DrillBit>() ?? GetComponent<DrillBit>();

        // Subscribe to XRI grab/release events (replaces the E-key logic)
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        if (_grabInteractable == null) return;
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    // ── grab callbacks (replaces TryPickup / Drop) ────────────────────────────

    /// <summary>Called by XRGrabInteractable when the player grips the drill.</summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_drillBit != null)
            _drillBit.isEquipped = true;
    }

    /// <summary>Called by XRGrabInteractable when the player releases the drill.</summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        if (_drillBit == null) return;
        _drillBit.isEquipped = false;
        _drillBit.SetDrilling(false); // stop drilling immediately on release
    }

    // ── Editor helper ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void Reset()
    {
        // Auto-add XRGrabInteractable when this component is first added in Editor
        if (GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() == null)
            gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }
#endif
}