using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Pearls.Enterprise
{
    /// <summary>
    /// A senior-level asset pipeline for industrial CAD/BIM data.
    /// </summary>
    public class EnterpriseBIMImporter : AssetPostprocessor
    {
        private const string CAD_TAG = "BIM_Asset";

        // Pre-process: Before Unity creates its internal mesh format
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("_Enterprise/")) return;

            ModelImporter modelImporter = assetImporter as ModelImporter;
            if (modelImporter == null) return;

            // PERFORMANCE: Aggressive optimization
            modelImporter.optimizeMeshVertices = true;
            modelImporter.optimizeMeshPolygons = true;
            modelImporter.weldVertices = true;
            modelImporter.importAnimation = false;
            modelImporter.importLights = false;
            modelImporter.importCameras = false;

            // VISUALS: Auto-generate lightmap UVs is critical for CAD
            modelImporter.generateSecondaryUV = true;
        }

        // Post-process: After the GameObject hierarchy is created
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Contains("_Enterprise/")) return;

            var metadataLookup = LoadSidecarMetadata(assetPath);

            var allTransforms = root.GetComponentsInChildren<Transform>();
            foreach (var t in allTransforms)
            {
                SanitizeObject(t.gameObject, metadataLookup);
            }
        }

        /// <summary>
        /// Loads the companion metadata file exported alongside the FBX (same
        /// base filename, .meta.json extension). Missing or malformed sidecar data should
        /// never fail the import, geometry still needs to come in even if the
        /// metadata join fails, so we log loudly and continue with an empty map.
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> LoadSidecarMetadata(string modelAssetPath)
        {
            string sidecarPath = System.IO.Path.ChangeExtension(modelAssetPath, ".meta.json");

            if (!System.IO.File.Exists(sidecarPath))
            {
                Debug.LogWarning($"[BIMImporter] No sidecar metadata found for '{modelAssetPath}'. " +
                                  "Geometry will import, but objects will carry no BIM property data.");
                return new Dictionary<string, Dictionary<string, string>>();
            }

            try
            {
                string json = System.IO.File.ReadAllText(sidecarPath);
                // In production this deserializes into a GlobalId -> {key: value} map
                // via JsonUtility or a lightweight JSON library; omitted here for brevity.
                return ParseMetadataJson(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BIMImporter] Failed to parse sidecar metadata for '{modelAssetPath}': {ex.Message}");
                return new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private Dictionary<string, Dictionary<string, string>> ParseMetadataJson(string json)
        {
            // Placeholder for the real deserialization step.
            return new Dictionary<string, Dictionary<string, string>>();
        }

        private void SanitizeObject(GameObject go, Dictionary<string, Dictionary<string, string>> metadataLookup)
        {
            // 1. Tag for easy searching
            go.tag = "Untagged"; // Use a custom tag in production

            // 2. Add Metadata Container and join against the sidecar data by name.
            // Production pipelines key this off the IFC GlobalId embedded in the
            // FBX node name during export, not the raw display name, which can
            // collide across a large model.
            var data = go.AddComponent<BIMDataContainer>();
            if (metadataLookup.TryGetValue(go.name, out var properties))
            {
                foreach (var kvp in properties)
                    data.AddData(kvp.Key, kvp.Value);
            }

            // 3. Automated Collider Logic
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float boundsSize = mf.sharedMesh.bounds.size.magnitude;

                if (boundsSize < 0.05f) 
                {
                    // Too small to care about? Skip collider to save physics CPU
                    return;
                }

                if (boundsSize > 10.0f)
                {
                    // Massively large? Use BoxCollider for performance (bounds are in
                    // the model's local units, so tune these thresholds to your scale)
                    go.AddComponent<BoxCollider>();
                }
                else
                {
                    // Medium? A convex MeshCollider. Convex hulls are capped (Unity
                    // limits them to 255 triangles and logs a warning when it simplifies
                    // the hull), so dense meshes may need a BoxCollider instead.
                    var mc = go.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
            }
        }
    }
}
