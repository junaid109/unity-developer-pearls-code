using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Pearls.ConsoleUtils
{
    /// <summary>
    /// An object pool that warms its pool across multiple frames using a
    /// per-frame millisecond budget, rather than instantiating everything
    /// in a single frame.
    /// </summary>
    public class FrameBudgetedObjectPooler : MonoBehaviour
    {
        [Tooltip("Prefab to pool.")]
        public GameObject Prefab;

        [Tooltip("Total instances to pre-warm.")]
        public int PoolSize = 100;

        [Tooltip("Max milliseconds of instantiation work allowed per frame " +
                 "during warm-up. Keep well under your frame budget (16.6ms " +
                 "at 60fps) since rendering and other systems need the rest.")]
        public float MillisecondBudgetPerFrame = 2.0f;

        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        public bool IsWarmupComplete { get; private set; }

        public IEnumerator WarmUpRoutine()
        {
            int spawned = 0;

            while (spawned < PoolSize)
            {
                _stopwatch.Restart();
                int spawnedThisFrame = 0;

                // Instantiate as many objects as fit in this frame's budget,
                // instead of a fixed count -- complex prefabs (physics,
                // skinned meshes) cost more per-instance than simple ones,
                // so a fixed "10 per frame" rule doesn't generalize.
                // Always spawn at least one per frame: with a budget of 0 (a
                // mistyped Inspector value) the loop would otherwise never run
                // and the warm-up would yield forever without finishing.
                while (spawned < PoolSize &&
                       (spawnedThisFrame == 0 ||
                        _stopwatch.Elapsed.TotalMilliseconds < MillisecondBudgetPerFrame))
                {
                    GameObject instance = Instantiate(Prefab, transform);
                    instance.SetActive(false);
                    _pool.Enqueue(instance);
                    spawned++;
                    spawnedThisFrame++;
                }

                // Yield control back to the render loop -- this is the line
                // that keeps the splash screen responsive. Without it, the
                // while loop above runs to completion in one frame.
                yield return null;
            }

            IsWarmupComplete = true;
        }

        /// <summary>Retrieves a warmed instance, or instantiates a fresh one
        /// as a fallback if the pool is exhausted (logs a warning -- this
        /// means PoolSize was undersized for actual peak demand).</summary>
        public GameObject Get()
        {
            if (_pool.Count > 0)
            {
                GameObject instance = _pool.Dequeue();
                instance.SetActive(true);
                return instance;
            }

            UnityEngine.Debug.LogWarning(
                $"[FrameBudgetedObjectPooler] Pool exhausted for {Prefab.name}. " +
                "Instantiating on-demand -- this will cost a frame spike. " +
                "Consider raising PoolSize.");
            return Instantiate(Prefab, transform);
        }

        public void Return(GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(transform);
            _pool.Enqueue(instance);
        }
    }
}