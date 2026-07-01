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

        [Header("Marching Cubes")]
        public MARCHING_MODE mode = MARCHING_MODE.CUBES;
        public bool smoothNormals = false;

        [Header("Local Voxel Grid")]
        public int gridSize = 48;
        public float roiRadius = 0.06f;
        public float roiDepth = 0.06f;
        public float clipRadiusOffset = 0.01f;

        [Header("Voxel Boundary Colliders")]
        public CapsuleCollider[] outerColliders;
        public CapsuleCollider[] innerColliders;

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

            if (outerColliders == null || outerColliders.Length == 0)
            {
                Debug.LogError("VoxelizedModelExample: no outer colliders assigned.");
                yield break;
            }

            localBounds = new Bounds(drillContactPoint,
                new Vector3(roiRadius * 2f, roiRadius * 2f, roiDepth * 2f));
            voxels = new VoxelArray(gridSize, gridSize, gridSize);

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

            // Update clip shader using the baked skinned renderers so the
            // original model is hidden in the drilled region
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
        // Capsule region checks
        // ─────────────────────────────────────────────────────────────

        private bool IsInSolidRegion(Vector3 worldPos)
        {
            // Must be inside at least one outer collider
            bool insideAnyOuter = false;
            foreach (var col in outerColliders)
            {
                if (col != null && IsInsideCapsule(worldPos, col))
                {
                    insideAnyOuter = true;
                    break;
                }
            }
            if (!insideAnyOuter) return false;

            // Must not be inside any inner collider
            if (innerColliders != null)
                foreach (var col in innerColliders)
                    if (col != null && IsInsideCapsule(worldPos, col))
                        return false;

            return true;
        }

        private bool IsInsideCapsule(Vector3 worldPos, CapsuleCollider col)
        {
            Vector3 center = col.transform.TransformPoint(col.center);
            float halfHeight = Mathf.Max(0, col.height * 0.5f - col.radius);

            Vector3 axis = col.direction switch
            {
                0 => col.transform.right,
                1 => col.transform.up,
                _ => col.transform.forward
            };

            Vector3 point1 = center + axis * halfHeight;
            Vector3 point2 = center - axis * halfHeight;
            float radius = col.radius * col.transform.lossyScale.x;

            // Find the closest point on the capsule axis segment to worldPos
            Vector3 ab = point2 - point1;
            float t = Mathf.Clamp01(Vector3.Dot(worldPos - point1, ab) / ab.sqrMagnitude);
            Vector3 closest = point1 + t * ab;

            return Vector3.Distance(worldPos, closest) <= radius;
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

        private IEnumerator FillVoxelsAsync()
        {
            int padding = 1;
            int usableSize = gridSize - padding * 2;
            Vector3 cellSize = localBounds.size / usableSize;

            // Default all voxels to empty
            for (int x = 0; x < gridSize; x++)
                for (int y = 0; y < gridSize; y++)
                    for (int z = 0; z < gridSize; z++)
                        voxels[x, y, z] = 1f;

            int solidCount = 0;

            // Fill voxels that fall within the ROI and inside the solid
            // wall region defined by outer/inner capsule colliders.
            // Pure math — no physics cooking or timing issues.
            for (int x = 0; x < usableSize; x++)
            {
                for (int y = 0; y < usableSize; y++)
                {
                    for (int z = 0; z < usableSize; z++)
                    {
                        Vector3 voxelWorld = localBounds.min + new Vector3(
                            (x + 0.5f) * cellSize.x,
                            (y + 0.5f) * cellSize.y,
                            (z + 0.5f) * cellSize.z);

                        // ROI check — limits the voxelized area to a box
                        // around the contact point
                        Vector3 delta = voxelWorld - drillContactPoint;
                        float radialDist = Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y);
                        if (radialDist > roiRadius || Mathf.Abs(delta.z) > roiDepth)
                            continue;

                        // Capsule check — only mark voxels inside the solid
                        // wall region defined by the outer/inner colliders
                        if (IsInSolidRegion(voxelWorld))
                        {
                            voxels[x + padding, y + padding, z + padding] = -1f;
                            solidCount++;
                        }
                    }
                }

                yield return null;
            }

            Debug.Log($"FillVoxelsAsync complete. Solid voxels: {solidCount}");
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

            Vector3 innerClipCenter = drillContactPoint;

            // Cast a ray inward along Z from the contact point to find
            // where it hits the inner wall
            RaycastHit hit;
            if (Physics.Raycast(drillContactPoint, Vector3.back, out hit, 1f))
            {
                innerClipCenter = hit.point;
                Debug.Log($"Inner wall hit at: {innerClipCenter}");
            }
            else
            {
                Debug.LogWarning("Inner wall raycast missed — no second clip placed.");
            }

            foreach (var smr in _skinnedRenderers)
                foreach (var mat in smr.materials)
                {
                    if (mat.HasProperty("_ClipCenter"))
                        mat.SetVector("_ClipCenter", drillContactPoint);
                    if (mat.HasProperty("_ClipRadius"))
                        mat.SetFloat("_ClipRadius", roiRadius - clipRadiusOffset);
                    if (mat.HasProperty("_ClipCenterInner"))
                        mat.SetVector("_ClipCenterInner", innerClipCenter);
                    if (mat.HasProperty("_ClipRadiusInner"))
                        mat.SetFloat("_ClipRadiusInner", roiRadius - clipRadiusOffset);
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