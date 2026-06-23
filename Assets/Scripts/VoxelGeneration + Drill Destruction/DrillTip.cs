using UnityEngine;
using MarchingCubesProject;

/// <summary>
/// Attached to the drill tip collider (trigger).
/// Detects contact with the model and drives voxelization + drilling.
/// </summary>
public class DrillTip : MonoBehaviour
{
    [Tooltip("Assign the DrillBit component from the parent drill object.")]
    public DrillBit drillBit;

    private void OnTriggerEnter(Collider other)
    {
        var poseToVoxel = other.GetComponentInParent<PoseToVoxel>();
        if (poseToVoxel == null) return;

        var model = poseToVoxel.voxelizer;
        if (model == null || model.IsReady) return;

        Vector3 contactPoint = other.ClosestPoint(transform.position);
        poseToVoxel.ConfirmPoseAndVoxelize(contactPoint);
    }
    private void OnTriggerStay(Collider other)
    {
        var model = other.GetComponentInParent<VoxelizedModelExample>()
                 ?? other.GetComponent<VoxelizedModelExample>();

        if (model == null)
        {
            var reference = other.GetComponent<VoxelPatchReference>();
            if (reference != null)
                model = reference.model;
        }

        if (model != null && model.IsReady)
        {
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            drillBit.OnTipContact(model, contactPoint);
            Debug.Log($"Contact point: {contactPoint}, drill tip: {transform.position}");
        }
    }
}