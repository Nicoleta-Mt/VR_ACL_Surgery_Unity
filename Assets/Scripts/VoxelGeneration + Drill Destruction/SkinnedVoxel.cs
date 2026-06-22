using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MarchingCubesProject;

/// <summary>
/// Bakes the skinned mesh at the current pose and hands it to VoxelizedModelExample
/// for localized voxelization around the drill contact point.
/// </summary>
public class PoseToVoxel : MonoBehaviour
{
    private bool _isPending = false;
    [Header("References")]
    public SkinnedMeshRenderer[] skinnedMeshRenderers;
    public VoxelizedModelExample voxelizer;

    /// <summary>
    /// Called by DrillTip on first contact. contactPoint is the world-space
    /// position where the drill touched the model.
    /// </summary>
    public void ConfirmPoseAndVoxelize(Vector3 contactPoint)
    {
        if (voxelizer.IsReady || _isPending) return;
        _isPending = true;
        // Freeze the animator so the pose doesn't shift during baking
        var animator = skinnedMeshRenderers[0].GetComponentInParent<Animator>();
        if (animator != null)
        {
            voxelizer.SetAnimator(animator);
            animator.enabled = false;
        }

        // Bake and combine all skinned renderers into one static mesh
        Mesh bakedMesh = BakeAndCombineAll(skinnedMeshRenderers);

        // Pass everything the voxelizer needs
        voxelizer.bakedMesh = bakedMesh;
        voxelizer.drillContactPoint = contactPoint;
        voxelizer.SetSkinnedRenderers(skinnedMeshRenderers);

        FindObjectOfType<VoxelizationQueue>().Enqueue(voxelizer);
    }

    private Mesh BakeAndCombineAll(SkinnedMeshRenderer[] renderers)
    {
        var combineInstances = new CombineInstance[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Mesh baked = new Mesh();
            renderers[i].BakeMesh(baked);
            Debug.Log($"Renderer {i}: {renderers[i].name}, verts: {baked.vertexCount}");

            // BakeMesh outputs verts in the renderer's local space.
            // We need world space so the temp collider (at origin) lines up
            // with the ROI bounds which are in world space.
            combineInstances[i] = new CombineInstance
            {
                mesh = baked,
                transform = renderers[i].transform.localToWorldMatrix
            };
        }

        Mesh combined = new Mesh();
        // mergeSubMeshes: true, useMatrices: true — applies localToWorldMatrix
        combined.CombineMeshes(combineInstances, true, true);
        combined.RecalculateBounds();
        combined.RecalculateNormals();

        // Log so we can verify bounds are where the model actually is
        Debug.Log($"Combined baked mesh bounds: {combined.bounds}, center: {combined.bounds.center}");

        return combined;
    }
}