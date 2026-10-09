using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Pearls.WebGL
{
    /// <summary>
    /// Downloads assets over plain HTTP one at a time, builds them into GameObjects,
    /// and evicts the least recently used ones when a byte budget is exceeded.
    /// </summary>
    public sealed class StreamedAssetPool : MonoBehaviour
    {
        [SerializeField] private long _byteBudget = 64L * 1024 * 1024;

        // Builds a GameObject from downloaded bytes. Supplied by the caller.
        public delegate GameObject Builder(byte[] data);

        private sealed class Entry
        {
            public GameObject Instance;
            public long Bytes;
            public LinkedListNode<string> Node;
        }

        private readonly Dictionary<string, Entry> _loaded = new Dictionary<string, Entry>();
        private readonly LinkedList<string> _lru = new LinkedList<string>();   // front = most recent
        private readonly Queue<Action> _pending = new Queue<Action>();
        private readonly HashSet<string> _inFlight = new HashSet<string>();
        private long _usedBytes;
        private bool _running;

        public void Request(string url, Builder build, Action<GameObject> onReady)
        {
            if (_loaded.TryGetValue(url, out var hit))
            {
                Touch(hit);
                onReady?.Invoke(hit.Instance);
                return;
            }

            _pending.Enqueue(() => StartCoroutine(Download(url, build, onReady)));
            if (!_running) StartCoroutine(Pump());
        }

        // One download at a time. Slow on purpose: a browser tab cannot absorb a burst.
        private IEnumerator Pump()
        {
            _running = true;
            while (_pending.Count > 0)
            {
                _pending.Dequeue().Invoke();
                while (_inFlight.Count > 0) yield return null;
            }
            _running = false;
        }

        private IEnumerator Download(string url, Builder build, Action<GameObject> onReady)
        {
            // Two callers may have queued the same URL; the second one reuses the first's result.
            if (_loaded.TryGetValue(url, out var existing))
            {
                Touch(existing);
                onReady?.Invoke(existing.Instance);
                yield break;
            }

            _inFlight.Add(url);
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Stream failed for {url}: {request.error}");
                    _inFlight.Remove(url);
                    onReady?.Invoke(null);
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                EvictUntilFits(data.Length);

                GameObject instance = build(data);
                var entry = new Entry { Instance = instance, Bytes = data.Length };
                entry.Node = _lru.AddFirst(url);
                _loaded[url] = entry;
                _usedBytes += entry.Bytes;

                _inFlight.Remove(url);
                onReady?.Invoke(instance);
            }
        }

        private void Touch(Entry entry)
        {
            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
        }

        private void EvictUntilFits(long incoming)
        {
            while (_usedBytes + incoming > _byteBudget && _lru.Last != null)
            {
                string oldest = _lru.Last.Value;
                var entry = _loaded[oldest];

                Destroy(entry.Instance);
                _usedBytes -= entry.Bytes;
                _lru.RemoveLast();
                _loaded.Remove(oldest);
            }
        }

        public void ReleaseAll()
        {
            foreach (var entry in _loaded.Values) Destroy(entry.Instance);
            _loaded.Clear();
            _lru.Clear();
            _usedBytes = 0;
            Resources.UnloadUnusedAssets();
        }
    }
}