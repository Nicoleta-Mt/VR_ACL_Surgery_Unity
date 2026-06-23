using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DrillPickup — attach to your Camera (or Player GameObject).
///
/// Requirements:
///   • The drill must have a Rigidbody.
///   • The drill must have a non-trigger Collider so the raycast can hit it.
///   • Tag your drill GameObject "Drill" (or change drillTag below).
///
/// Controls (New Input System):
///   • Look at drill + press E  → pick up
///   • Press E again            → drop
///   • While held, the drill follows your camera aim smoothly.
///   • Left Mouse Button        → activates drilling (handled by DrillBit).
/// </summary>
public class DrillPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public string drillTag = "Drill";
    public float pickupRange = 3f;
    public float holdDistance = 1.8f;
    public float followSpeed = 15f;
    public float rotationSpeed = 15f;

    [Header("Hold Offset")]
    public Vector3 holdOffset = new Vector3(0.3f, -0.2f, 0f);
    public Vector3 holdRotationOffset = new Vector3(0f, 0f, 0f);

    [Header("Physics")]
    [Range(0f, 1f)]
    public float velocityDamping = 0.9f;

    // ── private state ─────────────────────────────────────────────────────────
    private GameObject _heldDrill;
    private Rigidbody _heldRb;
    private DrillBit _heldDrillBit;
    private bool _isHolding;
    private bool _drillHadGravity;
    private bool _lookingAtDrill;

    // Tracks whether E was already pressed last frame (manual GetKeyDown equivalent)
    private bool _eWasPressed;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Update()
    {
        CheckLook();

        bool ePressed = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        bool eDown = ePressed && !_eWasPressed;   // rising edge = "key down"
        _eWasPressed = ePressed;

        if (eDown)
        {
            if (_isHolding) Drop();
            else TryPickup();
        }
    }

    private void FixedUpdate()
    {
        if (!_isHolding || _heldRb == null) return;

        Vector3 targetPos = transform.position
                          + transform.forward * holdDistance
                          + transform.right * holdOffset.x
                          + transform.up * holdOffset.y;

        Vector3 delta = targetPos - _heldRb.position;
        _heldRb.linearVelocity = delta * followSpeed * velocityDamping;

        Quaternion targetRot = transform.rotation * Quaternion.Euler(holdRotationOffset);
        _heldRb.MoveRotation(Quaternion.Slerp(_heldRb.rotation, targetRot,
                                               Time.fixedDeltaTime * rotationSpeed));

        _heldRb.angularVelocity = Vector3.zero;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void CheckLook()
    {
        if (_isHolding) { _lookingAtDrill = false; return; }

        Ray ray = new Ray(transform.position, transform.forward);
        _lookingAtDrill = Physics.Raycast(ray, out RaycastHit hit, pickupRange)
                          && hit.collider.CompareTag(drillTag);
    }

    private void TryPickup()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRange)) return;
        if (!hit.collider.CompareTag(drillTag)) return;

        _heldDrill = hit.collider.transform.root.gameObject;
        _heldRb = _heldDrill.GetComponentInChildren<Rigidbody>()
               ?? _heldDrill.GetComponent<Rigidbody>();

        if (_heldRb == null)
        {
            Debug.LogWarning("DrillPickup: drill has no Rigidbody — cannot pick up.");
            return;
        }

        // Find DrillBit anywhere on the drill (parent or children)
        _heldDrillBit = _heldDrill.GetComponentInChildren<DrillBit>()
                     ?? _heldDrill.GetComponent<DrillBit>();

        if (_heldDrillBit != null)
            _heldDrillBit.isEquipped = true;

        _drillHadGravity = _heldRb.useGravity;
        _heldRb.useGravity = false;
        _isHolding = true;
    }

    private void Drop()
    {
        if (_heldDrillBit != null)
        {
            _heldDrillBit.isEquipped = false;
            _heldDrillBit.SetDrilling(false);   // make sure drill stops on drop
            _heldDrillBit = null;
        }

        if (_heldRb != null)
        {
            _heldRb.useGravity = _drillHadGravity;
            _heldRb.linearVelocity = Vector3.zero;
            _heldRb.angularVelocity = Vector3.zero;
        }

        _heldDrill = null;
        _heldRb = null;
        _isHolding = false;
    }

    // ── on-screen hint ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16
        };

        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        GUI.Label(new Rect(cx - 5, cy - 5, 10, 10), "·", style);

        if (_lookingAtDrill)
            GUI.Label(new Rect(cx - 100, cy + 20, 200, 30), "[E] Pick up drill", style);
        else if (_isHolding)
            GUI.Label(new Rect(cx - 100, cy + 20, 200, 30), "[E] Drop drill", style);
    }
}