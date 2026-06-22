using UnityEngine;
using MarchingCubesProject;

public class DrillResetTest : MonoBehaviour
{
    public VoxelizedModelExample voxelizer;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            voxelizer.ResetVoxelization();
    }
}