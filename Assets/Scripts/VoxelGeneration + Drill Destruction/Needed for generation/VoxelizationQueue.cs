using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MarchingCubesProject;

/// <summary>
/// VoxelizationQueue — single shared queue that processes one model's
/// VoxelizeAsync() at a time, so multiple bones being drilled around the
/// same moment don't all voxelize in the same frame and stall.
///
/// Models are enqueued by PoseToVoxel.ConfirmPoseAndVoxelize() once their
/// bakedMesh + drillContactPoint have been set.
/// </summary>
public class VoxelizationQueue : MonoBehaviour
{
    private Queue<VoxelizedModelExample> pending = new Queue<VoxelizedModelExample>();
    private bool isRunning = false;

    public void Enqueue(VoxelizedModelExample model)
    {
        if (model == null)
        {
            Debug.LogWarning("[VoxelizationQueue] Tried to enqueue a null model.");
            return;
        }

        pending.Enqueue(model);
        if (!isRunning)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isRunning = true;
        while (pending.Count > 0)
        {
            VoxelizedModelExample next = pending.Dequeue();

            if (next == null)
            {
                Debug.LogWarning("[VoxelizationQueue] Skipped a null/destroyed model in queue.");
                continue;
            }

            Debug.Log($"[VoxelizationQueue] Voxelizing: {next.gameObject.name}");
            yield return next.VoxelizeAsync();
            Debug.Log($"[VoxelizationQueue] Finished: {next.gameObject.name} (IsReady={next.IsReady})");
        }
        isRunning = false;
    }
}
