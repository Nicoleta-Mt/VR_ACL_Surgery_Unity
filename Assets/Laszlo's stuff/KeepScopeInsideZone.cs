using UnityEngine;

public class KeepScopeInsideZone : MonoBehaviour
{
    public Collider allowedZone;

    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;

    void Start()
    {
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    void LateUpdate()
    {
        if (allowedZone.bounds.Contains(transform.position))
        {
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
        }
        else
        {
            transform.position = lastValidPosition;
            transform.rotation = lastValidRotation;
        }
    }
}