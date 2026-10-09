using NUnit.Framework;
using Pearls.Enterprise;
using UnityEngine;

namespace Pearls.Enterprise.Tests
{
    public class SiteClipTests
    {
        private static Bounds Tile(float cx, float cz, float size, float height = 1f) =>
            new Bounds(new Vector3(cx, height / 2f, cz), new Vector3(size, height, size));

        [Test]
        public void ZeroRadius_DisablesClipping()
        {
            Assert.IsTrue(SiteClip.Intersects(Tile(10000f, 10000f, 10f), Vector3.zero, 0f));
        }

        [Test]
        public void CentreInsideTile_Intersects()
        {
            Assert.IsTrue(SiteClip.Intersects(Tile(0f, 0f, 100f), new Vector3(10f, 0f, 10f), 1f));
        }

        [Test]
        public void TileCornerJustInsideRadius_Intersects_JustOutside_DoesNot()
        {
            // Tile spans x,z in [100, 110]. Nearest point to the origin is (100, 100), ~141.42 m away.
            Assert.IsTrue(SiteClip.Intersects(Tile(105f, 105f, 10f), Vector3.zero, 141.5f));
            Assert.IsFalse(SiteClip.Intersects(Tile(105f, 105f, 10f), Vector3.zero, 141.3f));
        }

        [Test]
        public void HeightIsIgnored_SoATallTowerOrDeepValleyIsKept()
        {
            // The site centre is 500 m below the tile, but horizontally inside it.
            var tower = new Bounds(new Vector3(0f, 800f, 0f), new Vector3(10f, 100f, 10f));
            Assert.IsTrue(SiteClip.Intersects(tower, new Vector3(0f, 300f, 0f), 5f));
        }
    }

    public class BIMDataContainerTests
    {
        [Test]
        public void StoresAndReturnsValues_AndNullForMissingKeys()
        {
            var go = new GameObject("Beam");
            try
            {
                var data = go.AddComponent<BIMDataContainer>();
                data.AddData("FireRating", "60min");
                data.AddData("Material", "Steel");

                Assert.AreEqual("Steel", data.GetValue("Material"));
                Assert.IsNull(data.GetValue("LoadClass"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
