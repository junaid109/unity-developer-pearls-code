using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Pearls.Kiosk
{
    /// <summary>
    /// Drains the offline queue in the background, on a slow cadence, so it
    /// never competes with foreground network use (media downloads, live
    /// data polling) for bandwidth on a venue's often-congested Wi-Fi.
    /// Runs on the main thread as a coroutine, so Unity APIs are safe here.
    /// </summary>
    public class OfflineQueueUploader : MonoBehaviour
    {
        [Tooltip("Seconds between upload attempts. Deliberately slow -- this is a background chore, not the priority.")]
        public float DrainIntervalSeconds = 15f;
        [Tooltip("Times the server may permanently reject a record (HTTP 4xx) before it is moved aside. Network errors and 5xx never count.")]
        public int MaxRejectionsPerRecord = 3;

        private readonly Dictionary<string, int> _rejectionCounts = new Dictionary<string, int>();

        void Start() => StartCoroutine(DrainLoop());

        private IEnumerator DrainLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(DrainIntervalSeconds);
                yield return DrainOnce();
            }
        }

        private IEnumerator DrainOnce()
        {
            string queueDir = OfflinePersistence.QueueDir;
            if (!Directory.Exists(queueDir)) yield break;

            // Only fully written files: Enqueue renames .tmp to .json when complete.
            foreach (string path in Directory.GetFiles(queueDir, "*.json"))
            {
                string json;
                try
                {
                    json = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    continue; // File is locked -- skip this pass, try again next cycle.
                }

                using (UnityWebRequest request = new UnityWebRequest("https://api.example.com/interactions", "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    // The file name is stable across retries, so the server can drop a
                    // duplicate if an earlier send succeeded but its reply was lost.
                    request.SetRequestHeader("Idempotency-Key", Path.GetFileNameWithoutExtension(path));
                    request.timeout = 15;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        File.Delete(path); // Only remove on a confirmed successful send.
                        _rejectionCounts.Remove(path);
                        continue;
                    }

                    long code = request.responseCode;
                    bool permanentRejection = request.result == UnityWebRequest.Result.ProtocolError
                                              && code >= 400 && code < 500 && code != 408 && code != 429;

                    if (!permanentRejection)
                    {
                        // Network down, timeout, 5xx or rate limit: the record is fine.
                        // Do NOT count this against it, and stop hammering a dead link --
                        // wait for the next cycle.
                        yield break;
                    }

                    // A record the server permanently rejects (malformed payload, say)
                    // must not jam the queue behind it -- quarantine it for manual
                    // inspection after a few rejections.
                    int rejections = _rejectionCounts.TryGetValue(path, out int c) ? c + 1 : 1;
                    _rejectionCounts[path] = rejections;
                    if (rejections >= MaxRejectionsPerRecord)
                    {
                        Quarantine(path);
                        _rejectionCounts.Remove(path);
                    }
                }
            }
        }

        private static void Quarantine(string path)
        {
            try
            {
                string quarantineDir = Path.Combine(Application.persistentDataPath, "offline_queue_failed");
                Directory.CreateDirectory(quarantineDir);
                string target = Path.Combine(quarantineDir, Path.GetFileName(path));
                if (File.Exists(target)) File.Delete(target);
                File.Move(path, target);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Uploader] Could not quarantine {path}: {e.Message}");
            }
        }
    }
}