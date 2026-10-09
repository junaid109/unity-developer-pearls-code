using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Pearls.ConsoleUtils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.ConsoleUtils.Tests
{
    public class FrameBudgetedObjectPoolerTests
    {
        private GameObject _prefab;
        private GameObject _poolGo;
        private FrameBudgetedObjectPooler _pool;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PooledThing");
            _poolGo = new GameObject("Pool");
            _pool = _poolGo.AddComponent<FrameBudgetedObjectPooler>();
            _pool.Prefab = _prefab;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_poolGo);
            Object.DestroyImmediate(_prefab);
        }

        /// <summary>Steps the coroutine by hand. Each MoveNext is one frame.</summary>
        private static int RunToCompletion(IEnumerator routine, int maxFrames)
        {
            int frames = 0;
            while (routine.MoveNext())
            {
                frames++;
                if (frames > maxFrames) return -1; // never finished
            }
            return frames;
        }

        [Test]
        public void GenerousBudget_WarmsEverythingInOneFrame()
        {
            _pool.PoolSize = 10;
            _pool.MillisecondBudgetPerFrame = 10000f;

            int frames = RunToCompletion(_pool.WarmUpRoutine(), 100);

            Assert.AreEqual(1, frames);
            Assert.IsTrue(_pool.IsWarmupComplete);
            Assert.AreEqual(10, _poolGo.transform.childCount);
        }

        [Test]
        public void ZeroBudget_StillMakesProgress_OnePerFrame()
        {
            _pool.PoolSize = 5;
            _pool.MillisecondBudgetPerFrame = 0f; // a mistyped Inspector value

            int frames = RunToCompletion(_pool.WarmUpRoutine(), 100);

            Assert.AreEqual(5, frames, "With no budget the pool must still spawn one instance per frame.");
            Assert.IsTrue(_pool.IsWarmupComplete);
        }

        [Test]
        public void Get_ActivatesPooledInstance_Return_DeactivatesIt()
        {
            _pool.PoolSize = 1;
            _pool.MillisecondBudgetPerFrame = 10000f;
            RunToCompletion(_pool.WarmUpRoutine(), 10);

            GameObject a = _pool.Get();
            Assert.IsTrue(a.activeSelf);

            _pool.Return(a);
            Assert.IsFalse(a.activeSelf);
            Assert.AreSame(a, _pool.Get(), "A returned instance is reused.");
        }

        [Test]
        public void ExhaustedPool_FallsBackToInstantiate_AndWarns()
        {
            _pool.PoolSize = 0;
            RunToCompletion(_pool.WarmUpRoutine(), 10);

            LogAssert.Expect(LogType.Warning, new Regex("Pool exhausted for PooledThing"));
            GameObject extra = _pool.Get();
            Assert.IsNotNull(extra);
            Assert.AreSame(_poolGo.transform, extra.transform.parent);
        }
    }
}
