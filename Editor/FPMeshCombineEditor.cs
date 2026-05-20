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
        private bool includeSkinnedMeshRenderers = true;
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

        [MenuItem("FuzzPhyte/Utility/Rendering/FP Mesh Combiner", priority = FP_UtilityData.MENU_UTILITY_RENDERING + 1)]
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
                "Combine multiple MeshFilters / SkinnedMeshRenderers / MeshColliders into a single Mesh asset, " +
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
            includeSkinnedMeshRenderers = EditorGUILayout.Toggle("Include SkinnedMeshRenderers", includeSkinnedMeshRenderers);
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
                    "Assign a root object and ensure there are MeshFilters, SkinnedMeshRenderers, and/or MeshColliders under it.",
                    MessageType.Warning);
            }
        }

        // --- Core Logic ------------------------------------------------------

        private struct MeshSource
        {
            public Mesh Mesh;
            public Transform Transform;

            public MeshSource(Mesh mesh, Transform transform)
            {
                Mesh = mesh;
                Transform = transform;
            }
        }

        private int PreviewMeshCount()
        {
            if (rootObject == null)
                return 0;

            var sources = GetSourceMeshes();
            return sources.Count;
        }

        private List<MeshSource> GetSourceMeshes()
        {
            var result = new List<MeshSource>();
            var sourceComponents = new HashSet<Component>();

            if (!includeMeshFilters && !includeSkinnedMeshRenderers && !includeMeshColliders)
                return result;

            if (rootObject == null)
                return result;

            if (includeMeshFilters)
            {
                var filters = includeChildren
                    ? rootObject.GetComponentsInChildren<MeshFilter>(includeInactive)
                    : rootObject.GetComponents<MeshFilter>();

                foreach (var mf in filters)
                {
                    if (!IsValidSourceObject(mf.gameObject))
                        continue;

                    if (mf.sharedMesh != null)
                    {
                        result.Add(new MeshSource(mf.sharedMesh, mf.transform));
                        sourceComponents.Add(mf);
                    }
                }
            }

            if (includeSkinnedMeshRenderers)
            {
                var skinnedRenderers = includeChildren
                    ? rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive)
                    : rootObject.GetComponents<SkinnedMeshRenderer>();

                foreach (var smr in skinnedRenderers)
                {
                    if (!IsValidSourceObject(smr.gameObject))
                        continue;

                    if (smr.sharedMesh != null)
                    {
                        result.Add(new MeshSource(smr.sharedMesh, smr.transform));
                        sourceComponents.Add(smr);
                    }
                }
            }

            if (includeMeshColliders)
            {
                var colliders = includeChildren
                    ? rootObject.GetComponentsInChildren<MeshCollider>(includeInactive)
                    : rootObject.GetComponents<MeshCollider>();

                foreach (var col in colliders)
                {
                    if (!IsValidSourceObject(col.gameObject))
                        continue;

                    if (col.sharedMesh == null)
                        continue;

                    // Prefer visual mesh sources when present, but fall back to the collider's mesh.
                    var mf = col.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && sourceComponents.Contains(mf))
                        continue;

                    var smr = col.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh != null && sourceComponents.Contains(smr))
                        continue;

                    result.Add(new MeshSource(col.sharedMesh, col.transform));
                    sourceComponents.Add(col);
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

            var sources = GetSourceMeshes();
            if (sources.Count == 0)
            {
                Debug.LogWarning("[FP Mesh Combiner] No valid source meshes found.");
                return;
            }

            // Build CombineInstances with per-submesh data
            var combineInstances = new List<CombineInstance>();
            var rootToLocal = rootObject.transform.worldToLocalMatrix;

            foreach (var source in sources)
            {
                var mesh = source.Mesh;
                if (mesh == null)
                    continue;

                var localToWorld = source.Transform.localToWorldMatrix;
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
