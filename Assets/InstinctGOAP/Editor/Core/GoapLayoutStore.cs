using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Instinct.GOAP.EditorTools
{
    public static class GoapLayoutStore
    {
        private const string Folder = "UserSettings/InstinctGOAP";

        [Serializable]
        private class Entry
        {
            public string Key;
            public float X;
            public float Y;
        }

        [Serializable]
        private class LayoutData
        {
            public string Domain;
            public string SavedAt;
            public List<Entry> Nodes = new List<Entry>();
        }

        private static string PathFor(string domain) => $"{Folder}/{Sanitize(domain)}.json";

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "default";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        public static bool Has(string domain) =>
            !string.IsNullOrEmpty(domain) && File.Exists(PathFor(domain));

        public static void Save(string domain, IReadOnlyDictionary<string, Vector2> positions)
        {
            if (string.IsNullOrEmpty(domain) || positions == null || positions.Count == 0) return;

            var data = new LayoutData
            {
                Domain = domain,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            foreach (var kv in positions)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                data.Nodes.Add(new Entry { Key = kv.Key, X = kv.Value.x, Y = kv.Value.y });
            }

            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathFor(domain), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GOAP] could not save the graph layout - {e.Message}");
            }
        }

        public static Dictionary<string, Vector2> Load(string domain)
        {
            var result = new Dictionary<string, Vector2>();
            if (!Has(domain)) return result;

            try
            {
                var data = JsonUtility.FromJson<LayoutData>(File.ReadAllText(PathFor(domain)));
                if (data?.Nodes == null) return result;
                foreach (var e in data.Nodes)
                    if (!string.IsNullOrEmpty(e.Key))
                        result[e.Key] = new Vector2(e.X, e.Y);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GOAP] could not read the saved graph layout - {e.Message}");
            }
            return result;
        }

        public static void Delete(string domain)
        {
            if (!Has(domain)) return;
            try { File.Delete(PathFor(domain)); }
            catch (Exception e) { Debug.LogWarning($"[GOAP] could not delete the saved layout - {e.Message}"); }
        }
    }
}
