namespace FuzzPhyte.Utility.Interactive.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using FuzzPhyte.Utility.Interactive;
    [CustomEditor(typeof(FPVariantConfig))]
    public class FPVariantConfigEditor:Editor
    {
        // UI state (not serialized on the asset)
        private GameObject _sourceRoot;
        private bool _clearBeforeBuild = true;
        private bool _includeInactive = true;
        private bool _includeSkinned = true;

        public override void OnInspectorGUI()
        {
            // Draw the regular inspector first
            base.OnInspectorGUI();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MeshSets Builder", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _sourceRoot = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Source Root", "Root object whose hierarchy will be scanned for meshes"),
                    _sourceRoot, typeof(GameObject), true);

                _clearBeforeBuild = EditorGUILayout.ToggleLeft("Clear existing MeshSets first", _clearBeforeBuild);
                _includeInactive = EditorGUILayout.ToggleLeft("Include inactive children", _includeInactive);
                _includeSkinned = EditorGUILayout.ToggleLeft("Include Skinned Meshes", _includeSkinned);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Build MeshSets From Source", GUILayout.Height(26)))
                    {
                        BuildFrom(_sourceRoot);
                    }
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Clear MeshSets"))
                {
                    var cfg = (FPVariantConfig)target;
                    Undo.RecordObject(cfg, "Clear MeshSets");
                    cfg.MeshSets.Clear();
                    EditorUtility.SetDirty(cfg);
                }
            }
        }

        private void BuildFrom(GameObject root)
        {
            var cfg = (FPVariantConfig)target;

            if (!root)
            {
                EditorUtility.DisplayDialog("No Source", "Please assign a Source Root (or select one in the hierarchy).", "OK");
                return;
            }

            // Collect pieces
            var newSets = new List<FPMeshMaterialSet>();

            // MeshRenderer + MeshFilter path
            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(_includeInactive);
            foreach (var mr in meshRenderers)
            {
                var tf = mr.transform;
                var mf = tf.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;

                var set = FPMeshMaterialSetHelper.CreateSetFromPiece(root.transform, tf,
                    mesh: mf.sharedMesh,
                    sharedMats: mr.sharedMaterials,
                    useSkinned: false);

                newSets.Add(set);
            }

            // SkinnedMeshRenderer path (optional)
            if (_includeSkinned)
            {
                var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(_includeInactive);
                foreach (var smr in smrs)
                {
                    if (!smr || !smr.sharedMesh) continue;
                    var tf = smr.transform;

                    var set = FPMeshMaterialSetHelper.CreateSetFromPiece(root.transform, tf,
                        mesh: smr.sharedMesh,
                        sharedMats: smr.sharedMaterials,
                        useSkinned: true);

                    newSets.Add(set);
                }
            }

            if (newSets.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing Found",
                    "No MeshFilters with MeshRenderers (and/or SkinnedMeshRenderers) found under the source root.",
                    "OK");
                return;
            }

            // Write to asset
            Undo.RecordObject(cfg, "Build MeshSets From Source");
            if (_clearBeforeBuild) cfg.MeshSets.Clear();
            cfg.MeshSets.AddRange(newSets);

            // Mark asset dirty
            EditorUtility.SetDirty(cfg);
            Debug.Log($"FPVariantConfig: Added {newSets.Count} MeshSet(s) from '{root.name}'.");
        }

        
    }
}
