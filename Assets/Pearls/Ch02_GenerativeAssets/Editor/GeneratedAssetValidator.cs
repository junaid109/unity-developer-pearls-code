using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pearls.Generative
{
    // ---------- Thin Editor adapter (place this file in an Editor folder) ----------

    public class GeneratedAssetValidator : AssetPostprocessor
    {
        const string GeneratedRoot = "Assets/Generated/";
        const string QuarantineRoot = "Assets/Generated/_Quarantine/";
        static readonly AssetBudget Budget = new AssetBudget();

        static string SidecarFor(string assetPath) => assetPath + ".provenance.json";

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(GeneratedRoot)) return;

            var facts = GatherFacts(root, assetPath);
            var findings = GeneratedAssetRules.Evaluate(facts, Budget);
            bool quarantined = assetPath.StartsWith(QuarantineRoot);

            foreach (var finding in findings)
            {
                string line = $"GeneratedAssetValidator: {assetPath}: {finding.Message}";
                // Inside quarantine, everything is advisory. Outside it, errors are errors.
                if (finding.Severity == Severity.Error && !quarantined) Debug.LogError(line);
                else Debug.LogWarning(line);
            }
        }

        static AssetFacts GatherFacts(GameObject root, string path)
        {
            var facts = new AssetFacts
            {
                FileName = Path.GetFileName(path),
                HasProvenanceSidecar = File.Exists(SidecarFor(path))
            };
            var materials = new HashSet<Material>();

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // Avoid ?. on Unity objects: it bypasses Unity's overloaded null check.
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer s) mesh = s.sharedMesh;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh == null) continue;

                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                        facts.TriangleCount += mesh.GetIndexCount(i) / 3;

                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                    facts.AllMeshesHaveUVs = false;

                // Approximate: mesh bounds times the largest axis of the node's scale.
                Vector3 size = mesh.bounds.size;
                Vector3 scale = r.transform.lossyScale;
                float extent = Mathf.Max(size.x, size.y, size.z) *
                               Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                facts.MaxExtentMeters = Mathf.Max(facts.MaxExtentMeters, extent);

                foreach (var m in r.sharedMaterials)
                    if (m != null) materials.Add(m);
            }
            facts.MaterialCount = materials.Count;
            return facts;
        }

        // Refuse promotion: a model moved out of quarantine without a named approver goes back.
        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] moved, string[] movedFrom)
        {
            for (int i = 0; i < moved.Length; i++)
            {
                string from = movedFrom[i], to = moved[i];
                if (!from.StartsWith(QuarantineRoot) || to.StartsWith(QuarantineRoot)) continue;
                if (to.EndsWith(".json") || IsApproved(to) || IsApproved(from)) continue;

                Debug.LogError($"GeneratedAssetValidator: {to} has no 'approvedBy' in its " +
                               "provenance sidecar. Moving it back to quarantine.");
                // Never move assets during the import callback itself; defer to the next tick.
                EditorApplication.delayCall += () => AssetDatabase.MoveAsset(to, from);
            }
        }

        static bool IsApproved(string assetPath)
        {
            string sidecar = SidecarFor(assetPath);
            if (!File.Exists(sidecar)) return false;
            var data = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidecar));
            return data != null && !string.IsNullOrWhiteSpace(data.approvedBy);
        }

        [System.Serializable]
        class Sidecar { public string approvedBy; }
    }
}
