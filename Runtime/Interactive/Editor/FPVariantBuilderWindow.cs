namespace FuzzPhyte.Utility.Interactive.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using FuzzPhyte.Utility.Interactive;
    public class FPVariantBuilderWindow : EditorWindow
    {
        private FPVariantConfig config;
        // Preview state
        private FPVariantPreview preview;
        // Data
        private GameObject inlineBasePrefab;
        private Mesh inlineMesh;
        private List<Material> inlineMats = new();
        private List<FPVariantColliderSpec> inlineCols = new();
        private string inlineVariantName = "NewVariant";
        private string inlineFolder = "Assets/Variants";
        [MenuItem("FuzzPhyte/Variant Builder")]
        public static void Open() => GetWindow<FPVariantBuilderWindow>("FP Variant Builder");

        private void OnDestroy() => CleanupPreview();

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variant Source", EditorStyles.boldLabel);


            config = (FPVariantConfig)EditorGUILayout.ObjectField("Config (optional)", config, typeof(FPVariantConfig), false);
            if (config == null)
            {
                // Inline lightweight config when no SO is provided
                EditorGUILayout.HelpBox("No ScriptableObject config linked – editing inline session config.", MessageType.Info);
            }


            DrawConfigFields();


            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview In Scene"))
            {
                DoPreview();
            }
            if (GUILayout.Button("Rebuild Colliders"))
            {
                if (preview)
                {
                    preview.RebuildColliders();
                }
            }
            if (GUILayout.Button("Save As Variant Prefab"))
            {
                SaveVariant();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (preview)
            {
                EditorGUILayout.HelpBox("Select colliders in the Hierarchy under the preview to manipulate with scene handles. Use the Variant Preview inspector for quick fit helpers.", MessageType.None);
            }


        }
        #region Variant Steps
        private void DrawConfigFields()
        {
            SerializedObject so = null; 
            SerializedProperty spCols = null;
            if (config)
            {
                so = new SerializedObject(config);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("basePrefab"));
                EditorGUILayout.PropertyField(so.FindProperty("visualMesh"));
                EditorGUILayout.PropertyField(so.FindProperty("visualMaterials"), true);
                spCols = so.FindProperty("colliders");
                EditorGUILayout.PropertyField(spCols, true);
                EditorGUILayout.PropertyField(so.FindProperty("variantName"));
                EditorGUILayout.PropertyField(so.FindProperty("saveFolder"));
                so.ApplyModifiedProperties();
            }
            else
            {
                inlineBasePrefab = (GameObject)EditorGUILayout.ObjectField("Base Prefab", inlineBasePrefab, typeof(GameObject), false);
                inlineMesh = (Mesh)EditorGUILayout.ObjectField("Visual Mesh", inlineMesh, typeof(Mesh), false);
                EditorGUILayout.LabelField("Visual Materials");
                using (new EditorGUI.IndentLevelScope())
                {
                    int remove = -1;
                    for (int i = 0; i < inlineMats.Count; i++)
                    {
                        inlineMats[i] = (Material)EditorGUILayout.ObjectField($"Element {i}", inlineMats[i], typeof(Material), false);
                        if (GUILayout.Button("Remove Material")) remove = i;
                    }
                    if (remove >= 0) inlineMats.RemoveAt(remove);
                    if (GUILayout.Button("Add Material")) inlineMats.Add(null);
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add Collider"))
                {
                    inlineCols.Add(
                        new FPVariantColliderSpec 
                        { 
                            name = $"Col {inlineCols.Count}", 
                            localScale = Vector3.one, 
                            radius = 0.25f, 
                            height = 0.5f 
                        });
                }

                for (int i = 0; i < inlineCols.Count; i++)
                {
                    EditorGUILayout.BeginVertical("box");
                    var c = inlineCols[i];
                    c.name = EditorGUILayout.TextField("Name", c.name);
                    c.type = (FPVariantColliderType)EditorGUILayout.EnumPopup("Type", c.type);
                    c.isTrigger = EditorGUILayout.Toggle("Is Trigger", c.isTrigger);
                    c.material = (PhysicsMaterial)EditorGUILayout.ObjectField("Physic Material", c.material, typeof(PhysicsMaterial), false);
                    c.localPosition = EditorGUILayout.Vector3Field("Local Pos", c.localPosition);
                    c.localEuler = EditorGUILayout.Vector3Field("Local Euler", c.localEuler);


                    switch (c.type)
                    {
                        case FPVariantColliderType.Box:
                            c.localScale = EditorGUILayout.Vector3Field("Size", c.localScale == Vector3.zero ? Vector3.one : c.localScale);
                            c.fitToVisual = EditorGUILayout.Toggle("Fit To Visual Bounds", c.fitToVisual);
                            break;
                        case FPVariantColliderType.Sphere:
                            c.radius = EditorGUILayout.FloatField("Radius", Mathf.Max(0.001f, c.radius));
                            break;
                        case FPVariantColliderType.Capsule:
                            c.radius = EditorGUILayout.FloatField("Radius", Mathf.Max(0.001f, c.radius));
                            c.height = EditorGUILayout.FloatField("Height", Mathf.Max(0.001f, c.height));
                            c.direction = EditorGUILayout.IntSlider("Direction (0=X,1=Y,2=Z)", Mathf.Clamp(c.direction, 0, 2), 0, 2);
                            break;
                        case FPVariantColliderType.Mesh:
                            c.meshForMeshCollider = (Mesh)EditorGUILayout.ObjectField("Collider Mesh (opt)", c.meshForMeshCollider, typeof(Mesh), false);
                            c.convex = EditorGUILayout.Toggle("Convex", c.convex);
                            break;
                    }


                    if (GUILayout.Button("Remove")) { inlineCols.RemoveAt(i); i--; }
                    else inlineCols[i] = c;
                    EditorGUILayout.EndVertical();
                }
                inlineVariantName = EditorGUILayout.TextField("Variant Name", inlineVariantName);
                inlineFolder = EditorGUILayout.TextField("Save Folder", inlineFolder);
            }
        }
        private void DoPreview()
        {
            var basePrefab = config ? config.basePrefab : inlineBasePrefab;
            if (!basePrefab)
            {
                EditorUtility.DisplayDialog("Missing Base Prefab", "Please assign a Base Prefab.", "OK");
                return;
            }


            // Spawn fresh preview root
            CleanupPreview();


            var baseInstance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            Undo.RegisterCreatedObjectUndo(baseInstance, "Create Preview");


            var root = baseInstance.GetComponent<FPInteractiveItemRoot>();
            if (root == null)
            {
                root = baseInstance.AddComponent<FPInteractiveItemRoot>();
            }


            var previewGo = new GameObject("FP_VariantPreview");
            previewGo.transform.SetParent(root.VisualRoot, false);
            preview = previewGo.AddComponent<FPVariantPreview>();
            preview.baseInstance = baseInstance;
            preview.visualsRoot = root.VisualRoot;
            preview.colliderRoot = root.ColliderRoot;


            // Visuals
            preview.workingMesh = config ? config.visualMesh : inlineMesh;
            var mats = config ? new List<Material>(config.visualMaterials) : inlineMats;
            preview.workingMaterials = new List<Material>(mats);
            preview.EnsureVisual();


            // Colliders
            preview.workingColliders = new List<FPVariantColliderSpec>(config ? config.colliders : inlineCols);


            // Auto-fit any requested boxes now
            AutoFitBoxes(preview);
            preview.RebuildColliders();


            Selection.activeGameObject = baseInstance;
            EditorSceneManager.MarkSceneDirty(baseInstance.scene);
        }
        private void AutoFitBoxes(FPVariantPreview p)
        {
            if (!p.workingMesh || p.visualsRoot == null) return;
            var bounds = p.workingMesh.bounds; // local space
            for (int i = 0; i < p.workingColliders.Count; i++)
            {
                var spec = p.workingColliders[i];
                if (spec.type == FPVariantColliderType.Box && spec.fitToVisual)
                {
                    spec.localPosition = bounds.center;
                    spec.localScale = bounds.size;
                    spec.localEuler = Vector3.zero;
                    p.workingColliders[i] = spec;
                }
            }
        }
        private void SaveVariant()
        {
            if (!preview || !preview.baseInstance)
            {
                EditorUtility.DisplayDialog("No Preview", "Create a preview first.", "OK");
                return;
            }


            var root = preview.baseInstance.GetComponent<FPInteractiveItemRoot>();
            if (!root) root = preview.baseInstance.AddComponent<FPInteractiveItemRoot>();


            var variantName = config ? config.variantName : inlineVariantName;
            var folder = config ? config.saveFolder : inlineFolder;
            if (string.IsNullOrEmpty(variantName)) variantName = "NewVariant";
            if (string.IsNullOrEmpty(folder)) folder = "Assets/Variants";


            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = System.IO.Path.GetDirectoryName(folder);
                var leaf = System.IO.Path.GetFileName(folder);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EditorUtility.DisplayDialog("Invalid Folder", $"Parent folder not found: {parent}", "OK");
                    return;
                }
                AssetDatabase.CreateFolder(parent, leaf);
            }


            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{variantName}.prefab");
            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(preview.baseInstance, path, InteractionMode.UserAction);
            if (saved)
            {
                EditorUtility.DisplayDialog("Saved", $"Variant saved to:\n{path}", "OK");
            }
        }
        private void CleanupPreview()
        {
            if (preview)
            {
                if (preview.baseInstance) Undo.DestroyObjectImmediate(preview.baseInstance);
                if (preview.gameObject) Undo.DestroyObjectImmediate(preview.gameObject);
                preview = null;
            }
        }

        #endregion
    }
}