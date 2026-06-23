using UnityEngine;

public class KneeController : MonoBehaviour
{
    public Transform tibia;
    public float flexionAngle = 0f;

    void Update()
    {
        tibia.localRotation = Quaternion.Euler(flexionAngle, 0f, 0f);
    }
}