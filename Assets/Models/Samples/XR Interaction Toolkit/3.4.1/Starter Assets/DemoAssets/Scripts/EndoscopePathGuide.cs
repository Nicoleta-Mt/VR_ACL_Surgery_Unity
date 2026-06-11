using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// EndoscopePathGuide — attach to the "arthroscope" GameObject.
///
/// Setup:
///   1. Attach this script to the arthroscope GameObject.
///   2. Create empty GameObjects inside the knee as waypoints (WP1, WP2, WP3).
///   3. Drag waypoints into the Waypoints list in the Inspector in order.
///   4. The arthroscope is constrained to the path when pushed in.
///   5. A wireframe tunnel appears when the arthroscope is near the path.
/// </summary>
public class EndoscopePathGuide : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The knee 3D model GameObject.")]
    public GameObject knee;

    [Header("Waypoints")]
    [Tooltip("Empty GameObjects inside the knee, placed in order from entry to deepest point.")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Path Settings")]
    [Tooltip("How smoothly the arthroscope follows the path.")]
    [Range(1f, 20f)]
    public float followSpeed = 10f;

    [Tooltip("Distance from the path before constraining starts.")]
    public float constrainDistance = 0.15f;

    [Header("Tunnel Settings")]
    [Tooltip("Radius of the wireframe tunnel.")]
    public float tunnelRadius = 0.02f;

    [Tooltip("How many segments the tunnel cylinder has.")]
    [Range(4, 16)]
    public int tunnelSegments = 8;

    [Tooltip("How many rings per waypoint segment.")]
    [Range(2, 10)]
    public int tunnelRings = 4;

    [Tooltip("Tunnel colour.")]
    public Color tunnelColor = new Color(0f, 0.8f, 1f, 0.3f);

    [Tooltip("Distance from path at which tunnel becomes visible.")]
    public float tunnelVisibilityDistance = 0.3f;

    // ── private ────────────────────────────────────────────────────────────────
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
    private float              _pathProgress;
    private List<GameObject>   _tunnelRings = new List<GameObject>();
    private Material           _tunnelMat;
    private bool               _tunnelVisible;

    void Start()
    {
        _grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (_grab == null)
            Debug.LogWarning("[EndoscopePathGuide] No XRGrabInteractable found.");
        if (waypoints.Count < 2)
            Debug.LogWarning("[EndoscopePathGuide] Add at least 2 waypoints.");

        BuildTunnel();
        SetTunnelVisible(false);
    }

    void Update()
    {
        if (waypoints.Count < 2) return;

        bool held = _grab != null && _grab.isSelected;

        // Find closest point on path
        float closestT   = GetClosestPathT(transform.position);
        float distToPath = Vector3.Distance(transform.position, GetPathPosition(closestT));

        // Show/hide tunnel based on proximity
        bool shouldShow = distToPath < tunnelVisibilityDistance;
        if (shouldShow != _tunnelVisible)
            SetTunnelVisible(shouldShow);

        // Constrain arthroscope to path while held and near
        if (held && distToPath < constrainDistance)
        {
            // Project current position onto path
            Vector3    targetPos = GetPathPosition(closestT);
            Quaternion targetRot = GetPathRotation(closestT);

            // Allow movement along the path axis only
            Vector3 pathForward  = GetPathForward(closestT);
            Vector3 toTarget     = targetPos - transform.position;
            Vector3 alongPath    = Vector3.Project(transform.position - targetPos, pathForward);

            // Constrained position: snap to path laterally, keep depth along path
            Vector3 constrained = targetPos + alongPath;

            // Gentle lateral nudge only — don't fight the hand
            float blend = Mathf.Clamp01(distToPath / constrainDistance);
            float gentleStrength = (1f - blend) * 0.05f;

            transform.position = Vector3.Lerp(transform.position, constrained, gentleStrength);

            // Update path progress for reference
            _pathProgress = closestT;
        }
    }

    // ── path math ──────────────────────────────────────────────────────────────

    Vector3 GetPathPosition(float t)
    {
        if (waypoints.Count == 1) return waypoints[0].position;
        float scaled = t * (waypoints.Count - 1);
        int   a      = Mathf.FloorToInt(scaled);
        int   b      = Mathf.Min(a + 1, waypoints.Count - 1);
        return Vector3.Lerp(waypoints[a].position, waypoints[b].position, scaled - a);
    }

    Quaternion GetPathRotation(float t)
    {
        if (waypoints.Count == 1) return waypoints[0].rotation;
        float scaled = t * (waypoints.Count - 1);
        int   a      = Mathf.FloorToInt(scaled);
        int   b      = Mathf.Min(a + 1, waypoints.Count - 1);
        return Quaternion.Slerp(waypoints[a].rotation, waypoints[b].rotation, scaled - a);
    }

    Vector3 GetPathForward(float t)
    {
        float delta = 0.01f;
        Vector3 a   = GetPathPosition(Mathf.Clamp01(t - delta));
        Vector3 b   = GetPathPosition(Mathf.Clamp01(t + delta));
        return (b - a).normalized;
    }

    float GetClosestPathT(Vector3 pos)
    {
        float bestT    = 0f;
        float bestDist = float.MaxValue;
        int   steps    = (waypoints.Count - 1) * 20;

        for (int i = 0; i <= steps; i++)
        {
            float t    = (float)i / steps;
            float dist = Vector3.Distance(pos, GetPathPosition(t));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestT    = t;
            }
        }
        return bestT;
    }

    // ── tunnel construction ────────────────────────────────────────────────────

    void BuildTunnel()
    {
        if (waypoints.Count < 2) return;

        // Create unlit transparent material
        _tunnelMat = new Material(Shader.Find("Unlit/Color"));
        _tunnelMat.color = tunnelColor;

        int totalRings = (waypoints.Count - 1) * tunnelRings + 1;

        for (int r = 0; r < totalRings; r++)
        {
            float t         = (float)r / (totalRings - 1);
            Vector3 center  = GetPathPosition(t);
            Vector3 forward = GetPathForward(t);
            Quaternion rot  = Quaternion.LookRotation(forward);

            // Draw a ring of line segments
            for (int s = 0; s < tunnelSegments; s++)
            {
                float angleA = (float)s       / tunnelSegments * Mathf.PI * 2f;
                float angleB = (float)(s + 1) / tunnelSegments * Mathf.PI * 2f;

                Vector3 pA = center + rot * new Vector3(Mathf.Cos(angleA), Mathf.Sin(angleA), 0) * tunnelRadius;
                Vector3 pB = center + rot * new Vector3(Mathf.Cos(angleB), Mathf.Sin(angleB), 0) * tunnelRadius;

                GameObject seg = CreateLineSeg("TunnelSeg", pA, pB);
                _tunnelRings.Add(seg);
            }

            // Longitudinal lines every other ring
            if (r < totalRings - 1)
            {
                float tNext     = (float)(r + 1) / (totalRings - 1);
                Vector3 cNext   = GetPathPosition(tNext);
                Vector3 fNext   = GetPathForward(tNext);
                Quaternion rNext = Quaternion.LookRotation(fNext);

                for (int s = 0; s < tunnelSegments; s += 2)
                {
                    float angle = (float)s / tunnelSegments * Mathf.PI * 2f;
                    Vector3 pA  = center + rot   * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * tunnelRadius;
                    Vector3 pB  = cNext  + rNext * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * tunnelRadius;

                    GameObject seg = CreateLineSeg("TunnelLong", pA, pB);
                    _tunnelRings.Add(seg);
                }
            }
        }
    }

    GameObject CreateLineSeg(string name, Vector3 start, Vector3 end)
    {
        GameObject go  = new GameObject(name);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material        = _tunnelMat;
        lr.startWidth      = 0.001f;
        lr.endWidth        = 0.001f;
        lr.positionCount   = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.useWorldSpace   = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows  = false;
        return go;
    }

    void SetTunnelVisible(bool visible)
    {
        _tunnelVisible = visible;
        foreach (GameObject seg in _tunnelRings)
            if (seg != null) seg.SetActive(visible);
    }

    // ── editor gizmos ──────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(waypoints[i].position, 0.005f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(waypoints[i].position, waypoints[i].forward * 0.02f);

            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.8f, 0.6f);
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
