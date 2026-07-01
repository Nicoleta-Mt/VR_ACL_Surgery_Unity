using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private InputActionReference leftPrimaryButtonAction;
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
    private void OnEnable()
    {
        leftPrimaryButtonAction?.action.Enable();
    }
    private void OnDisable()
    {
        leftPrimaryButtonAction?.action.Disable();
    }
    private void Start()
    {
        // Snap to initial angle on startup
        ApplyBitAngle();
    }
    private void Update()
    {
        // Toggle drill diameter and snap bit angle
        if (isEquipped && (leftPrimaryButtonAction?.action.WasPressedThisFrame() ?? false))
        {
            _useLargeRadius = !_useLargeRadius;
            ApplyBitAngle();
        }
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
    public void OnTipContact(VoxelizedModelExample model, Vector3 point, Vector3 tipPos)
    {
        // If we already have a contact model, only switch if this one is closer
        if (_contactModel != null && _contactModel != model)
        {
            float currentDist = Vector3.Distance(_contactPoint, tipPos);
            float newDist = Vector3.Distance(point, tipPos);
            if (newDist >= currentDist) return;
        }
        _contactModel = model;
        _contactPoint = point;
    }
    public void OnTipExit()
    {
        _contactModel = null;
    }
}