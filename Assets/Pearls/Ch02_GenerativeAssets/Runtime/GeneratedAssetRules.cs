using System.Collections.Generic;

namespace Pearls.Generative
{
    // ---------- Pure rule layer: no Unity types, testable with plain NUnit ----------

    public enum Severity { Warning, Error }

    public sealed class Finding
    {
        public readonly Severity Severity;
        public readonly string Message;
        public Finding(Severity severity, string message) { Severity = severity; Message = message; }
        public override string ToString() => $"[{Severity}] {Message}";
    }

    /// <summary>Plain facts about one imported model. The adapter fills these in.</summary>
    public sealed class AssetFacts
    {
        public string FileName = "";
        public long TriangleCount;
        public float MaxExtentMeters;      // largest bounding-box side, after import scale
        public bool AllMeshesHaveUVs = true;
        public int MaterialCount;
        public bool HasProvenanceSidecar;
    }

    /// <summary>Per-project budget. Placeholder values: set your own per asset category.</summary>
    public sealed class AssetBudget
    {
        public long MaxTriangles = 20000;
        public float MinExtentMeters = 0.05f;
        public float MaxExtentMeters = 30f;
        public int MaxMaterials = 3;
        public string RequiredPrefix = "GEN_";
    }

    public static class GeneratedAssetRules
    {
        public static List<Finding> Evaluate(AssetFacts f, AssetBudget b)
        {
            var findings = new List<Finding>();

            if (!f.HasProvenanceSidecar)
                findings.Add(new Finding(Severity.Error,
                    "No provenance sidecar. Record tool, version, source and terms check."));

            if (!f.FileName.StartsWith(b.RequiredPrefix))
                findings.Add(new Finding(Severity.Warning,
                    $"Name should start with '{b.RequiredPrefix}' so generated assets stay findable."));

            if (f.TriangleCount > b.MaxTriangles)
                findings.Add(new Finding(Severity.Error,
                    $"{f.TriangleCount} triangles exceeds budget of {b.MaxTriangles}. Retopologise."));

            // Out-of-range size usually means a centimetre/metre mix-up or a normalised export.
            if (f.MaxExtentMeters < b.MinExtentMeters || f.MaxExtentMeters > b.MaxExtentMeters)
                findings.Add(new Finding(Severity.Warning,
                    $"Largest side is {f.MaxExtentMeters:0.###} m. Check units and import scale."));

            if (!f.AllMeshesHaveUVs)
                findings.Add(new Finding(Severity.Error, "A mesh has no UV0 channel. Unwrap before import."));

            if (f.MaterialCount > b.MaxMaterials)
                findings.Add(new Finding(Severity.Warning,
                    $"{f.MaterialCount} materials (budget {b.MaxMaterials}). Consider merging."));

            return findings;
        }

        public static bool HasErrors(List<Finding> findings) =>
            findings.Exists(x => x.Severity == Severity.Error);
    }
}
