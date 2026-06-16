using UnityEngine;
using UnityEngine.XR;
using MarchingCubesProject;

/// <summary>
/// DrillBit — VR version.
///
/// What changed vs the desktop version:
///   • Removed Input.GetMouseButton(0).
///   • Drilling is now triggered by the XR controller's triggerButton.
///     The script reads whichever hand is currently holding the drill via
///     XRGrabInteractable, so it works with either hand automatically.
///   • isEquipped is still set by VRDrillGrabbable (same as DrillPickup did).
/// </summary>
public class DrillBit : MonoBehaviour
{
    [Header("Drill Settings")]
    public float drillRadius = 0.5f;
    public float drillRate = 0.05f;
    public bool isDrilling = false;
    public bool isEquipped = false;

    [Header("Audio")]
    [Tooltip("Assign the DrillAudio component (can live on this GameObject or the parent).")]
    public DrillAudio drillAudio;

    [Header("Visuals")]
    public Transform drillBitMesh;
    public float spinSpeed = 720f; // degrees per second

    // ── private state ─────────────────────────────────────────────────────────
    private float _nextDrillTime = 0f;
    private bool _wasContacting = false;
    private bool _wasDrilling = false;

    // Set by DrillTip callbacks
    private VoxelizedModelExample _contactModel = null;
    private Vector3 _contactPoint;

    // XR controller device resolved at equip-time
    private InputDevice _controller;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Update()
    {
        // ── Resolve controller once per equip session ─────────────────────────
        if (isEquipped && !_controller.isValid)
            TryResolveController();

        // ── Trigger replaces left mouse button ───────────────────────────────
        bool triggerHeld = false;
        if (isEquipped && _controller.isValid)
            _controller.TryGetFeatureValue(CommonUsages.triggerButton, out triggerHeld);

        SetDrilling(isEquipped && triggerHeld);

        // ── Contact state ─────────────────────────────────────────────────────
        bool contacting = _contactModel != null;

        // ── Spin the bit while drilling ───────────────────────────────────────
        if (isDrilling && drillBitMesh != null)
            drillBitMesh.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        // ── Notify DrillAudio only on state change ────────────────────────────
        if (drillAudio != null && (contacting != _wasContacting || isDrilling != _wasDrilling))
        {
            drillAudio.SetContactState(isDrilling, contacting);
            _wasContacting = contacting;
            _wasDrilling = isDrilling;
        }

        // ── Actual voxel drilling ─────────────────────────────────────────────
        if (!isDrilling) return;
        if (Time.time < _nextDrillTime) return;

        if (_contactModel != null)
        {
            _contactModel.Drill(_contactPoint, drillRadius);
            _nextDrillTime = Time.time + drillRate;
        }
    }

    // ── Public API (unchanged) ────────────────────────────────────────────────

    public void SetDrilling(bool drilling)
    {
        isDrilling = drilling;
        _wasDrilling = drilling;

        if (drillAudio != null)
            drillAudio.SetContactState(drilling, _wasContacting);
    }

    /// <summary>Called by DrillTip when the tip enters/stays in a voxel model.</summary>
    public void OnTipContact(VoxelizedModelExample model, Vector3 point)
    {
        _contactModel = model;
        _contactPoint = point;
    }

    /// <summary>Called by DrillTip when the tip leaves a voxel model.</summary>
    public void OnTipExit()
    {
        _contactModel = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to find the XR controller for whichever hand is holding the drill.
    /// Called once per frame until a valid device is found.
    /// </summary>
    private void TryResolveController()
    {
        // Try right hand first, then left — whichever reports as holding something
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid && rightHand.TryGetFeatureValue(CommonUsages.gripButton, out bool rGrip) && rGrip)
        {
            _controller = rightHand;
            return;
        }

        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.gripButton, out bool lGrip) && lGrip)
        {
            _controller = leftHand;
        }
    }
}