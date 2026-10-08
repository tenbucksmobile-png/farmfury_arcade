using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmFuryArcade.Core
{
    /// <summary>Web demo: the save store SaveManager writes through, with the same calls as
    /// UnityEngine.PlayerPrefs.
    ///
    /// Two backends:
    /// - PlayerPrefs (default): the website build, the Editor, and a YouTube build opened outside
    ///   YouTube. Unity keeps web PlayerPrefs in the browser's IndexedDB.
    /// - Cloud blob: YouTube Playables, which requires every save to go through
    ///   ytgame.game.saveData/loadData and forbids any other save mechanism. All keys live in one
    ///   versioned JSON string. PlatformBootstrap loads it before the game scene opens and sends it
    ///   back to YouTube when Save() marks it dirty (throttled), and immediately on pause.
    /// </summary>
    public static class PlatformPrefs
    {
        private static IStore _store = new PlayerPrefsStore();

        /// <summary>True while the YouTube cloud-blob backend is active.</summary>
        public static bool UsingCloudStore => _store is CloudStore;

        /// <summary>Called by PlatformBootstrap once YouTube's loadData() returns (or times out).
        /// json may be null/empty for a first-time player.</summary>
        public static void UseCloudStore(string json)
        {
            _store = new CloudStore(json);
        }

        public static int GetInt(string key, int defaultValue = 0) => _store.GetInt(key, defaultValue);
        public static void SetInt(string key, int value) => _store.SetInt(key, value);
        public static float GetFloat(string key, float defaultValue = 0f) => _store.GetFloat(key, defaultValue);
        public static void SetFloat(string key, float value) => _store.SetFloat(key, value);
        public static string GetString(string key, string defaultValue = "") => _store.GetString(key, defaultValue);
        public static void SetString(string key, string value) => _store.SetString(key, value);
        public static bool HasKey(string key) => _store.HasKey(key);
        public static void DeleteKey(string key) => _store.DeleteKey(key);

        /// <summary>Same meaning as PlayerPrefs.Save(): "this is a good moment to persist".</summary>
        public static void Save() => _store.Save();

        /// <summary>Cloud backend only: true once Save() has been called since the last upload.</summary>
        public static bool IsDirty => _store is CloudStore cloud && cloud.Dirty;

        /// <summary>Cloud backend only: the JSON to upload; clears the dirty flag.</summary>
        public static string TakeSerializedForUpload()
        {
            return _store is CloudStore cloud ? cloud.Serialize(clearDirty: true) : null;
        }

        private interface IStore
        {
            int GetInt(string key, int defaultValue);
            void SetInt(string key, int value);
            float GetFloat(string key, float defaultValue);
            void SetFloat(string key, float value);
            string GetString(string key, string defaultValue);
            void SetString(string key, string value);
            bool HasKey(string key);
            void DeleteKey(string key);
            void Save();
        }

        private sealed class PlayerPrefsStore : IStore
        {
            public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
            public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
            public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
            public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
            public bool HasKey(string key) => PlayerPrefs.HasKey(key);
            public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
            public void Save() => PlayerPrefs.Save();
        }

        /// <summary>Serialized shape of the YouTube save. JsonUtility can't serialize dictionaries,
        /// so each type is stored as parallel key/value lists. "version" lets a later demo build
        /// read an older save (a YouTube certification requirement).</summary>
        [Serializable]
        private class SaveBlob
        {
            public int version = CloudStore.CurrentVersion;
            public List<string> intKeys = new List<string>();
            public List<int> intValues = new List<int>();
            public List<string> floatKeys = new List<string>();
            public List<float> floatValues = new List<float>();
            public List<string> stringKeys = new List<string>();
            public List<string> stringValues = new List<string>();
        }

        private sealed class CloudStore : IStore
        {
            public const int CurrentVersion = 1;

            private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public bool Dirty { get; private set; }

            public CloudStore(string json)
            {
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }
                try
                {
                    var blob = JsonUtility.FromJson<SaveBlob>(json);
                    if (blob == null)
                    {
                        return;
                    }
                    // Version 1 is the only format so far. A future version should migrate older
                    // blobs here rather than discard them.
                    for (int i = 0; i < blob.intKeys.Count && i < blob.intValues.Count; i++) _ints[blob.intKeys[i]] = blob.intValues[i];
                    for (int i = 0; i < blob.floatKeys.Count && i < blob.floatValues.Count; i++) _floats[blob.floatKeys[i]] = blob.floatValues[i];
                    for (int i = 0; i < blob.stringKeys.Count && i < blob.stringValues.Count; i++) _strings[blob.stringKeys[i]] = blob.stringValues[i];
                }
                catch (Exception e)
                {
                    // A corrupt save must not crash the game (certification requirement) - start
                    // fresh instead.
                    Debug.LogWarning($"[PlatformPrefs] Could not read YouTube save, starting fresh: {e.Message}");
                }
            }

            public string Serialize(bool clearDirty)
            {
                var blob = new SaveBlob();
                foreach (var kv in _ints) { blob.intKeys.Add(kv.Key); blob.intValues.Add(kv.Value); }
                foreach (var kv in _floats) { blob.floatKeys.Add(kv.Key); blob.floatValues.Add(kv.Value); }
                foreach (var kv in _strings) { blob.stringKeys.Add(kv.Key); blob.stringValues.Add(kv.Value); }
                if (clearDirty)
                {
                    Dirty = false;
                }
                return JsonUtility.ToJson(blob);
            }

            // A key holds one type at a time, matching PlayerPrefs (setting a key as an int
            // replaces it as a float/string, and a getter of the wrong type returns the default).
            public int GetInt(string key, int defaultValue) => _ints.TryGetValue(key, out var v) ? v : defaultValue;
            public void SetInt(string key, int value) { RemoveEverywhere(key); _ints[key] = value; }
            public float GetFloat(string key, float defaultValue) => _floats.TryGetValue(key, out var v) ? v : defaultValue;
            public void SetFloat(string key, float value) { RemoveEverywhere(key); _floats[key] = value; }
            public string GetString(string key, string defaultValue) => _strings.TryGetValue(key, out var v) ? v : defaultValue;
            public void SetString(string key, string value) { RemoveEverywhere(key); _strings[key] = value ?? string.Empty; }
            public bool HasKey(string key) => _ints.ContainsKey(key) || _floats.ContainsKey(key) || _strings.ContainsKey(key);
            public void DeleteKey(string key) => RemoveEverywhere(key);
            public void Save() => Dirty = true;

            private void RemoveEverywhere(string key)
            {
                _ints.Remove(key);
                _floats.Remove(key);
                _strings.Remove(key);
            }
        }
    }
}
