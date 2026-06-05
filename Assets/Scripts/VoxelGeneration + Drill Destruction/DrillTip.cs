using UnityEngine;
using MarchingCubesProject;

public class DrillTip : MonoBehaviour
{
    private void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.3f);
        foreach (var h in hits)
            Debug.Log("OverlapSphere hit: " + h.gameObject.name);
    }
    [Tooltip("Assign the DrillBit component from the parent drill object.")]
    public DrillBit drillBit;

    private void OnTriggerStay(Collider other)
    {
        var model = other.GetComponentInParent<VoxelizedModelExample>()
                 ?? other.GetComponent<VoxelizedModelExample>();

        if (model != null)
            drillBit.OnTipContact(model, transform.position);
    }

    private void OnTriggerExit(Collider other)
    {
        var model = other.GetComponentInParent<VoxelizedModelExample>()
                 ?? other.GetComponent<VoxelizedModelExample>();

        if (model != null)
            drillBit.OnTipExit();
    }
}