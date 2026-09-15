// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Read-only labels for saved YAML references, including unresolved prefab instances.</summary>
    internal sealed class FPAssetTracerIdentity
    {
        private readonly Match[] documents;
        private readonly Dictionary<string, Match> byId;

        internal FPAssetTracerIdentity(string text)
        {
            documents = Regex.Matches(text,
                @"^--- !u!(?<type>\d+) &(?<id>-?\d+)[^\r\n]*\r?\n(?:(?!^--- !u!).)*",
                RegexOptions.Multiline | RegexOptions.Singleline).Cast<Match>().ToArray();
            byId = documents.ToDictionary(m => m.Groups["id"].Value);
        }

        internal static string Asset(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return $"Unresolved asset (GUID: {guid}; name unavailable)";
            bool missing = FPAssetTracerCleanup.Missing(guid);
            return $"{Path.GetFileNameWithoutExtension(path)} (GUID: {guid}; {(missing ? "missing, last known path: " : "path: ")}{path})";
        }

        internal static string ObjectId(Object value)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)
                ? $" [GUID: {guid}; fileID: {id}]" : "";
        }

        internal static string SourceTarget(string text)
        {
            var target = Regex.Match(text, @"target: \{fileID: (-?\d+), guid: ([a-f0-9]{32})");
            if (!target.Success) return "";
            string id = target.Groups[1].Value, guid = target.Groups[2].Value;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object found = null;
            if (!string.IsNullOrEmpty(path) && !FPAssetTracerCleanup.Missing(guid))
            {
                var objects = new List<Object>(AssetDatabase.LoadAllAssetsAtPath(path));
                foreach (var root in objects.OfType<GameObject>().ToArray())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects.Add(transform.gameObject);
                        objects.AddRange(transform.GetComponents<Component>());
                    }
                found = objects.FirstOrDefault(o => o != null &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string objectGuid, out long localId) &&
                    objectGuid == guid && localId.ToString() == id);
            }
            return "Override target: " + (found != null ? $"{found.name} [{found.GetType().Name}]" : "Object/component name unavailable") +
                $" (fileID: {id}) in {Asset(guid)}";
        }

        internal string At(int offset)
        {
            var document = documents.FirstOrDefault(m => offset >= m.Index && offset < m.Index + m.Length);
            if (document == null) return "Owner unavailable in saved text.";
            string id = document.Groups["id"].Value;
            if (document.Groups["type"].Value == "1001")
            {
                string guid = Regex.Match(document.Value, @"m_SourcePrefab: \{[^\r\n]*guid: ([a-f0-9]{32})").Groups[1].Value;
                var names = Regex.Matches(document.Value, @"propertyPath: m_Name\r?\n      value: ([^\r\n]+)")
                    .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
                return $"Prefab instance (fileID: {id}) → {Asset(guid)}" +
                    (names.Length > 0 ? "\nSaved GameObject name overrides in this instance: " + string.Join(", ", names) : "\nInstance GameObject name not preserved here; prefab asset name may differ from the scene name.");
            }
            string gameObject = Regex.Match(document.Value, @"m_GameObject: \{fileID: (-?\d+)\}").Groups[1].Value;
            string owner = Name(gameObject.Length > 0 ? gameObject : id);
            string type = Regex.Match(document.Value, @"\r?\n([A-Za-z0-9_]+):").Groups[1].Value;
            string script = Regex.Match(document.Value, @"m_Script: \{[^\r\n]*guid: ([a-f0-9]{32})").Groups[1].Value;
            return $"GameObject/owner: {owner} [{type}; fileID: {id}]" + (script.Length > 0 ? "\nComponent script: " + Asset(script) : "");
        }

        private string Name(string id)
        {
            if (!byId.TryGetValue(id, out var document)) return $"Unresolved GameObject (fileID: {id})";
            var name = Regex.Match(document.Value, @"^  m_Name: ([^\r\n]*)", RegexOptions.Multiline);
            return name.Success ? $"{name.Groups[1].Value} (GameObject fileID: {id})" : $"Name unavailable (fileID: {id})";
        }
    }
}
