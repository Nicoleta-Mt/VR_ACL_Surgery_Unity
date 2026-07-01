using UnityEngine;

public class ArthroscopeTipBoundary : MonoBehaviour
{
    [Header("Boundary Sphere")]
    public Transform boundaryCenter;
    public float boundaryRadius = 0.05f;

    [Header("Tip Reference")]
    public Transform tip;

    void LateUpdate()
    {
        if (boundaryCenter == null || tip == null) return;

        Vector3 fromCenter = tip.position - boundaryCenter.position;
        float dist = fromCenter.magnitude;

        if (dist > boundaryRadius)
        {
            Vector3 clampedTipPos = boundaryCenter.position + fromCenter.normalized * boundaryRadius;
            Vector3 correction = clampedTipPos - tip.position;

            // push the whole arthroscope (parent) back by the same correction
            transform.position += correction;
        }
    }

    void OnDrawGizmos()
    {
        if (boundaryCenter == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(boundaryCenter.position, boundaryRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(boundaryCenter.position, boundaryRadius);
    }
} // <-- class closes here now, after both methods