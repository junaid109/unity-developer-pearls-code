using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;

namespace Pearls.Kiosk
{
    /// <summary>
    /// Health monitoring and deterministic recovery for an unattended,
    /// public-facing Unity installation. No developer-facing UI is ever
    /// shown to the public -- every failure path ends back at the attract
    /// scene.
    /// Place one instance in the first scene that loads; it persists across resets.
    /// </summary>
    public class KioskArmor : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Seconds of no interaction before resetting to the attract state.")]
        public float IdleResetTimeout = 120f;
        [Tooltip("Build index of the attract / idle scene every reset returns to.")]
        public int AttractSceneIndex = 0;
        [Tooltip("Minimum seconds between resets, so a scene that throws on load cannot cause a reset loop.")]
        public float MinSecondsBetweenResets = 5f;

        private static KioskArmor _instance;
        private string _errorLogPath;
        private readonly object _logLock = new object();
        private IdleResetPolicy _policy; // the pure decisions: idle yet? reset allowed?

        // Written from whatever thread logged the exception, read on the main thread.
        private volatile string _pendingResetReason;

        void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Cache the path now: Application.persistentDataPath may only be
            // read on the main thread, and the log callback can run elsewhere.
            _errorLogPath = Path.Combine(Application.persistentDataPath, "kiosk_errors.log");
            _policy = new IdleResetPolicy(IdleResetTimeout, MinSecondsBetweenResets, Time.unscaledTime);
            Application.logMessageReceivedThreaded += CaptureFatalErrors;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                Application.logMessageReceivedThreaded -= CaptureFatalErrors;
                _instance = null;
            }
        }

        void Update()
        {
            // Track any sign of a live visitor -- touch, key or mouse button,
            // since the same build often runs on a touch kiosk or a desktop
            // fallback rig.
            if (VisitorActivityThisFrame())
            {
                _policy.RegisterInteraction(Time.unscaledTime);
            }

            // Resets requested from the log callback are executed here, on the
            // main thread, because SceneManager.LoadScene is not thread-safe.
            string reason = _pendingResetReason;
            if (reason != null)
            {
                _pendingResetReason = null;
                ResetKiosk(reason);
                return;
            }

            // A visitor who walked away mid-session leaves the app dangling
            // in whatever state they left it. Return to attract mode so the
            // next visitor gets a clean start -- unless we are already there.
            bool inAttract = SceneManager.GetActiveScene().buildIndex == AttractSceneIndex;
            if (_policy.IsIdle(Time.unscaledTime, inAttract))
            {
                ResetKiosk("Idle timeout");
            }
        }

        // New Unity 6 projects use the Input System package only, and there the legacy
        // Input class throws InvalidOperationException on every call. Read whichever
        // input backend the Player Settings have switched on.
        private static bool VisitorActivityThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) return true;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.anyKey || Input.touchCount > 0) return true;
#endif
            return false;
        }

        // May be called on any thread: do file I/O only, never touch Unity objects.
        private void CaptureFatalErrors(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;

            // Log to disk for later diagnosis -- there's no one on site to
            // read this in real time, so it has to survive until someone
            // can pull it off the device after the event.
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(_errorLogPath, $"[{DateTime.Now:u}] {type}: {condition}\n{stackTrace}\n\n");
                }
            }
            catch (IOException)
            {
                // If we can't even write the log, don't let logging itself
                // become the crash. Fall through to the reset regardless.
            }

            if (type == LogType.Exception)
            {
                _pendingResetReason = $"Fatal exception: {condition}";
            }
        }

        /// <summary>Main-thread only. Idle timeouts and fatal errors both end here.</summary>
        public void ResetKiosk(string reason)
        {
            if (!_policy.TryBeginReset(Time.unscaledTime)) return; // too soon after the last one

            // Deliberately not Debug.LogError -- this path must never look
            // like a crash to anything watching the console output.
            Debug.LogWarning($"[KioskArmor] Resetting to attract state. Reason: {reason}");
            SceneManager.LoadScene(AttractSceneIndex);
        }
    }

    /// <summary>
    /// Offline-first capture queue. Writes each interaction/lead record to
    /// disk immediately so nothing is lost to a dropped connection; a
    /// background uploader drains the queue whenever the network is up.
    /// Call from the main thread (Application.persistentDataPath is main-thread only).
    /// </summary>
    public static class OfflinePersistence
    {
        public static string QueueDir => Path.Combine(Application.persistentDataPath, "offline_queue");

        public static void Enqueue(string jsonPayload)
        {
            Directory.CreateDirectory(QueueDir);
            string name = Guid.NewGuid().ToString();
            string tmp = Path.Combine(QueueDir, name + ".tmp");
            string final = Path.Combine(QueueDir, name + ".json");

            // Write to a temp name, then rename: the uploader only sees *.json,
            // so it can never pick up a half-written file after a power cut.
            File.WriteAllText(tmp, jsonPayload);
            File.Move(tmp, final);
            // A separate background pass (OfflineQueueUploader, below) periodically
            // attempts to upload every *.json and deletes each one only after a
            // confirmed successful send.
        }
    }
}