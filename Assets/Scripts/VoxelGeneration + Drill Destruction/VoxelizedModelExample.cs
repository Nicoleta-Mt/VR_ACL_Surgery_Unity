using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Common.Unity.Drawing;

namespace MarchingCubesProject
{
    public enum MARCHING_MODE { CUBES, TETRAHEDRON };

    public class VoxelizedModelExample : MonoBehaviour
    {
        [Header("Materials")]
        public Material material;
        public Material interiorMaterial;

        [Header("Marching Cubes")]
        public MARCHING_MODE mode = MARCHING_MODE.CUBES;
        public bool smoothNormals = false;

        [Header("Local Voxel Grid")]
        public int gridSize = 24;
        public float roiRadius = 0.08f;
        public float clipRadiusOffset = 0.01f;

        public bool IsReady { get; private set; } = false;

        [HideInInspector] public Vector3 drillContactPoint;
        [HideInInspector] public Mesh bakedMesh;

        private SkinnedMeshRenderer[] _skinnedRenderers;
        private Animator _animator;

        private VoxelArray voxels;
        private Bounds localBounds;
        private MeshFilter drillableMeshFilter;
        private Transform voxelMeshTransform;
        private List<GameObject> meshes = new List<GameObject>();

        // ─────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────

        public void SetSkinnedRenderers(SkinnedMeshRenderer[] renderers)
        {
            _skinnedRenderers = renderers;
        }

        public void SetAnimator(Animator animator)
        {
            _animator = animator;
        }

        public IEnumerator VoxelizeAsync()
        {
            IsReady = false;

            if (bakedMesh == null)
            {
                Debug.LogError("VoxelizedModelExample: bakedMesh is null.");
                yield break;
            }

            localBounds = new Bounds(drillContactPoint, Vector3.one * roiRadius * 2f);
            voxels = new VoxelArray(gridSize, gridSize, gridSize);

            yield return new WaitForFixedUpdate();
            yield return FillVoxelsAsync();

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();

            Marching marching = mode == MARCHING_MODE.TETRAHEDRON
                ? (Marching)new MarchingTertrahedron()
                : new MarchingCubes();
            marching.Surface = 0.0f;
            marching.Generate(voxels.Voxels, verts, indices);

            if (verts.Count == 0)
            {
                Debug.LogWarning("Marching cubes produced no geometry.");
                yield break;
            }

            RescaleVerts(verts);

            GameObject go = CreateMeshObject("VoxelPatch_Outer", verts, normals, indices, material);
            drillableMeshFilter = go.GetComponent<MeshFilter>();
            voxelMeshTransform = go.transform;

            yield return null;

            var col = go.AddComponent<MeshCollider>();
            col.convex = true;
            col.sharedMesh = null;
            col.sharedMesh = drillableMeshFilter.mesh;
            Debug.Log($"MeshCollider assigned. Verts: {drillableMeshFilter.mesh.vertexCount}");

            if (interiorMaterial != null)
                CreateMeshObject("VoxelPatch_Interior", verts, normals, indices, interiorMaterial, flipNormals: false);

            UpdateClipShader();

            IsReady = true;
            Debug.Log($"Voxelization complete. localBounds.min={localBounds.min} center={localBounds.center}");
        }

        public void ResetVoxelization()
        {
            foreach (var go in meshes)
                Destroy(go);

            meshes.Clear();
            voxels = null;
            bakedMesh = null;
            IsReady = false;

            if (_skinnedRenderers != null)
                foreach (var smr in _skinnedRenderers)
                    smr.enabled = true;

            if (_animator != null)
                _animator.enabled = true;
        }

        // ─────────────────────────────────────────────────────────────
        // Drilling
        // ─────────────────────────────────────────────────────────────

        public void Drill(Vector3 worldPos, float worldRadius)
        {
            if (!IsReady) return;

            int padding = 1;
            int usableSize = gridSize - padding * 2;
            Vector3 cellSize = localBounds.size / usableSize;

            bool changed = false;
            for (int x = 0; x < usableSize; x++)
                for (int y = 0; y < usableSize; y++)
                    for (int z = 0; z < usableSize; z++)
                    {
                        if (voxels[x + padding, y + padding, z + padding] >= 0f)
                            continue;

                        // localBounds.min is already world-space, so this is world-space too
                        Vector3 voxelWorld = localBounds.min + new Vector3(
                            (x + 0.5f) * cellSize.x,
                            (y + 0.5f) * cellSize.y,
                            (z + 0.5f) * cellSize.z);

                        if (Vector3.Distance(voxelWorld, worldPos) <= worldRadius)
                        {
                            voxels[x + padding, y + padding, z + padding] = 1.0f;
                            changed = true;
                        }
                    }

            if (changed)
                StartCoroutine(RegenerateMesh());
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

        private IEnumerator FillVoxelsAsync()
        {
            int padding = 1;
            int usableSize = gridSize - padding * 2;
            Vector3 cellSize = localBounds.size / usableSize;

            // Default all to empty
            for (int x = 0; x < gridSize; x++)
                for (int y = 0; y < gridSize; y++)
                    for (int z = 0; z < gridSize; z++)
                        voxels[x, y, z] = 1f;

            int tempLayer = LayerMask.NameToLayer("TempVoxel");
            if (tempLayer == -1)
            {
                Debug.LogError("TempVoxel layer not found.");
                yield break;
            }

            GameObject temp = new GameObject("_TempCollider");
            temp.layer = tempLayer;
            temp.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
            temp.AddComponent<MeshCollider>().sharedMesh = bakedMesh;
            var rb = temp.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            yield return new WaitForFixedUpdate();

            int layerMask = 1 << tempLayer;
            int solidCount = 0;

            for (int x = 0; x < usableSize; x++)
            {
                for (int z = 0; z < usableSize; z++)
                {
                    // World position of this column — matches Drill() calculation exactly
                    float wx = localBounds.min.x + (x + 0.5f) * cellSize.x;
                    float wz = localBounds.min.z + (z + 0.5f) * cellSize.z;

                    Vector3 rayOrigin = new Vector3(wx, localBounds.max.y + 1f, wz);
                    RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down,
                        localBounds.size.y + 2f, layerMask);

                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    bool inside = false;
                    int hitIndex = 0;

                    for (int y = usableSize - 1; y >= 0; y--)
                    {
                        // World Y of this voxel — matches Drill() calculation exactly
                        float wy = localBounds.min.y + (y + 0.5f) * cellSize.y;

                        while (hitIndex < hits.Length && hits[hitIndex].point.y > wy)
                        {
                            inside = !inside;
                            hitIndex++;
                        }

                        Vector3 cellCenter = new Vector3(wx, wy, wz);
                        if (Vector3.Distance(cellCenter, drillContactPoint) > roiRadius)
                            continue;

                        if (inside)
                        {
                            voxels[x + padding, y + padding, z + padding] = -1f;
                            solidCount++;
                        }
                    }
                }

                yield return null;
            }

            Debug.Log($"FillVoxelsAsync complete. Solid voxels: {solidCount}");
            Destroy(temp);
        }

