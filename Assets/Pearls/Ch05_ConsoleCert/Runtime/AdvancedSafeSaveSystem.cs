using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace Pearls.ConsoleUtils
{
    /// <summary>
    /// A crash-safe save system: write-then-replace, SHA-256 integrity on every
    /// file, and a backup the loader falls back to.
    ///
    /// DESIGN NOTES:
    /// - The primary file is never written directly.
    /// - Each file is [payload][32-byte SHA-256], so main AND backup can be verified.
    /// - File.Replace is used when a main file exists. It is atomic only where the
    ///   OS/filesystem provides it. If a crash lands in a non-atomic window, the
    ///   loader still recovers from the backup (or the leftover temp file).
    /// - Disk work runs on a worker thread to keep Unity's main thread responsive.
    /// - FaultInjection lets tests throw between steps, deterministically.
    /// </summary>
    public static class AdvancedSafeSaveSystem
    {
        private const string SAVE_FILENAME = "userdata.dat";
        private const string TEMP_FILENAME = "userdata.tmp";
        private const string BACKUP_FILENAME = "userdata.bak";
        private const int HASH_SIZE = 32; // SHA-256

        /// <summary>
        /// Test hook. Called with a stage name ("AfterTempWrite", "AfterVerify",
        /// "BeforeReplace"). Throw from it to simulate the process dying at that
        /// point. Leave null in production builds.
        /// </summary>
        public static Action<string> FaultInjection;

        private static string FolderPath => Application.persistentDataPath;

        public static async Task<bool> SaveDataAsync(byte[] data)
        {
            string mainPath = Path.Combine(FolderPath, SAVE_FILENAME);
            string tempPath = Path.Combine(FolderPath, TEMP_FILENAME);
            string backupPath = Path.Combine(FolderPath, BACKUP_FILENAME);

            try
            {
                // 1. Serialize payload + hash into one buffer.
                byte[] packed = Pack(data);

                // 2. Write to the temporary file first (off-thread).
                await Task.Run(() => File.WriteAllBytes(tempPath, packed));
                FaultInjection?.Invoke("AfterTempWrite");

                // 3. Read the temp file back and verify its own embedded hash.
                byte[] readBack = await Task.Run(() => File.ReadAllBytes(tempPath));
                if (!TryUnpack(readBack, out _))
                    throw new IOException("Temporary file verification failed.");
                FaultInjection?.Invoke("AfterVerify");

                // 4. Replace the main file.
                FaultInjection?.Invoke("BeforeReplace");
                await Task.Run(() =>
                {
                    if (File.Exists(mainPath))
                    {
                        // Main becomes the new content, the old main becomes the backup.
                        File.Replace(tempPath, mainPath, backupPath);
                    }
                    else
                    {
                        File.Move(tempPath, mainPath);
                    }
                });

                Debug.Log($"[SafeSave] Save complete at {DateTime.Now}");
                return true;
            }
            catch (Exception ex)
            {
                // The existing main file is untouched unless the failure happened
                // inside the replace step, and LoadDataAsync covers that case.
                Debug.LogError($"[SafeSave] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads the newest file that passes its integrity check: main, then
        /// backup, then a fully written temp file left by an interrupted swap.
        /// Returns null if nothing valid exists.
        /// </summary>
        public static async Task<byte[]> LoadDataAsync()
        {
            string[] candidates =
            {
                Path.Combine(FolderPath, SAVE_FILENAME),
                Path.Combine(FolderPath, BACKUP_FILENAME),
                Path.Combine(FolderPath, TEMP_FILENAME)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                if (!File.Exists(path)) continue;

                try
                {
                    byte[] raw = await Task.Run(() => File.ReadAllBytes(path));
                    if (TryUnpack(raw, out byte[] payload))
                    {
                        if (i > 0)
                            Debug.LogWarning($"[SafeSave] Main save unusable, recovered from {Path.GetFileName(path)}.");
                        return payload;
                    }
                    Debug.LogWarning($"[SafeSave] {Path.GetFileName(path)} failed its integrity check.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SafeSave] Could not read {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            return null; // an honest "no valid save data"
        }

        private static byte[] Pack(byte[] payload)
        {
            byte[] hash = ComputeHash(payload);
            byte[] packed = new byte[payload.Length + HASH_SIZE];
            Buffer.BlockCopy(payload, 0, packed, 0, payload.Length);
            Buffer.BlockCopy(hash, 0, packed, payload.Length, HASH_SIZE);
            return packed;
        }

        private static bool TryUnpack(byte[] packed, out byte[] payload)
        {
            payload = null;
            if (packed == null || packed.Length < HASH_SIZE) return false;

            int payloadLength = packed.Length - HASH_SIZE;
            byte[] body = new byte[payloadLength];
            Buffer.BlockCopy(packed, 0, body, 0, payloadLength);

            byte[] expected = ComputeHash(body);
            for (int i = 0; i < HASH_SIZE; i++)
                if (packed[payloadLength + i] != expected[i]) return false;

            payload = body;
            return true;
        }

        private static byte[] ComputeHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
                return sha256.ComputeHash(data);
        }
    }
}