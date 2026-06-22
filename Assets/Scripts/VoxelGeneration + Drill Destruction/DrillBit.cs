using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using MarchingCubesProject;

public class DrillBit : MonoBehaviour
{
    [Header("Drill Settings")]
    public float drillRadiusSmall = 0.3f;
    public float drillRadiusLarge = 0.7f;
    public float drillRate = 0.05f;
    public bool isDrilling = false;
    public bool isEquipped = false;

    [Header("Diameter Switch")]
    private bool _useLargeRadius = false;
    public float drillRadius => _useLargeRadius ? drillRadiusLarge : drillRadiusSmall;

    [Header("Bit Angles")]
    public float angleSmall = -90f;
    public float angleLarge = -50f;

    [Header("Audio")]
    [Tooltip("Assign the DrillAudio component (can live on this GameObject or the parent).")]
    public DrillAudio drillAudio;

    private float nextDrillTime = 0f;
    private bool _wasContacting = false;
    private bool _wasDrilling = false;

    [Header("Visuals")]
    public Transform drillBitMesh;
    public Transform drillTipMesh;
    public float spinSpeed = 720f;

    private VoxelizedModelExample _contactModel = null;
    private Vector3 _contactPoint;

    // XR references
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor _currentInteractor;

    private void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);

            // Activate (trigger pull) = drill
            _grabInteractable.activated.AddListener(OnActivated);
            _grabInteractable.deactivated.AddListener(OnDeactivated);
        }
    }

    private void OnDestroy()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnReleased);
            _grabInteractable.activated.RemoveListener(OnActivated);
            _grabInteractable.deactivated.RemoveListener(OnDeactivated);
        }
    }

    private void Start()
    {
        ApplyBitAngle();
    }

    private void Update()
    {
        bool contacting = _contactModel != null;

        if (isDrilling && drillBitMesh != null)
            drillBitMesh.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);

        if (drillAudio != null && (contacting != _wasContacting || isDrilling != _wasDrilling))
        {
            drillAudio.SetContactState(isDrilling, contacting);
            _wasContacting = contacting;
            _wasDrilling = isDrilling;
        }

        if (!isDrilling) return;
        if (Time.time < nextDrillTime) return;

        if (_contactModel != null)
        {
            _contactModel.Drill(_contactPoint, drillRadius);
            nextDrillTime = Time.time + drillRate;
        }
    }

    // ── XR Event Handlers ──────────────────────────────────────────────

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isEquipped = true;
        _currentInteractor = args.interactorObject;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isEquipped = false;
        _currentInteractor = null;
        SetDrilling(false);
    }

    /// <summary>
    /// Called when the controller's Activate button (trigger) is pressed.
    /// Begins drilling.
    /// </summary>
    private void OnActivated(ActivateEventArgs args)
    {
        if (isEquipped)
            SetDrilling(true);
    }

    /// <summary>
    /// Called when the controller's Activate button (trigger) is released.
    /// Stops drilling.
    /// </summary>
    private void OnDeactivated(DeactivateEventArgs args)
    {
        SetDrilling(false);
    }

    /// <summary>
    /// Call this from a controller button binding (e.g. Primary Button / thumbstick press)
    /// to toggle between small and large drill radius.
    /// Wire it up via XRI Input Action or an InputActionReference on the interactor.
    /// </summary>
    public void ToggleDiameter()
    {
        if (!isEquipped) return;
        _useLargeRadius = !_useLargeRadius;
        ApplyBitAngle();
    }

    // ── Original helpers (unchanged) ───────────────────────────────────

    private void ApplyBitAngle()
    {
        if (drillTipMesh == null) return;
        float angle = _useLargeRadius ? angleLarge : angleSmall;
        drillTipMesh.localRotation = Quaternion.Euler(angle, 90f, -90f);
    }

    public void SetDrilling(bool drilling)
    {
        isDrilling = drilling;
        _wasDrilling = drilling;

        if (drillAudio != null)
            drillAudio.SetContactState(drilling, _wasContacting);
    }

    public void OnTipContact(VoxelizedModelExample model, Vector3 point)
    {
        _contactModel = model;
        _contactPoint = point;
    }

    public void OnTipExit()
    {
        _contactModel = null;
    }
}