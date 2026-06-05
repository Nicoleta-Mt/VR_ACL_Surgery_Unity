using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Common.Unity.Drawing;

namespace MarchingCubesProject
{
    public enum MARCHING_MODE { CUBES, TETRAHEDRON };

    public class VoxelizedModelExample : MonoBehaviour
    {
        public Material material;
        public MARCHING_MODE mode = MARCHING_MODE.CUBES;
        public bool smoothNormals = false;
        public bool drawNormals = false;

        [Header("Voxelization")]
        public GameObject sourceModel;
        public int width = 32;
        public int height = 32;
        public int depth = 32;

        // Persistent references needed for drilling
        private VoxelArray voxels;
        private Bounds modelBounds;
        private MeshFilter drillableMeshFilter;

        private List<GameObject> meshes = new List<GameObject>();
        private NormalRenderer normalRenderer;

        void Start()
        {
            if (sourceModel == null) { Debug.LogError("Assign a sourceModel!"); return; }

            Marching marching = mode == MARCHING_MODE.TETRAHEDRON
                ? (Marching)new MarchingTertrahedron()
                : new MarchingCubes();

            marching.Surface = 0.0f;

            voxels = new VoxelArray(width, height, depth);
            modelBounds = GetCompoundBounds(sourceModel);

            FillVoxelsFromModel(voxels, modelBounds);
            sourceModel.SetActive(false);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();

            marching.Generate(voxels.Voxels, verts, indices);

            if (smoothNormals)
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 p = verts[i];
                    float u = p.x / (width - 1.0f);
                    float v = p.y / (height - 1.0f);
                    float w = p.z / (depth - 1.0f);
                    normals.Add(voxels.GetNormal(u, v, w));
                }

                normalRenderer = new NormalRenderer();
                normalRenderer.DefaultColor = Color.red;
                normalRenderer.Length = 0.25f;
                normalRenderer.Load(verts, normals);
            }

            RescaleVerts(verts, modelBounds);

            // Create the mesh and store a reference to it for drilling
            GameObject go = CreateMesh32(verts, normals, indices, modelBounds.min);
            drillableMeshFilter = go.GetComponent<MeshFilter>();
           
            // Add a MeshCollider so the drill raycast can hit it
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = drillableMeshFilter.mesh;
        }

        private void FillVoxelsFromModel(VoxelArray voxels, Bounds bounds)
        {
            int padding = 1;
            int usableW = width - padding * 2;
            int usableH = height - padding * 2;
            int usableD = depth - padding * 2;

            Vector3 boundsMin = bounds.min;
            Vector3 boundsSize = bounds.size;

            var cellSize = new Vector3(
                boundsSize.x / usableW,
                boundsSize.y / usableH,
                boundsSize.z / usableD
            );
            Vector3 halfCell = cellSize * 0.5f;

            bool addedCollider = EnsureMeshCollider(sourceModel);

            // Default everything to air
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    for (int z = 0; z < depth; z++)
                        voxels[x, y, z] = 1.0f;

            // Fill only the inner region, leaving the outer padding as air
            for (int x = 0; x < usableW; x++)
                for (int y = 0; y < usableH; y++)
                    for (int z = 0; z < usableD; z++)
                    {
                        Vector3 worldPos = boundsMin + new Vector3(
                            (x + 0.5f) * cellSize.x,
                            (y + 0.5f) * cellSize.y,
                            (z + 0.5f) * cellSize.z
                        );

                        bool inside = Physics.CheckBox(worldPos, halfCell * 0.99f);
                        voxels[x + padding, y + padding, z + padding] = inside ? -1.0f : 1.0f;
                    }

            if (addedCollider)
                RemoveTemporaryColliders(sourceModel);
        }

        private void RescaleVerts(List<Vector3> verts, Bounds bounds)
        {
            int padding = 1;
            int usableW = width - padding * 2;
            int usableH = height - padding * 2;
            int usableD = depth - padding * 2;

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                v -= new Vector3(padding, padding, padding);
                v.x = (v.x / usableW) * bounds.size.x;
                v.y = (v.y / usableH) * bounds.size.y;
                v.z = (v.z / usableD) * bounds.size.z;
                verts[i] = v;
            }
        }


        public void Drill(Vector3 worldPos, float worldRadius)
        {
            Debug.Log($"Drill() called at worldPos: {worldPos}, modelBounds.min: {modelBounds.min}, modelBounds.size: {modelBounds.size}");
           
            int padding = 1;
            int usableW = width - padding * 2;
            int usableH = height - padding * 2;
            int usableD = depth - padding * 2;

            float voxelRadius = (worldRadius / modelBounds.size.x) * usableW;

            Vector3 localPos = worldPos - modelBounds.min;
            float vx = (localPos.x / modelBounds.size.x) * usableW + padding;
            float vy = (localPos.y / modelBounds.size.y) * usableH + padding;
            float vz = (localPos.z / modelBounds.size.z) * usableD + padding;

            int minX = Mathf.Max(0, Mathf.FloorToInt(vx - voxelRadius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(vx + voxelRadius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(vy - voxelRadius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(vy + voxelRadius));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(vz - voxelRadius));
            int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt(vz + voxelRadius));

            bool changed = false;

            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float dx = x - vx, dy = y - vy, dz = z - vz;
                        if (dx * dx + dy * dy + dz * dz <= voxelRadius * voxelRadius)
                        {
                            if (voxels[x, y, z] < 0f)
                            {
                                voxels[x, y, z] = 1.0f;
                                changed = true;
                            }
                        }
                    }

            if (changed)
                RegenerateMesh();
        }

        private void RegenerateMesh()
        {
            Marching marching = mode == MARCHING_MODE.TETRAHEDRON
                ? (Marching)new MarchingTertrahedron()
                : new MarchingCubes();
            marching.Surface = 0.0f;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();

            marching.Generate(voxels.Voxels, verts, indices);

            RescaleVerts(verts, modelBounds);

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            drillableMeshFilter.mesh = mesh;

            // Force the collider to fully reset rather than just updating sharedMesh
            var col = drillableMeshFilter.GetComponent<MeshCollider>();
            if (col != null)
            {
                col.sharedMesh = null;
                col.sharedMesh = mesh;
            }
        }

        private bool EnsureMeshCollider(GameObject root)
        {
            bool added = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    added = true;
                }
            }
            return added;
        }

        private void RemoveTemporaryColliders(GameObject root)
        {
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>())
                Destroy(mc);
        }

        private Bounds GetCompoundBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private GameObject CreateMesh32(List<Vector3> verts, List<Vector3> normals, List<int> indices, Vector3 position)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);

            if (normals.Count > 0) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();

            mesh.RecalculateBounds();

            GameObject go = new GameObject("Mesh");
            go.transform.parent = transform;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.GetComponent<Renderer>().material = material;
            go.GetComponent<MeshFilter>().mesh = mesh;
            go.transform.localPosition = position;

            meshes.Add(go);
            return go;
        }

        private void OnRenderObject()
        {
            if (normalRenderer != null && meshes.Count > 0 && drawNormals)
            {
                normalRenderer.LocalToWorld = meshes[0].transform.localToWorldMatrix;
                normalRenderer.Draw();
            }
        }
    }
}