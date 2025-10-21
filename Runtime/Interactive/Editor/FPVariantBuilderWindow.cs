namespace FuzzPhyte.Utility.Interactive.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using FuzzPhyte.Utility.Interactive;
    using FuzzPhyte.Utility.Editor;
    using System.Reflection;
    using UnityEditorInternal;

    public class FPVariantBuilderWindow : EditorWindow
    {
        protected FPVariantConfig config;
        protected FPVariantPreview preview;
        protected GameObject inlineBasePrefab;
        protected Mesh inlineMesh;
        protected List<Material> inlineMats = new();
        protected List<FPVariantColliderSpec> inlineCols = new();
        protected string inlineVariantName = "NewVariant";
        protected string inlineFolder = "Assets/Variants";
        
        protected ReorderableList _colliderListUI;
        protected int _selectedColliderIndex = -1;

        #region InLine MeshSets Mode without Config File
        protected List<FPMeshMaterialSet> inlineMeshSets = new();
        protected GameObject inlineSourceRoot;
        protected bool inlineIncludeInactive = true;
        protected bool inlineIncludeSkinned = true;
        protected bool inlineClearBeforeBuild = true;
        [SerializeField] private float _inlineColliderPanelHeight = 220;
        [SerializeField] private float _inlineColliderPanelHeightNull = 50;
        protected Vector2 _inlineColliderScroll;
        // Scroll state for the dynamic bottom region (colliders + preview controls + save)
        private Vector2 _bottomScroll;

        #endregion
        [MenuItem("FuzzPhyte/Variant Builder")]
        public static void Open() => GetWindow<FPVariantBuilderWindow>("FP Variant Builder");

        protected virtual void OnDestroy() => ScheduleCleanupPreview();

        protected virtual void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variant Source", EditorStyles.boldLabel);
            var afterHeaderRect = GUILayoutUtility.GetLastRect();
            float remainingHeight = position.height - afterHeaderRect.yMax - 12f; // padding
            remainingHeight = Mathf.Max(remainingHeight, 120f); // safety min so it never collapses

            _bottomScroll = EditorGUILayout.BeginScrollView(_bottomScroll, GUILayout.Height(remainingHeight));
            config = (FPVariantConfig)EditorGUILayout.ObjectField("Config (optional)", config, typeof(FPVariantConfig), false);
            if (config == null)
            {
                inlineBasePrefab = (GameObject)EditorGUILayout.ObjectField("Base Prefab (optional)", inlineBasePrefab, typeof(GameObject), false);
            }
            
            if (config == null && inlineBasePrefab==null)
            {
                // Inline lightweight config when no SO is provided
                EditorGUILayout.HelpBox("No ScriptableObject config linked or base prefab, we need one!", MessageType.Info);
                ScheduleCleanupPreview();
            }
            else
            {
                DrawConfigFields();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
               

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    if (!config)
                    {
                        DrawColliders();
                    }
                    EditorGUILayout.Space();
                    // collider edit work
                    if (preview)
                    {
                        EnsurePreviewColliderListUI();
                        if (_colliderListUI != null)
                        {
                            _colliderListUI.DoLayoutList();
                            if (preview)
                            {
                                EditorGUILayout.HelpBox("Select colliders in the Hierarchy under the preview to manipulate with scene handles. Use the Variant Preview inspector for quick fit helpers.", MessageType.None);
                            }
                            EditorGUILayout.Space(2);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUI.enabled = _selectedColliderIndex >= 0 && _selectedColliderIndex < preview.workingColliders.Count;
                               
                                if (GUILayout.Button("Select & Edit In Scene", GUILayout.Height(24)))
                                {
                                    SelectPreviewColliderByIndex(_selectedColliderIndex);
                                }
                                GUI.enabled = true;

                                if (GUILayout.Button("Apply Edits To Data", GUILayout.Height(24)))
                                {
                                    var srcList = config ? config.colliders : inlineCols;
                                    preview.workingColliders = new List<FPVariantColliderSpec>(srcList);

                                    // 2) Pull live edits (center/size/radius/height/dir) from the actual Collider components
                                    PullEditsFromLive(preview);

                                    // 3) Optional behavior:
                                    // If you want manual edits (handles/inspector) to disable future auto-fit, uncomment:
                                    // for (int i = 0; i < preview.workingColliders.Count; i++)
                                    //     preview.workingColliders[i].fitToVisual = false;

                                    // 4) Write back to the data (SO or inline)
                                    if (config)
                                    {
                                        Undo.RecordObject(config, "Sync Colliders From Preview");
                                        config.colliders = new List<FPVariantColliderSpec>(preview.workingColliders);
                                        EditorUtility.SetDirty(config);
                                        AssetDatabase.SaveAssets();
                                    }
                                    else
                                    {
                                        inlineCols = new List<FPVariantColliderSpec>(preview.workingColliders);
                                    }

                                    Debug.Log("Variant Builder: collider edits synced back to data."); 
                                }
                            }

                        }
                    }
                    EditorGUILayout.Space(5);
                    DrawVariantButtons();
                    EditorGUILayout.Space(5);
                    FP_Utility_Editor.DrawUILine(FP_Utility_Editor.WarningColor);
                    DrawVariantSaveInfo();
                    FP_Utility_Editor.DrawUILine(FP_Utility_Editor.WarningColor);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        #region Variant Steps
        protected void DrawVariantButtons()
        { 
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
            if (GUILayout.Button("Focus Component"))
            {
                if (preview)
                {
                    Selection.activeTransform = preview.transform;
                }
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
           
        }
        protected void DrawConfigFields()
        {
            SerializedObject so = null; 
            SerializedProperty spCols = null;
            if (config)
            {
                so = new SerializedObject(config);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("basePrefab"));
                spCols = so.FindProperty("colliders");
                EditorGUILayout.PropertyField(spCols, true);
                EditorGUILayout.PropertyField(so.FindProperty("variantName"));
                EditorGUILayout.PropertyField(so.FindProperty("saveFolder"));
                so.ApplyModifiedProperties();
            }
            else
            {
                if (inlineBasePrefab != null) 
                { 
                    inlineVariantName = inlineBasePrefab.name + "_variant";
                }
                // ---- Inline MeshSets builder ----
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Inline MeshSets Builder", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    inlineSourceRoot = (GameObject)EditorGUILayout.ObjectField(
                        new GUIContent("Source Root", "Root object whose hierarchy will be scanned for meshes"),
                        inlineSourceRoot, typeof(GameObject), true);

                    inlineClearBeforeBuild = EditorGUILayout.ToggleLeft("Clear existing MeshSets first", inlineClearBeforeBuild);
                    inlineIncludeInactive = EditorGUILayout.ToggleLeft("Include inactive children", inlineIncludeInactive);
                    inlineIncludeSkinned = EditorGUILayout.ToggleLeft("Include Skinned Meshes", inlineIncludeSkinned);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("MeshSets Builder Controls", EditorStyles.miniBoldLabel);
                    Rect r = GUILayoutUtility.GetRect(0, 0, GUI.skin.button, GUILayout.ExpandWidth(true));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string sourceLabel = "Build From Source";
                        string selectionLabel = "Build From Selection";
                        string clearLabel = "Clear Mesh Sets";
                        if (!FP_Utility_Editor.WillTextFitSingleLine(sourceLabel, r.width * 0.33f))
                        {
                            sourceLabel = "Source";
                        }
                        if(!FP_Utility_Editor.WillTextFitSingleLine(selectionLabel,r.width * 0.33f))
                        {
                            selectionLabel = "Selection";
                        }
                        if (!FP_Utility_Editor.WillTextFitSingleLine(clearLabel, r.width * 0.33f))
                        {
                            clearLabel = "Clear";
                        }
                        if (GUILayout.Button(sourceLabel, GUILayout.Height(22)))
                        {
                            BuildInlineMeshSetsFrom(inlineSourceRoot);
                        }
                        if (GUILayout.Button(selectionLabel, GUILayout.Height(22)))
                        {
                            BuildInlineMeshSetsFrom(Selection.activeGameObject);
                        }
                        if (GUILayout.Button(clearLabel, GUILayout.Height(22)))
                        {
                            inlineMeshSets.Clear();
                        }
                          
                    }

                    // tiny read-only list of current sets
                    EditorGUILayout.Space(4);
                    if (inlineMeshSets.Count == 0)
                        EditorGUILayout.HelpBox("No MeshSets yet. Pick a Source Root and click Build.", MessageType.Info);
                    else
                    {
                        for (int i = 0; i < inlineMeshSets.Count; i++)
                        {
                            var set = inlineMeshSets[i];
                            EditorGUILayout.LabelField($"[{i}] {set.NameHint}  •  {(set.UseSkinned ? "Skinned" : "Static")}  •  Mesh: {(set.Mesh ? set.Mesh.name : "None")}  •  Mats: {set.Materials?.Count ?? 0}");
                        }
                    }
                }
            }
        }
        protected void DrawColliders()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);

            
            float scrollHeight = _inlineColliderPanelHeight;
            if (inlineCols == null || inlineCols.Count == 0)
            {
                scrollHeight = _inlineColliderPanelHeightNull;
            }
            // fix scroll area
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Scrollable area just for the collider items
                
                _inlineColliderScroll = EditorGUILayout.BeginScrollView(
                    _inlineColliderScroll,
                    GUILayout.Height(scrollHeight)
                );

                if (inlineCols == null || inlineCols.Count == 0)
                {
                    EditorGUILayout.HelpBox("No inline colliders yet. Add one below.", MessageType.Info);
                   
                }
                else
                {
                    
                    for (int i = 0; i < inlineCols.Count; i++)
                    {
                        // ——— your existing per-collider UI goes here ———
                        // e.g., type, fitToVisual, localPosition/Rotation/Scale or radius/height,
                        //       “Edit This (Handles)” button, remove button, etc.
                       
                        var c = inlineCols[i];

                        EditorGUILayout.Space(4);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField($"[{i}] {c.name}  •  {c.type}", EditorStyles.boldLabel);

                            // Example (keep whatever you had here):
                            c.type = (FPColliderType)EditorGUILayout.EnumPopup("Type", c.type);
                            c.fitToVisual = EditorGUILayout.Toggle("Fit To Visual", c.fitToVisual);

                            if (c.type == FPColliderType.Box)
                            {
                                c.localPosition = EditorGUILayout.Vector3Field("Local Position", c.localPosition);
                                c.localEuler = EditorGUILayout.Vector3Field("Local Euler", c.localEuler);
                                c.localScale = EditorGUILayout.Vector3Field("Size", c.localScale);
                            }
                            else if (c.type == FPColliderType.Sphere)
                            {
                                c.localPosition = EditorGUILayout.Vector3Field("Local Position", c.localPosition);
                                c.localEuler = EditorGUILayout.Vector3Field("Local Euler", c.localEuler);
                                c.radius = EditorGUILayout.FloatField("Radius", c.radius);
                            }
                            else if (c.type == FPColliderType.Capsule)
                            {
                                c.localPosition = EditorGUILayout.Vector3Field("Local Position", c.localPosition);
                                c.localEuler = EditorGUILayout.Vector3Field("Local Euler", c.localEuler);
                                c.direction = EditorGUILayout.IntSlider("Direction (X/Y/Z)", c.direction, 0, 2);
                                c.radius = EditorGUILayout.FloatField("Radius", c.radius);
                                c.height = EditorGUILayout.FloatField("Height", c.height);
                            }
                            else if (c.type == FPColliderType.Mesh)
                            {
                                c.localPosition = EditorGUILayout.Vector3Field("Local Position", c.localPosition);
                                c.localEuler = EditorGUILayout.Vector3Field("Local Euler", c.localEuler);
                                c.meshForMeshCollider = (Mesh)EditorGUILayout.ObjectField("Mesh", c.meshForMeshCollider, typeof(Mesh), false);
                            }

                            // Optional per-row buttons you already have
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (preview && GUILayout.Button("~ Edit Collider"))
                                {
                                    // reuse your existing select routine
                                    SelectPreviewColliderByIndex(i);
                                }
                                if (GUILayout.Button("- Remove Collider"))
                                {
                                    inlineCols.RemoveAt(i);
                                    GUIUtility.ExitGUI();
                                }
                            }
                        }

                        inlineCols[i] = c;
                    }
                }

                EditorGUILayout.EndScrollView();

                // Add button (kept outside scroll so it’s always visible)
                if (GUILayout.Button("+ Add Collider"))
                {
                    inlineCols.Add(new FPVariantColliderSpec
                    {
                        name = $"Collider_{inlineCols.Count:00}",
                        type = FPColliderType.Box,
                        localScale = Vector3.one
                    });
                }
            }
            //
        }
        protected void DrawVariantSaveInfo()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope("box"))
            {
                inlineVariantName = EditorGUILayout.TextField("Variant Name", inlineVariantName);
                inlineFolder = EditorGUILayout.TextField("Save Folder", inlineFolder);

                if (GUILayout.Button("Save As Variant Prefab"))
                {
                    SaveVariant();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        protected void DoPreview()
        {
            var basePrefab = config ? config.basePrefab : inlineBasePrefab;
            if (!basePrefab)
            {
                EditorUtility.DisplayDialog("Missing Base Prefab", "Please assign a Base Prefab.", "OK");
                return;
            }

            ScheduleCleanupPreview();

            var baseInstance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            Undo.RegisterCreatedObjectUndo(baseInstance, "Create Preview");


            var root = baseInstance.GetComponent<FPInteractiveItemRoot>();
            if (root == null)
            {
                root = baseInstance.AddComponent<FPInteractiveItemRoot>();
                //set visuals root and collider root to empty objects nested under
                var visuals = new GameObject("Visuals_Root");
                visuals.transform.SetParent(root.transform, false);
                root.VisualRoot = visuals.transform;
                var colliders = new GameObject("Collider_Root");
                colliders.transform.SetParent(root.transform, false);
                root.ColliderRoot = colliders.transform;
            }


            var previewGo = new GameObject("FP_VariantPreview");
            previewGo.transform.SetParent(root.VisualRoot, false);
            preview = previewGo.AddComponent<FPVariantPreview>();
            preview.baseInstance = baseInstance;
            preview.visualsRoot = root.VisualRoot;
            preview.colliderRoot = root.ColliderRoot;


            // Visuals updated

            if (config != null && config.MeshSets != null && config.MeshSets.Count > 0)
            {
                Apply(preview.visualsRoot.gameObject, config, "Visuals", destroyExtras: true);

                // Find a representative mesh from the spawned visuals (for mesh-collider defaults, etc.)
                preview.workingMesh = FindFirstMeshInVisuals(preview.visualsRoot);

                // We don't need preview.workingMaterials when using MeshSets
                preview.workingMaterials?.Clear();
            }else if (inlineMeshSets!=null && inlineMeshSets.Count > 0)
            {
                if (preview.visualsRoot == null)
                {
                    var visuals = new GameObject("Visuals_Root").transform;
                    visuals.transform.SetParent(root.transform, false);
                    preview.visualsRoot = visuals;
                }
                Apply(preview.visualsRoot.gameObject, inlineMeshSets, "Visuals", destroyExtras: true);
                preview.workingMesh = FindFirstMeshInVisuals(preview.visualsRoot);
                preview.workingMaterials?.Clear();
            }
           

            // Colliders
            preview.workingColliders = new List<FPVariantColliderSpec>(config ? config.colliders : inlineCols);
            // Auto-fit any requested boxes now
            // AutoFitBoxes(preview);

            AutoFitCollidersFromVisuals(preview);
            if (config)
            {
                config.colliders = new List<FPVariantColliderSpec>(preview.workingColliders);
                EditorUtility.SetDirty(config);
            }
            else
            {
                inlineCols = new List<FPVariantColliderSpec>(preview.workingColliders);
            }
            preview.RebuildColliders();

            // move SceneView to spawned item
            FP_Utility_Editor.FocusOnObject(previewGo);
            Selection.activeGameObject = baseInstance;
            EditorSceneManager.MarkSceneDirty(baseInstance.scene);
        }
        protected void AutoFitBoxes(FPVariantPreview p)
        {
            if (p == null || p.visualsRoot == null || p.workingColliders == null) return;

            if (!TryGetCombinedLocalBounds(p.visualsRoot, out var localBounds)) return;
            var bounds = p.workingMesh.bounds; // local space
            for (int i = 0; i < p.workingColliders.Count; i++)
            {
                var spec = p.workingColliders[i];
                if (spec.type == FPColliderType.Box && spec.fitToVisual)
                {
                    spec.localPosition = bounds.center;
                    spec.localScale = bounds.size;
                    spec.localEuler = Vector3.zero;
                    p.workingColliders[i] = spec;
                }
            }
        }
        protected void AutoFitCollidersFromVisuals(FPVariantPreview p)
        {
            if (p == null || p.visualsRoot == null || p.workingColliders == null) return;

            // Combined local-space bounds across all renderers under visualsRoot
            if (!TryGetCombinedLocalBounds(p.visualsRoot, out var lb)) return;

            for (int i = 0; i < p.workingColliders.Count; i++)
            {
                var spec = p.workingColliders[i];
                if (!spec.fitToVisual) continue; // only auto-fit when requested

                switch (spec.type)
                {
                    case FPColliderType.Box:
                        {
                            spec.localPosition = lb.center;
                            spec.localScale = lb.size;      // we store box size here
                            spec.localEuler = Vector3.zero; // align with visuals AABB
                            break;
                        }

                    case FPColliderType.Sphere:
                        {
                            float radius = 0.5f * Mathf.Max(lb.size.x, Mathf.Max(lb.size.y, lb.size.z));
                            spec.localPosition = lb.center;
                            spec.localEuler = Vector3.zero;
                            spec.radius = Mathf.Max(0.001f, radius);
                            break;
                        }

                    case FPColliderType.Capsule:
                        {
                            // Choose the longest local axis as capsule axis, compute radius from the smaller two
                            Vector3 s = lb.size;
                            int dir = 0; float longest = s.x;
                            if (s.y >= longest) { dir = 1; longest = s.y; }
                            if (s.z >= longest) { dir = 2; longest = s.z; }

                            float radius = 0.5f * (dir == 0 ? Mathf.Min(s.y, s.z) :
                                                   dir == 1 ? Mathf.Min(s.x, s.z) :
                                                              Mathf.Min(s.x, s.y));
                            float height = Mathf.Max(longest, 2f * radius);

                            spec.localPosition = lb.center;
                            spec.localEuler = Vector3.zero;
                            spec.direction = dir;                         // 0=X,1=Y,2=Z (Unity convention)
                            spec.radius = Mathf.Max(0.001f, radius);
                            spec.height = Mathf.Max(spec.radius * 2f, height);
                            break;
                        }

                    case FPColliderType.Mesh:
                        {
                            // If user didn’t pick a mesh, default to the first visual mesh
                            if (spec.meshForMeshCollider == null)
                                spec.meshForMeshCollider = FindFirstMeshInVisuals(p.visualsRoot);
                            // spec.convex stays whatever the user chose in UI
                            break;
                        }
                }

                p.workingColliders[i] = spec;
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
        private void ScheduleCleanupPreview()
        {
            if (preview == null) return;

            // capture references to destroy AFTER this GUI cycle
            var toDestroyInstance = preview.baseInstance;
            var toDestroyPreviewGO = preview.gameObject;

            // clear our field first so other code paths see it as gone
            preview = null;

            // also clear selection to avoid editors trying to draw dead targets
            Selection.activeObject = null;

            // defer actual DestroyImmediate to a safe time
            EditorApplication.delayCall += () =>
            {
                if (toDestroyInstance) Undo.DestroyObjectImmediate(toDestroyInstance);
                if (toDestroyPreviewGO) Undo.DestroyObjectImmediate(toDestroyPreviewGO);

                Repaint();
                SceneView.RepaintAll();
            };
        }

        /// <summary>
        /// Pull live edits from the system
        /// </summary>
        /// <param name="p"></param>
        private static void PullEditsFromLive(FPVariantPreview p)
        {
            if (!p || !p.colliderRoot || p.workingColliders == null) return;

            for (int i = 0; i < p.workingColliders.Count; i++)
            {
                var spec = p.workingColliders[i];
                var t = p.colliderRoot.Find($"Col_{i}_{spec.type}");
                if (!t) continue;

                // rotation in local space
                var rot = t.localRotation;
                // helper to convert collider.center (local) into visualsRoot local space
                Vector3 CenterToLocal(Vector3 centerLocal) => t.localPosition + rot * centerLocal;

                switch (spec.type)
                {
                    case FPColliderType.Box:
                        {
                            var bc = t.GetComponent<BoxCollider>();
                            if (!bc) break;
                            spec.localPosition = CenterToLocal(bc.center);
                            spec.localEuler = t.localEulerAngles;
                            spec.localScale = bc.size;                     // we store size here
                                                                           // Optional: you can also set spec.fitToVisual = false; after manual edit
                            break;
                        }
                    case FPColliderType.Sphere:
                        {
                            var sc = t.GetComponent<SphereCollider>();
                            if (!sc) break;
                            spec.localPosition = CenterToLocal(sc.center);
                            spec.localEuler = t.localEulerAngles;
                            spec.radius = Mathf.Max(0.001f, sc.radius);
                            break;
                        }
                    case FPColliderType.Capsule:
                        {
                            var cc = t.GetComponent<CapsuleCollider>();
                            if (!cc) break;
                            spec.localPosition = CenterToLocal(cc.center);
                            spec.localEuler = t.localEulerAngles;
                            spec.radius = Mathf.Max(0.001f, cc.radius);
                            spec.height = Mathf.Max(spec.radius * 2f, cc.height);
                            spec.direction = Mathf.Clamp(cc.direction, 0, 2);
                            break;
                        }
                    case FPColliderType.Mesh:
                        // Mesh collider has no built-in edit gizmo; nothing to pull
                        break;
                }

                p.workingColliders[i] = spec;
            }

            // Rebuild so preview matches the baked data representation (center baked into transform)
            p.RebuildColliders();
        }
        private void BuildInlineMeshSetsFrom(GameObject root)
        {
            if (!root)
            {
                EditorUtility.DisplayDialog("No Source", "Assign a Source Root (or select one) before building MeshSets.", "OK");
                return;
            }

            var newSets = new List<FPMeshMaterialSet>();

            // MeshRenderer + MeshFilter
            var mrs = root.GetComponentsInChildren<MeshRenderer>(inlineIncludeInactive);
            foreach (var mr in mrs)
            {
                var tf = mr.transform;
                var mf = tf.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;

                var set = FPMeshMaterialSetHelper.CreateSetFromPiece(root.transform, tf, mf.sharedMesh, mr.sharedMaterials, false);
                newSets.Add(set);
            }

            // SkinnedMeshRenderer (optional)
            if (inlineIncludeSkinned)
            {
                var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(inlineIncludeInactive);
                foreach (var smr in smrs)
                {
                    if (!smr || !smr.sharedMesh) continue;
                    var tf = smr.transform;

                    var set = FPMeshMaterialSetHelper.CreateSetFromPiece(root.transform, tf, smr.sharedMesh, smr.sharedMaterials, true);
                    newSets.Add(set);
                }
            }

            if (newSets.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing Found",
                    "No MeshFilters/MeshRenderers (or SkinnedMeshRenderers) found under the source root.",
                    "OK");
                return;
            }

            if (inlineClearBeforeBuild) inlineMeshSets.Clear();
            inlineMeshSets.AddRange(newSets);

            Debug.Log($"Inline MeshSets: added {newSets.Count} set(s) from '{root.name}'.");
            Repaint();
        }
        #endregion
        private void SelectPreviewColliderByIndex(int index)
        {
            if (preview == null || preview.workingColliders == null) return;
            if (index < 0 || index >= preview.workingColliders.Count) return;

            var spec = preview.workingColliders[index];

            // Ensure the collider children exist
            preview.RebuildColliders();

            var t = preview.colliderRoot.Find($"Col_{index}_{spec.type}");
            if (t)
            {
                Selection.activeTransform = t;
                FP_Utility_Editor.FocusOnObject(t.gameObject);
            }
        }

        private void EnsurePreviewColliderListUI()
        {
            if (preview == null || preview.workingColliders == null)
            {
                _colliderListUI = null;
                _selectedColliderIndex = -1;
                return;
            }

            var src = preview.workingColliders;

            if (_colliderListUI == null || _colliderListUI.list != (System.Collections.IList)src)
            {
                _colliderListUI = new ReorderableList(src, typeof(FPVariantColliderSpec), false, true, false, false);

                _colliderListUI.drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Preview Colliders");
                };

                _colliderListUI.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
                _colliderListUI.drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index < 0 || index >= src.Count) return;
                    var s = src[index];
                    rect.y += 3f;
                    var label = $"[{index}] {s.name} ({s.type})";
                    EditorGUI.LabelField(rect, label);
                    //double click row to select/focus
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && Event.current.clickCount == 2)
                    {
                        _selectedColliderIndex = index;
                        GUI.changed = true;
                        EditorApplication.delayCall += () => SelectPreviewColliderByIndex(index);
                        Event.current.Use();
                    }
                };

                _colliderListUI.onSelectCallback = list =>
                {
                    _selectedColliderIndex = list.index;
                };
            }
        }

        #region Variant Visual Applier

        private static Mesh FindFirstMeshInVisuals(Transform root)
        {
            if (!root) return null;
            var mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf && mf.sharedMesh) return mf.sharedMesh;
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr && smr.sharedMesh) return smr.sharedMesh;
            return null;
        }
        public static void Apply(GameObject target, FPVariantConfig cfg, string containerName = "Visuals", bool destroyExtras = true)
        {
            if (target == null || cfg == null) return;
            var sets = cfg.MeshSets;
            if (sets == null || sets.Count == 0) return;

            // Create/find a container
            Transform parent = target.transform;
            if (!string.IsNullOrEmpty(containerName))
            {
                var found = parent.Find(containerName);
                if (found == null)
                {
                    var go = new GameObject(containerName);
                    go.transform.SetParent(parent, false);
                    found = go.transform;
                }
                parent = found;
            }
            //CODE REPEATS
            // Ensure child count == sets.Count
            GenerateMeshObjects(parent, sets, destroyExtras);
        }
        /// <summary>
        /// Inline Mesh Mode
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sets"></param>
        /// <param name="containerName"></param>
        /// <param name="destroyExtras"></param>
        public static void Apply(GameObject target, List<FPMeshMaterialSet> sets, string containerName = "Visuals", bool destroyExtras = true)
        {
            if (target == null || sets == null || sets.Count == 0) return;

            // Create/find a container
            Transform parent = target.transform;
            if (!string.IsNullOrEmpty(containerName))
            {
                var found = parent.Find(containerName);
                if (found == null)
                {
                    var go = new GameObject(containerName);
                    go.transform.SetParent(parent, false);
                    found = go.transform;
                }
                parent = found;
            }

            // Ensure child count == sets.Count
            GenerateMeshObjects(parent, sets, destroyExtras);
        }
        
        /// <summary>
        /// Generates the children Mesh Objects needed based on the objects
        /// </summary>
        private static void GenerateMeshObjects(Transform parent, List<FPMeshMaterialSet> sets, bool destroyExtras = true)
        {
            var children = GetDirectChildren(parent);
            while (children.Count < sets.Count)
            {
                var go = new GameObject($"MeshPart_{children.Count:00}");
                go.transform.SetParent(parent, false);
                children.Add(go.transform);
            }
            if (destroyExtras)
            {
                for (int i = children.Count - 1; i >= sets.Count; i--)
                {
                    UnityEngine.Object.DestroyImmediate(children[i].gameObject);
                    children.RemoveAt(i);
                }
            }

            // Apply each piece
            for (int i = 0; i < sets.Count; i++)
            {
                var set = sets[i];
                var child = children[i];

                child.name = string.IsNullOrWhiteSpace(set.NameHint) ? $"MeshPart_{i:00}" : set.NameHint;

                if (set.LocalScaleOverride != Vector3.zero) child.localScale = set.LocalScaleOverride;
                if (set.LocalPositionOffset != Vector3.zero) child.localPosition = set.LocalPositionOffset;
                if (set.LocalEulerOffset != Vector3.zero) child.localRotation = Quaternion.Euler(set.LocalEulerOffset);

                if (set.UseSkinned)
                {
                    SkinnedMeshRenderer smr = null;
                    if (child.GetComponent<SkinnedMeshRenderer>())
                    {
                        smr = child.GetComponent<SkinnedMeshRenderer>();
                    }
                    else
                    {
                        smr = child.gameObject.AddComponent<SkinnedMeshRenderer>();
                    }
                    smr.sharedMesh = set.Mesh;
                    smr.sharedMaterials = set.GetPaddedMaterials();

                    // If mesh is null, disable to avoid warnings
                    smr.enabled = set.Mesh != null;
                }
                else
                {
                    MeshFilter mf = null;
                    MeshRenderer mr = null;
                    if (child.GetComponent<MeshFilter>())
                    {
                        mf = child.GetComponent<MeshFilter>();
                    }
                    else
                    {
                        mf = child.gameObject.AddComponent<MeshFilter>();
                    }
                    if (child.GetComponent<MeshRenderer>())
                    {
                        mr = child.gameObject.AddComponent<MeshRenderer>();
                    }
                    else
                    {
                        mr = child.gameObject.AddComponent<MeshRenderer>();
                    }
                    mf.sharedMesh = set.Mesh;

                    mr.sharedMaterials = set.GetPaddedMaterials();
                    mr.enabled = set.Mesh != null;
                }
            }
        }
        private static List<Transform> GetDirectChildren(Transform parent)
        {
            var list = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
                list.Add(parent.GetChild(i));
            return list;
        }
        private static bool TryGetCombinedLocalBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return false;

            var wb = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);

            WorldToLocalAABB(root, wb, out var center, out var size);
            localBounds = new Bounds(center, size);
            return true;
        }
        private static void WorldToLocalAABB(Transform t, Bounds worldBounds, out Vector3 localCenter, out Vector3 localSize)
        {
            Vector3 c = worldBounds.center, e = worldBounds.extents;
            var corners = new[]
            {
                new Vector3(c.x - e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z + e.z),
            };
            var lb = new Bounds(t.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                lb.Encapsulate(t.InverseTransformPoint(corners[i]));
            localCenter = lb.center; localSize = lb.size;
        }
        #endregion

    }
}