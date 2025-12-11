namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Editor window to combine multiple meshes into a single mesh using submeshes.
    /// The combined mesh is baked using world-space transforms into the local space
    /// of a chosen root object. Intended mostly for generating MeshCollider assets.
    /// </summary>
    public class FPMeshCombineEditor:EditorWindow
    {
        // Root under which we search for meshes (or null to use explicit selection)
        [SerializeField]
        private GameObject rootObject;

        // General options
        [SerializeField]
        private bool includeChildren = true;
        [SerializeField]
        private bool includeInactive = true;
        [SerializeField]
        private bool includeMeshColliders = true;
        [SerializeField]
        private bool includeMeshFilters = true;
        [SerializeField]
        private bool skipEditorOnlyTagged = true;

        // Output options
        [SerializeField]
        private string combinedMeshName = "FP_CombinedCollider";
        [SerializeField]
        private bool addMeshColliderToRoot = true;
        [SerializeField]
        private bool replaceExistingMeshCollider = true;
        [SerializeField]
        private bool makeColliderConvex = false;
        [SerializeField]
        private bool isTrigger = false;

        private Vector2 scrollPos;

        [MenuItem("FuzzPhyte/Utility/Rendering/FP Mesh Combiner", priority = FP_UtilityData.ORDER_MENU + 5)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshCombineEditor>("FP Mesh Combiner");
            window.minSize = new Vector2(350f, 260f);
            window.InitDefaults();
        }

        private void InitDefaults()
        {
            if (Selection.activeGameObject != null)
            {
                rootObject = Selection.activeGameObject;
                combinedMeshName = $"{rootObject.name}_CombinedCollider";
            }
        }

        private void OnGUI()
        {
            if (rootObject == null && Selection.activeGameObject != null)
            {
                // soft default to current selection on first open
                rootObject = Selection.activeGameObject;
                if (string.IsNullOrEmpty(combinedMeshName))
                    combinedMeshName = $"{rootObject.name}_CombinedCollider";
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scroll.scrollPosition;

                DrawHeader();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawRootSettings();
                EditorGUILayout.Space();

                DrawSourceSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawOutputSettings();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawActionButtons();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("FP Mesh Combiner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Combine multiple MeshFilters / MeshColliders into a single Mesh asset, " +
                "baked using world-space positions into the local space of the selected root.\n\n" +
                "Ideal for generating a single MeshCollider from many scene meshes.",
                MessageType.Info);
        }

        private void DrawRootSettings()
        {
            EditorGUILayout.LabelField("Root / Scope", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (rootObject != null && string.IsNullOrEmpty(combinedMeshName))
                {
                    combinedMeshName = $"{rootObject.name}_CombinedCollider";
                }
            }

            using (new EditorGUI.DisabledScope(rootObject == null))
            {
                if (GUILayout.Button("Use Current Selection As Root"))
                {
                    rootObject = Selection.activeGameObject;
                    if (rootObject != null)
                    {
                        combinedMeshName = $"{rootObject.name}_CombinedCollider";
                    }
                }
            }

            EditorGUILayout.Space();
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.LabelField("Source Mesh Settings", EditorStyles.boldLabel);

            includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);
            includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

            EditorGUILayout.Space();
            includeMeshFilters = EditorGUILayout.Toggle("Include MeshFilters", includeMeshFilters);
            includeMeshColliders = EditorGUILayout.Toggle("Include MeshColliders", includeMeshColliders);

            skipEditorOnlyTagged = EditorGUILayout.Toggle("Skip 'EditorOnly' Tagged Objects", skipEditorOnlyTagged);

            EditorGUILayout.Space();

            int count = PreviewMeshCount();
            EditorGUILayout.LabelField("Meshes Found (Preview)", count.ToString());
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);

            combinedMeshName = EditorGUILayout.TextField("Combined Mesh Name", combinedMeshName);

            EditorGUILayout.Space();
            addMeshColliderToRoot = EditorGUILayout.Toggle("Add MeshCollider to Root", addMeshColliderToRoot);

            using (new EditorGUI.DisabledScope(!addMeshColliderToRoot))
            {
                replaceExistingMeshCollider = EditorGUILayout.Toggle("Replace Existing Collider", replaceExistingMeshCollider);
                makeColliderConvex = EditorGUILayout.Toggle("Collider Convex", makeColliderConvex);
                isTrigger = EditorGUILayout.Toggle("Collider Is Trigger", isTrigger);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space();

            bool canCombine = rootObject != null && PreviewMeshCount() > 0;

            using (new EditorGUI.DisabledScope(!canCombine))
            {
                Color defaultColor = GUI.color;

                if (canCombine)
                {
                    GUI.color = FP_Utility_Editor.OkayColor;
                }

                if (GUILayout.Button("Combine Meshes and Save Asset", GUILayout.Height(32)))
                {
                    CombineAndSave();
                }

                GUI.color = defaultColor;
            }

            if (!canCombine)
            {
                EditorGUILayout.HelpBox(
                    "Assign a root object and ensure there are MeshFilters and/or MeshColliders under it.",
                    MessageType.Warning);
            }
        }

        // --- Core Logic ------------------------------------------------------

        private int PreviewMeshCount()
        {
            if (rootObject == null)
                return 0;

            var filters = GetSourceMeshFilters();
            return filters.Count;
        }

        private List<MeshFilter> GetSourceMeshFilters()
        {
            var result = new List<MeshFilter>();

            if (!includeMeshFilters && !includeMeshColliders)
                return result;

            if (rootObject == null)
                return result;

            // Search hierarchy under root
            var flags = includeInactive
                ? System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                : System.Reflection.BindingFlags.Public; // we'll just use GetComponentsInChildren overload instead

            var filters = rootObject.GetComponentsInChildren<MeshFilter>(includeInactive);
            if (includeMeshFilters)
            {
                foreach (var mf in filters)
                {
                    if (!IsValidSourceObject(mf.gameObject))
                        continue;

                    if (mf.sharedMesh != null)
                        result.Add(mf);
                }
            }

            if (includeMeshColliders)
            {
                var colliders = rootObject.GetComponentsInChildren<MeshCollider>(includeInactive);
                foreach (var col in colliders)
                {
                    if (!IsValidSourceObject(col.gameObject))
                        continue;

                    if (col.sharedMesh == null)
                        continue;

                    // Prefer the MeshFilter if present, but fall back to collider’s mesh
                    var mf = col.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        if (!result.Contains(mf))
                            result.Add(mf);
                    }
                    else
                    {
                        // Create an ephemeral GameObject to host this mesh via MeshFilter-like behaviour
                        // but for collision-only, we can treat collider as a "filter" using its transform.
                        // We'll add a fake MeshFilter entry by temporarily adding one and not saving it.
                        var fakeFilter = col.gameObject.GetComponent<MeshFilter>();
                        if (fakeFilter == null)
                        {
                            fakeFilter = col.gameObject.AddComponent<MeshFilter>();
                            fakeFilter.hideFlags = HideFlags.HideAndDontSave;
                            fakeFilter.sharedMesh = col.sharedMesh;
                        }

                        if (!result.Contains(fakeFilter) && fakeFilter.sharedMesh != null)
                            result.Add(fakeFilter);
                    }
                }
            }

            return result;
        }

        private bool IsValidSourceObject(GameObject go)
        {
            if (skipEditorOnlyTagged && go.CompareTag("EditorOnly"))
                return false;

            return true;
        }

        private void CombineAndSave()
        {
            if (rootObject == null)
            {
                Debug.LogError("[FP Mesh Combiner] Root object is null.");
                return;
            }

            var sources = GetSourceMeshFilters();
            if (sources.Count == 0)
            {
                Debug.LogWarning("[FP Mesh Combiner] No valid source meshes found.");
                return;
            }

            // Build CombineInstances with per-submesh data
            var combineInstances = new List<CombineInstance>();
            var rootToLocal = rootObject.transform.worldToLocalMatrix;

            foreach (var mf in sources)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null)
                    continue;

                var localToWorld = mf.transform.localToWorldMatrix;
                var xform = rootToLocal * localToWorld;

                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int s = 0; s < subMeshCount; s++)
                {
                    var ci = new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = s,
                        transform = xform
                    };
                    combineInstances.Add(ci);
                }
            }

            if (combineInstances.Count == 0)
            {
                Debug.LogWarning("[FP Mesh Combiner] No CombineInstances created. Aborting.");
                return;
            }

            // Create combined mesh
            var combinedMesh = new Mesh();
            combinedMesh.name = string.IsNullOrEmpty(combinedMeshName)
                ? "FP_CombinedCollider"
                : combinedMeshName;

            // Use 32-bit indices if needed
            int estimatedVertexCount = 0;
            foreach (var ci in combineInstances)
            {
                if (ci.mesh != null)
                    estimatedVertexCount += ci.mesh.vertexCount;
            }

            if (estimatedVertexCount > 65535)
            {
                combinedMesh.indexFormat = IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(
                combineInstances.ToArray(),
                mergeSubMeshes: false,   // keep submeshes for debugging / optional use
                useMatrices: true        // bake transforms into vertices
            );

            // Ask user where to save the asset
            string defaultName = string.IsNullOrEmpty(combinedMeshName)
                ? "FP_CombinedCollider"
                : combinedMeshName;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Combined Mesh",
                defaultName,
                "asset",
                "Choose a location to save the combined mesh asset."
            );

            if (string.IsNullOrEmpty(path))
            {
                DestroyImmediate(combinedMesh);
                return;
            }

            string result = FP_Utility_Editor.CreateAssetAt(combinedMesh, path);
            Debug.Log($"[FP Mesh Combiner] {result}");

            // Optionally hook up a MeshCollider on the root
            if (addMeshColliderToRoot)
            {
                var rootCollider = rootObject.GetComponent<MeshCollider>();
                if (rootCollider == null)
                {
                    rootCollider = rootObject.AddComponent<MeshCollider>();
                }
                else if (!replaceExistingMeshCollider)
                {
                    // If we're not allowed to replace, add a new child with the collider instead
                    var child = new GameObject(defaultName + "_Collider");
                    child.transform.SetParent(rootObject.transform, false);
                    rootCollider = child.AddComponent<MeshCollider>();
                }

                rootCollider.sharedMesh = combinedMesh;
                rootCollider.convex = makeColliderConvex;
                rootCollider.isTrigger = isTrigger;
            }

            EditorUtility.DisplayDialog(
                "FP Mesh Combiner",
                $"Combined mesh created with {combinedMesh.subMeshCount} submeshes.\nSaved to:\n{path}",
                "OK"
            );
        }
    }
}
