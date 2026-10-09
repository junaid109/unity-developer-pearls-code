using System.Linq;
using NUnit.Framework;
using Pearls.Optimization;
using UnityEngine;

namespace Pearls.Optimization.Tests
{
    public class AdvancedMeshCombinerTests
    {
        private GameObject _root;
        private AdvancedMeshCombiner _combiner;
        private Material _red, _blue;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root");
            _combiner = _root.AddComponent<AdvancedMeshCombiner>();
            var shader = Shader.Find("Hidden/Internal-Colored");
            _red = new Material(shader) { name = "Red" };
            _blue = new Material(shader) { name = "Blue" };
        }

        [TearDown]
        public void TearDown()
        {
            _combiner.ClearCombined();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_red);
            Object.DestroyImmediate(_blue);
        }

        private GameObject AddCube(Vector3 position, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_root.transform, false);
            cube.transform.localPosition = position;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        private Transform Container => _root.transform.Find("Combined");

        [Test]
        public void GroupsByMaterial_AndDeactivatesSources()
        {
            var a = AddCube(Vector3.zero, _red);
            var b = AddCube(Vector3.right * 2, _red);
            var c = AddCube(Vector3.up * 2, _blue);

            _combiner.CreateOptimizedMesh();

            Assert.IsNotNull(Container);
            Assert.AreEqual(2, Container.childCount, "One combined mesh per material.");
            var redMesh = Container.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.sharedMaterial == _red).GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2 * 24, redMesh.vertexCount, "Two cubes of 24 vertices each.");
            Assert.IsFalse(a.activeSelf || b.activeSelf || c.activeSelf);
        }

        [Test]
        public void CombinedVertices_StayInRootLocalSpace()
        {
            AddCube(new Vector3(10f, 0f, 0f), _red);
            _root.transform.position = new Vector3(100f, 0f, 0f); // move the root after placing children

            _combiner.CreateOptimizedMesh();

            var mesh = Container.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.AreEqual(10f, mesh.bounds.center.x, 1e-4f);
        }

        [Test]
        public void CellSize_SplitsFarApartObjects()
        {
            AddCube(Vector3.zero, _red);
            AddCube(new Vector3(50f, 0f, 0f), _red);
            _combiner.CellSize = 10f;

            _combiner.CreateOptimizedMesh();

            Assert.AreEqual(2, Container.childCount, "Same material, different cells.");
        }

        [Test]
        public void VertexBudget_SplitsAGroupIntoChunks()
        {
            for (int i = 0; i < 3; i++) AddCube(Vector3.right * i * 2, _red);
            _combiner.MaxVertsPerChunk = 30; // fits one 24-vertex cube per chunk

            _combiner.CreateOptimizedMesh();

            Assert.AreEqual(3, Container.childCount);
        }

        [Test]
        public void ClearCombined_ReactivatesSources_AndRemovesOutput()
        {
            var a = AddCube(Vector3.zero, _red);
            _combiner.CreateOptimizedMesh();
            _combiner.ClearCombined();

            Assert.IsTrue(a.activeSelf);
            Assert.IsNull(Container);
        }

        [Test]
        public void RunningTwice_DoesNotCombineItsOwnOutput()
        {
            AddCube(Vector3.zero, _red);
            _combiner.CreateOptimizedMesh();
            _combiner.CreateOptimizedMesh();

            Assert.AreEqual(1, _root.transform.Cast<Transform>().Count(t => t.name == "Combined"));
            Assert.AreEqual(1, Container.childCount);
        }
    }
}