        private void RescaleVerts(List<Vector3> verts)
        {
            int padding = 1;
            int usableSize = gridSize - padding * 2;

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                v -= new Vector3(padding, padding, padding);
                v.x = (v.x / usableSize) * localBounds.size.x;
                v.y = (v.y / usableSize) * localBounds.size.y;
                v.z = (v.z / usableSize) * localBounds.size.z;
                // No localBounds.min offset — the GameObject is positioned
                // at localBounds.min so verts are in its local space
                verts[i] = v;
            }
        }

        private GameObject CreateMeshObject(string objName, List<Vector3> verts,
            List<Vector3> normals, List<int> indices, Material mat, bool flipNormals = false)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);

            if (flipNormals)
            {
                var flipped = new int[indices.Count];
                for (int i = 0; i < indices.Count; i += 3)
                {
                    flipped[i] = indices[i + 2];
                    flipped[i + 1] = indices[i + 1];
                    flipped[i + 2] = indices[i];
                }
                mesh.SetTriangles(flipped, 0);
            }
            else
            {
                mesh.SetTriangles(indices, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform);
            // Verts are in local space starting at (0,0,0), so place
            // the GameObject at localBounds.min to bring them to world space
            go.transform.position = localBounds.min;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().material = mat;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var reference = go.AddComponent<VoxelPatchReference>();
            reference.model = this;

            meshes.Add(go);
            return go;
        }
        private bool _isRegenerating = false;
        private IEnumerator RegenerateMesh()
        {
            if (_isRegenerating) yield break;
            _isRegenerating = true;
            Marching marching = mode == MARCHING_MODE.TETRAHEDRON
                ? (Marching)new MarchingTertrahedron()
                : new MarchingCubes();
            marching.Surface = 0.0f;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();

            marching.Generate(voxels.Voxels, verts, indices);
            RescaleVerts(verts);

            UpdateMesh(drillableMeshFilter, verts, indices, flipNormals: false);

            // Interior patch is a sibling at scene root, not a child —
            // find it by name in the meshes list
            foreach (var go in meshes)
            {
                if (go != null && go.name == "VoxelPatch_Interior")
                {
                    var interiorFilter = go.GetComponent<MeshFilter>();
                    if (interiorFilter != null)
                        UpdateMesh(interiorFilter, verts, indices, flipNormals: false);
                    break;
                }
            }

            yield return null;

            var col = drillableMeshFilter.GetComponent<MeshCollider>();
            if (col != null)
            {
                col.sharedMesh = null;
                col.sharedMesh = drillableMeshFilter.mesh;
            }
            _isRegenerating = false;

        }

        private void UpdateMesh(MeshFilter mf, List<Vector3> verts, List<int> indices, bool flipNormals)
        {
            Mesh mesh = mf.mesh;
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);

            if (flipNormals)
            {
                var flipped = new int[indices.Count];
                for (int i = 0; i < indices.Count; i += 3)
                {
                    flipped[i] = indices[i + 2];
                    flipped[i + 1] = indices[i + 1];
                    flipped[i + 2] = indices[i];
                }
                mesh.SetTriangles(flipped, 0);
            }
            else
            {
                mesh.SetTriangles(indices, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void UpdateClipShader()
        {
            if (_skinnedRenderers == null) return;
            foreach (var smr in _skinnedRenderers)
                foreach (var mat in smr.materials)
                {
                    if (mat.HasProperty("_ClipCenter"))
                        mat.SetVector("_ClipCenter", drillContactPoint);
                    if (mat.HasProperty("_ClipRadius"))
                        mat.SetFloat("_ClipRadius", roiRadius - clipRadiusOffset);
                }
        }

        public void RestoreSkinnedMesh()
        {
            if (_skinnedRenderers != null)
                foreach (var smr in _skinnedRenderers)
                    smr.enabled = true;

            if (_animator != null)
                _animator.enabled = true;

            foreach (var go in meshes)
                Destroy(go);

            meshes.Clear();
            IsReady = false;
        }
    }
}