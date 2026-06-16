using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MarchingCubesProject;

public class PoseToVoxel : MonoBehaviour
{
    [Header("References")]
    public SkinnedMeshRenderer[] skinnedMeshRenderers;
    public VoxelizedModelExample voxelizer;

    void Update()
    {
        #if Unity_editor
        if (Input.GetKeyDown(KeyCode.V))
            ConfirmPoseAndVoxelize();
        #endif
    }

    public void ConfirmPoseAndVoxelize()
    {
        Mesh bakedMesh = BakeAndCombineAll(skinnedMeshRenderers);

        GameObject temp = new GameObject("BakedSourceModel");
        MeshFilter mf = temp.AddComponent<MeshFilter>();
        mf.sharedMesh = bakedMesh;
        MeshRenderer mr = temp.AddComponent<MeshRenderer>();

        // Add the collider here explicitly so it's ready before voxelization
        MeshCollider mc = temp.AddComponent<MeshCollider>();
        mc.sharedMesh = bakedMesh;

        foreach (var smr in skinnedMeshRenderers)
            smr.gameObject.SetActive(false);

        voxelizer.sourceModel = temp;
        FindObjectOfType<VoxelizationQueue>().Enqueue(voxelizer);

        voxelizer.StartCoroutine(DestroyAfterVoxelization(temp));
    }

    private IEnumerator DestroyAfterVoxelization(GameObject temp)
    {
        // Wait until the voxelizer has actually processed this model
        yield return new WaitUntil(() => voxelizer.IsReady);
        Destroy(temp);
    }

    private Mesh BakeAndCombineAll(SkinnedMeshRenderer[] renderers)
    {
        var combineInstances = new CombineInstance[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Mesh baked = new Mesh();
            renderers[i].BakeMesh(baked);
            combineInstances[i] = new CombineInstance
            {
                mesh = baked,
                // Brings each mesh from the renderer's local space into world space
                transform = renderers[i].transform.localToWorldMatrix
            };
        }

        Mesh combined = new Mesh();
        combined.CombineMeshes(combineInstances, true, true);
        combined.RecalculateBounds();
        combined.RecalculateNormals();
        return combined;
    }
}