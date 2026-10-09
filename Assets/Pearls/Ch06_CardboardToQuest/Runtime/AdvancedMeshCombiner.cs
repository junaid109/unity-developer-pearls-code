using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pearls.Optimization
{
    /// <summary>
    /// Combines the static child meshes under this object into a few larger meshes,
    /// one per (material, spatial cell, vertex-budget chunk).
    ///
    /// What it does:
    /// - Walks every submesh of every child MeshRenderer, so multi-material meshes
    ///   keep all their submeshes.
    /// - Groups by material, and optionally by spatial cell so far-apart objects do
    ///   not end up in one giant bounding volume.
    /// - Splits very large groups with ChunkByVertexBudget.
    /// - Owns the meshes it creates and destroys them in ClearCombined/OnDestroy.
    ///
    /// What it does not do:
    /// - It does not touch skinned meshes, lightmap UVs (UV1) or per-instance
    ///   MaterialPropertyBlock data.
    /// - It does not make anything "static" for Unity. Setting GameObject.isStatic
    ///   at runtime does not trigger batching. If you want Unity-managed batching
    ///   instead of your own meshes, call StaticBatchingUtility.Combine(gameObject)
    ///   on the root at load time.
    /// </summary>
    public class AdvancedMeshCombiner : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, source objects are deactivated after combination (and reactivated by ClearCombined).")]
        public bool DeactivateSources = true;

        [Tooltip("Use 32-bit index buffer (allows > 65k vertices per mesh)")]
        public bool UseLargeBuffer = true;

        [Tooltip("Spatial cell size in local units. 0 = one group per material. " +
                 "Use a size that matches how far apart your clusters are, so culling still works.")]
        public float CellSize = 0f;

        [Tooltip("Maximum vertices per combined mesh before a group is split.")]
        public int MaxVertsPerChunk = 250_000;

        private Transform _container;
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        private readonly List<GameObject> _deactivated = new List<GameObject>();

        [ContextMenu("Create Optimized Mesh")]
        public void CreateOptimizedMesh()
        {
            ClearCombined(); // never combine our own output, and never leak the last run

            var groups = new Dictionary<(Material, Vector3Int), List<CombineInstance>>();
            var sources = new List<GameObject>();
            int sourceCount = 0;

            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.gameObject == gameObject) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                Mesh mesh = mf.sharedMesh;
                if (mr == null || !mr.enabled || mesh == null) continue;

                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[Optimization] '{mf.name}' uses a non-readable mesh; enable Read/Write on the import settings. Skipped.", mf);
                    continue;
                }

                Vector3Int cell = Vector3Int.zero;
                if (CellSize > 0f)
                {
                    Vector3 local = transform.InverseTransformPoint(mf.transform.position) / CellSize;
                    cell = Vector3Int.FloorToInt(local);
                }

                Matrix4x4 toLocal = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Material[] mats = mr.sharedMaterials;
                bool used = false;

                // One CombineInstance per submesh, so no submesh is silently dropped.
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;

                    var key = (mats[s], cell);
                    if (!groups.TryGetValue(key, out var list))
                        groups[key] = list = new List<CombineInstance>();

                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = toLocal });
                    used = true;
                }

                if (used)
                {
                    sources.Add(mf.gameObject);
                    sourceCount++;
                }
            }

            if (groups.Count == 0) return;

            _container = new GameObject("Combined").transform;
            _container.SetParent(transform, false);

            int batches = 0;
            foreach (var group in groups)
            {
                foreach (var chunk in ChunkByVertexBudget(group.Value, MaxVertsPerChunk))
                {
                    var go = new GameObject($"Combined_{group.Key.Item1.name}_{group.Key.Item2}");
                    go.transform.SetParent(_container, false);

                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = group.Key.Item1;

                    var combined = new Mesh { name = go.name };
                    if (UseLargeBuffer) combined.indexFormat = IndexFormat.UInt32;
                    combined.CombineMeshes(chunk.ToArray(), true, true);

                    // sharedMesh, not mesh: the .mesh getter/setter can clone and leak.
                    // We own this mesh and destroy it in ClearCombined.
                    mf.sharedMesh = combined;
                    _ownedMeshes.Add(combined);
                    batches++;
                }
            }

            // Deactivate only after the iteration above has finished.
            if (DeactivateSources)
            {
                foreach (var src in sources)
                {
                    src.SetActive(false);
                    _deactivated.Add(src);
                }
            }

            Debug.Log($"[Optimization] Combined {sourceCount} source objects into {batches} batches.");
        }

        /// <summary>Destroys generated objects and meshes and reactivates the sources.</summary>
        [ContextMenu("Clear Combined")]
        public void ClearCombined()
        {
            foreach (var src in _deactivated)
                if (src != null) src.SetActive(true);
            _deactivated.Clear();

            foreach (var mesh in _ownedMeshes)
                if (mesh != null) SafeDestroy(mesh);
            _ownedMeshes.Clear();

            if (_container != null)
            {
                _container.gameObject.SetActive(false); // Destroy is deferred; hide it from later scans
                SafeDestroy(_container.gameObject);
                _container = null;
            }
        }

        private void OnDestroy()
        {
            // Generated meshes are not owned by any asset; free them explicitly.
            foreach (var mesh in _ownedMeshes)
                if (mesh != null) SafeDestroy(mesh);
            _ownedMeshes.Clear();
        }

        private static void SafeDestroy(Object obj)
        {
            // Destroy is illegal in edit mode, DestroyImmediate is discouraged in play mode.
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        /// <summary>
        /// Splits a group into several if it would exceed a safe vertex budget.
        /// Counts the whole source mesh's vertices per instance, which slightly
        /// over-estimates when only one submesh is used. That errs on the safe side.
        /// </summary>
        private static List<List<CombineInstance>> ChunkByVertexBudget(List<CombineInstance> group, int maxVertsPerChunk)
        {
            var chunks = new List<List<CombineInstance>>();
            var current = new List<CombineInstance>();
            int runningVertCount = 0;

            foreach (var ci in group)
            {
                int vertCount = ci.mesh != null ? ci.mesh.vertexCount : 0;

                if (runningVertCount + vertCount > maxVertsPerChunk && current.Count > 0)
                {
                    chunks.Add(current);
                    current = new List<CombineInstance>();
                    runningVertCount = 0;
                }

                current.Add(ci);
                runningVertCount += vertCount;
            }

            if (current.Count > 0) chunks.Add(current);
            return chunks;
        }
    }
}