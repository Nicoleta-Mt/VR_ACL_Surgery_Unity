using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MarchingCubesProject;

public class VoxelizationQueue : MonoBehaviour
{
    private Queue<VoxelizedModelExample> pending = new Queue<VoxelizedModelExample>();
    private bool isRunning = false;

    public void Enqueue(VoxelizedModelExample model)
    {
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
            string modelName = next.sourceModel != null ? next.sourceModel.name : "Unknown";
            Debug.Log($"Voxelizing: {modelName}");
            yield return next.VoxelizeAsync();
        }
        isRunning = false;
    }
}