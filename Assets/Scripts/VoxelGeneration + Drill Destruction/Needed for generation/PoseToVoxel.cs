using UnityEngine;
using MarchingCubesProject;

/// <summary>
/// PoseToVoxel — attach to the same GameObject as VoxelizedModelExample
/// (the bone model). Bridges DrillTip's first-contact event to the
/// voxelization pipeline.
///
/// Flow:
///   1. DrillTip.OnTriggerEnter finds this component and calls
///      ConfirmPoseAndVoxelize(contactPoint) on first contact.
///   2. This reads the bone's static mesh (no baking needed — the bone
///      never deforms), converts it to world space, and assigns it plus
///      the contact point to VoxelizedModelExample.
///   3. Instead of voxelizing immediately, the model is handed to the
///      shared VoxelizationQueue, so multiple bones drilled around the
///      same time don't all voxelize in the same frame.
/// </summary>
[RequireComponent(typeof(VoxelizedModelExample))]
public class PoseToVoxel : MonoBehaviour
{
    [Tooltip("The voxelizer for this bone. Auto-found on this object if left empty.")]
    public VoxelizedModelExample voxelizer;

    [Tooltip("Source mesh to voxelize. Auto-found on this object (MeshFilter) if left empty.")]
    public MeshFilter sourceMeshFilter;

    [Tooltip("Shared queue that processes voxelization one model at a time. Auto-found in the scene if left empty.")]
    public VoxelizationQueue voxelizationQueue;

    private bool _confirmed = false;

    void Awake()
    {
        if (voxelizer == null)
            voxelizer = GetComponent<VoxelizedModelExample>();

        if (sourceMeshFilter == null)
            sourceMeshFilter = GetComponent<MeshFilter>();

        if (voxelizationQueue == null)
            voxelizationQueue = FindObjectOfType<VoxelizationQueue>();
    }

    /// <summary>
    /// Called by DrillTip on first contact with this bone. Bakes the world-
    /// space mesh, sets the contact point, and enqueues voxelization.
    /// Safe to call multiple times — only the first call has any effect.
    /// </summary>
    public void ConfirmPoseAndVoxelize(Vector3 contactPointWorld)
    {
        if (_confirmed) return;
        if (voxelizer == null || sourceMeshFilter == null)
        {
            Debug.LogError("[PoseToVoxel] Missing voxelizer or sourceMeshFilter.");
            return;
        }
        if (voxelizationQueue == null)
        {
            Debug.LogError("[PoseToVoxel] No VoxelizationQueue found in scene.");
            return;
        }

        _confirmed = true;

        // The bone's pose is fixed (no animation/deformation), so we can
        // bake its current world-space mesh once, directly — no
        // SkinnedMeshRenderer.BakeMesh() needed.
        voxelizer.bakedMesh = BuildWorldSpaceMesh(sourceMeshFilter);
        voxelizer.drillContactPoint = contactPointWorld;

        voxelizationQueue.Enqueue(voxelizer);

        Debug.Log($"[PoseToVoxel] Confirmed pose for {gameObject.name}, queued for voxelization at {contactPointWorld}.");
    }

    /// <summary>
    /// Returns a new Mesh with vertices transformed into world space, so
    /// VoxelizedModelExample's world-space bounds/raycast logic works
    /// correctly regardless of this object's own transform.
    /// </summary>
    private Mesh BuildWorldSpaceMesh(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        if (source == null)
        {
            Debug.LogError("[PoseToVoxel] sourceMeshFilter has no sharedMesh.");
            return null;
        }

        Vector3[] localVerts = source.vertices;
        Vector3[] worldVerts = new Vector3[localVerts.Length];
        Transform t = filter.transform;

        for (int i = 0; i < localVerts.Length; i++)
            worldVerts[i] = t.TransformPoint(localVerts[i]);

        Mesh worldMesh = new Mesh();
        worldMesh.indexFormat = source.indexFormat;
        worldMesh.SetVertices(worldVerts);
        worldMesh.SetTriangles(source.triangles, 0);
        worldMesh.RecalculateNormals();
        worldMesh.RecalculateBounds();

        return worldMesh;
    }

    /// <summary>
    /// Resets so this bone can be drilled and re-voxelized again from a
    /// fresh contact point (e.g. after VoxelizedModelExample.ResetVoxelization()).
    /// </summary>
    public void ResetConfirmation()
    {
        _confirmed = false;
    }
}
