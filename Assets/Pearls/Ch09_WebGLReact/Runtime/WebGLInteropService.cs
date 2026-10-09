using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace Pearls.Metaverse
{
    [Serializable]
    public class BridgeMessage
    {
        public string Type;
        public string Payload; // JSON serialized sub-object
    }

    /// <summary>
    /// A typed interop service for React + Unity WebGL. The same bridge
    /// handles session/auth state, navigation events, and analytics --
    /// anything the page needs to tell Unity, or Unity needs to tell the
    /// page, goes through this one typed channel rather than ad hoc
    /// SendMessage calls scattered across the codebase.
    /// </summary>
    public static class WebGLInteropService
    {
        [DllImport("__Internal")]
        private static extern void OnUnityMessage(string json);

        [DllImport("__Internal")]
        private static extern string GetExternalState(string key);

        // WebGLInteropService.cs -- outbound queue for messages sent before the bridge is confirmed ready
        private static readonly Queue<BridgeMessage> _pendingOutbound = new Queue<BridgeMessage>();
        private static bool _bridgeConfirmedReady;

        public static void SendToReact(string type, object payload)
        {
            var msg = new BridgeMessage { Type = type, Payload = JsonUtility.ToJson(payload) };

            if (!_bridgeConfirmedReady)
            {
                _pendingOutbound.Enqueue(msg);
                return;
            }

            Flush(msg);
        }

        // Called once React signals (via GetExternalState or a dedicated ack call) that it has mounted.
        public static void ConfirmBridgeReady()
        {
            _bridgeConfirmedReady = true;
            while (_pendingOutbound.Count > 0) Flush(_pendingOutbound.Dequeue());
        }

        private static void Flush(BridgeMessage msg)
        {
            string finalJson = JsonUtility.ToJson(msg);
            #if UNITY_WEBGL && !UNITY_EDITOR
            OnUnityMessage(finalJson);
            #else
            Debug.Log($"[Mock] Sent to React: {msg.Type}");
            #endif
        }

        public static string GetValueFromBrowser(string key)
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return GetExternalState(key);
            #else
            return "MockValue";
            #endif
        }
    }
}
