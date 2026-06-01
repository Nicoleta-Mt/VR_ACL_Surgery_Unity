using UnityEngine;


/// <summary>
/// Snappoint — attach to the "Snappoint" GameObject (child of Arthroscope Portal).
///
/// Rules:
///   • Snaps the endoscope ONLY while the user is actively holding it.
///   • Uses a gentle spring-pull so it feels magnetic, not teleported.
///   • Pulling hard enough (hand moves beyond breakDistance) releases it smoothly.
///   • No snap when the endoscope is just floating nearby unattended.
/// </summary>
public class Snappoint : MonoBehaviour
{
    [Header("Snap Settings")]
    [Tooltip("Radius in which the endoscope starts being attracted (while held).")]
    public float attractDistance = 0.08f;

    [Tooltip("Distance the held hand must travel from the snap centre to break free.")]
    public float breakDistance = 0.06f;

    [Tooltip("Spring strength pulling the endoscope toward the snap pose while held.")]
    [Range(1f, 40f)]
    public float springStrength = 12f;

    [Tooltip("Damping applied to the spring so it doesn't overshoot.")]
    [Range(0f, 1f)]
    public float springDamping = 0.85f;

    [Tooltip("Name of the root endoscope GameObject — must match exactly.")]
    public string endoscopeName = "endoscope";

    [Header("Optional Visuals")]
    [Tooltip("Highlight shown when the endoscope is close enough to snap (while held).")]
    public GameObject proximityIndicator;

    // ── private state ──────────────────────────────────────────────────────────
    private Transform           _endoscope;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable  _grabInteractable;

    private bool    _isSnapping  = false;   // currently being pulled into snap
    private Vector3 _snapVelocity = Vector3.zero;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        GameObject endo = GameObject.Find(endoscopeName);
        if (endo == null)
        {
            Debug.LogWarning($"[Snappoint] Could not find '{endoscopeName}'. Check the name.");
            return;
        }

        _endoscope        = endo.transform;
        _grabInteractable = endo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (_grabInteractable == null)
            Debug.LogWarning("[Snappoint] No XRGrabInteractable found on endoscope. " +
                             "Snap-while-held requires XR Interaction Toolkit.");

        if (proximityIndicator != null)
            proximityIndicator.SetActive(false);
    }

    void Update()
    {
        if (_endoscope == null) return;

        bool held = IsHeld();
        float dist = Vector3.Distance(_endoscope.position, transform.position);

        // ── proximity indicator (only while held) ─────────────────────────────
        if (proximityIndicator != null)
            proximityIndicator.SetActive(held && !_isSnapping && dist < attractDistance * 1.5f);

        // ── only act while the endoscope is being held ────────────────────────
        if (!held)
        {
            // Released outside snap → just stop attracting
            if (_isSnapping)
                ExitSnap();
            return;
        }

        // ── enter snap zone ───────────────────────────────────────────────────
        if (!_isSnapping && dist < attractDistance)
            EnterSnap();

        // ── spring pull toward snap pose while snapping ───────────────────────
        if (_isSnapping)
        {
            // Smooth-damp position
            _endoscope.position = Vector3.SmoothDamp(
                _endoscope.position,
                transform.position,
                ref _snapVelocity,
                1f / springStrength,          // approx time to reach target
                Mathf.Infinity,
                Time.deltaTime);

            // Gentle slerp for rotation
            _endoscope.rotation = Quaternion.Slerp(
                _endoscope.rotation,
                transform.rotation,
                Time.deltaTime * springStrength * springDamping);

            // ── break-free check: hand pulled beyond breakDistance ────────────
            float snapDist = Vector3.Distance(_endoscope.position, transform.position);
            if (snapDist > breakDistance)
            {
                ExitSnap();
                Debug.Log("[Snappoint] endoscope pulled free.");
            }
        }
    }

    // ── state helpers ──────────────────────────────────────────────────────────

    private void EnterSnap()
    {
        _isSnapping   = true;
        _snapVelocity = Vector3.zero;

        if (proximityIndicator != null)
            proximityIndicator.SetActive(false);

        Debug.Log("[Snappoint] endoscope entering snap — spring engaged.");
    }

    private void ExitSnap()
    {
        _isSnapping = false;
        Debug.Log("[Snappoint] endoscope left snap zone.");
    }

    private bool IsHeld()
    {
        if (_grabInteractable == null) return false;
        return _grabInteractable.isSelected;
    }

    // ── editor gizmos ─────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Attract zone
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, attractDistance);

        // Break-free zone (inner — must pull past this)
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, breakDistance);
    }
}
