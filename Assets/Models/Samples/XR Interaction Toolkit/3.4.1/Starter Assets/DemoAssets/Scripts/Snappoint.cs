using UnityEngine;


/// <summary>
/// Snappoint — attach to the "Snappoint" GameObject (child of Arthroscope Portal).
/// Works with XR Grab Interactable + Rigidbody in VR.
/// </summary>
public class Snappoint : MonoBehaviour
{
    [Header("Snap Settings")]
    public float attractDistance = 0.08f;
    public float breakDistance = 0.10f;
    public string endoscopeName = "endoscope";

    [Header("Optional Visuals")]
    public GameObject proximityIndicator;

    // ── private ────────────────────────────────────────────────────────────────
    private Transform _endoscope;
    private Rigidbody _rb;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
    private bool _isSnapped;

    void Start()
    {
        GameObject endo = GameObject.Find(endoscopeName);
        if (endo == null)
        {
            Debug.LogWarning("[Snappoint] Cannot find '" + endoscopeName + "'");
            return;
        }

        _endoscope = endo.transform;
        _rb = endo.GetComponent<Rigidbody>();
        _grab = endo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (_grab == null)
            Debug.LogWarning("[Snappoint] No XRGrabInteractable on endoscope.");
        if (_rb == null)
            Debug.LogWarning("[Snappoint] No Rigidbody on endoscope.");

        if (proximityIndicator != null)
            proximityIndicator.SetActive(false);
    }

    void FixedUpdate()
    {
        if (_endoscope == null || _rb == null || _grab == null) return;

        bool held = _grab.isSelected;
        float dist = Vector3.Distance(_endoscope.position, transform.position);

        // Proximity indicator
        if (proximityIndicator != null)
            proximityIndicator.SetActive(held && !_isSnapped && dist < attractDistance * 1.5f);

        // Only act while held
        if (!held)
        {
            if (_isSnapped) ExitSnap();
            return;
        }

        // Enter snap zone
        if (!_isSnapped && dist < attractDistance)
            EnterSnap();

        // While snapped — move rb directly to snap pose
        if (_isSnapped)
        {
            // Move via Rigidbody so XR physics doesn't fight us
            _rb.MovePosition(Vector3.Lerp(_endoscope.position, transform.position, Time.fixedDeltaTime * 15f));
            _rb.MoveRotation(Quaternion.Slerp(_endoscope.rotation, transform.rotation, Time.fixedDeltaTime * 15f));

            // Dampen velocity so it doesn't jitter
            _rb.linearVelocity = _rb.linearVelocity * 0.1f;
            _rb.angularVelocity = _rb.angularVelocity * 0.1f;

            // Break free if pulled far enough
            if (dist > breakDistance)
                ExitSnap();
        }
    }

    void EnterSnap()
    {
        _isSnapped = true;
        if (proximityIndicator != null)
            proximityIndicator.SetActive(false);
        Debug.Log("[Snappoint] Snapped.");
    }

    void ExitSnap()
    {
        _isSnapped = false;
        Debug.Log("[Snappoint] Released.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, attractDistance);

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, breakDistance);
    }
}