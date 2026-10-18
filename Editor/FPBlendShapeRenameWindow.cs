// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPBlendShapeRenameWindow : EditorWindow
    {
        [Serializable]
        private sealed class RenameRow
        {
            public long id;
            public string owner;
            public string original;
            public bool selected;
            public string replacement;
        }

        private enum MaterialOverrideMode { AllSlots, IndividualSlots }

        [Serializable]
        private sealed class MaterialSlot
        {
            public string key;
            public string owner;
            public int rendererIndex;
            public int slotIndex;
            public Material original;
            public Material replacement;
        }

        [SerializeField] private GameObject sourceModel;
        [SerializeField] private List<RenameRow> rows = new List<RenameRow>();
        [SerializeField] private Material previewMaterial;
        [SerializeField] private MaterialOverrideMode materialOverrideMode;
        [SerializeField] private List<MaterialSlot> materialSlots = new List<MaterialSlot>();
        [SerializeField] private bool showMaterialSlots = true;
        [SerializeField] private bool showBlendshapes = true;
        [SerializeField] private float blendshapeListHeight = 210f;
        [SerializeField] private FPMeshPreviewProjection projection;
        [SerializeField] private Quaternion orbit = Quaternion.Euler(22, -35, 0);
        [SerializeField] private float zoom = 1.35f;
        [SerializeField] private bool invertOrbit;
        private Vector2 scroll;
        private Vector2 parameterScroll;
        private Vector2 materialScroll;
        private PreviewRenderUtility preview;
        private GameObject previewModel;
        private Material fallbackMaterial;
        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private Bounds bounds;
        private byte[] loadedBytes;
        private string status = "Choose an FBX to list its blendshapes.";
        private MessageType statusType = MessageType.Info;
        private int activeOrbitAxis = -1;

        [MenuItem("FuzzPhyte/Utility/Mesh/BlendShape Rename", priority = FP_UtilityData.MENU_UTILITY_MESH + 6)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPBlendShapeRenameWindow>("BlendShape Rename");
            window.minSize = new Vector2(850, 540);
            if (window.sourceModel == null) window.UseSelection();
        }

        private void OnEnable()
        {
            minSize = new Vector2(850, 540);
            Undo.undoRedoPerformed += OnUndoRedo;
            if (sourceModel != null) ReloadSource(true);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnUndoRedo() => ApplyPreviewMaterials();

        private void CleanupPreview()
        {
            preview?.Cleanup();
            preview = null;
            previewModel = null;
            renderers = null;
            originalMaterials = null;
            if (fallbackMaterial != null) DestroyImmediate(fallbackMaterial);
            fallbackMaterial = null;
        }

        private void OnGUI()
        {
            GUILayout.Label("BlendShape Rename", EditorStyles.boldLabel);
            Rect workspace = new Rect(4, 26, position.width - 8, position.height - 138);
            Rect left = new Rect(workspace.x, workspace.y, 352, workspace.height);
            Rect right = new Rect(left.xMax + 6, workspace.y, workspace.width - left.width - 6, workspace.height);
            GUI.Box(left, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(left.x + 8, left.y + 8, left.width - 16, left.height - 16));
            DrawParameters();
            GUILayout.EndArea();
            GUI.Box(right, GUIContent.none, EditorStyles.helpBox);
            DrawPreview(new Rect(right.x + 6, right.y + 6, right.width - 12, right.height - 12));
            GUILayout.BeginArea(new Rect(4, position.height - 106, position.width - 8, 102), EditorStyles.helpBox);
            GUILayout.Label("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, statusType);
            GUILayout.EndArea();
        }

        private void DrawParameters()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var model = (GameObject)EditorGUILayout.ObjectField("Original FBX", sourceModel, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) LoadSource(model);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selection")) UseSelection();
            using (new EditorGUI.DisabledScope(sourceModel == null))
                if (GUILayout.Button("Reload FBX")) ReloadSource(false);
            EditorGUILayout.EndHorizontal();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            parameterScroll = EditorGUILayout.BeginScrollView(parameterScroll);
            DrawBlendshapeList();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            if (showBlendshapes) HandleBlendshapeListResize(GUILayoutUtility.GetLastRect());
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            DrawMaterialOverrides();
            projection = FPMeshPreviewEditorUtility.DrawProjectionPopup(projection);
            invertOrbit = FPMeshPreviewEditorUtility.DrawInvertCameraOrbitToggle(invertOrbit);
            if (GUILayout.Button("Frame Whole Model")) { zoom = 1.35f; orbit = Quaternion.Euler(22, -35, 0); Repaint(); }
            EditorGUILayout.EndScrollView();
            DrawSaveButton();
        }

        private void DrawBlendshapeList()
        {
            showBlendshapes = EditorGUILayout.Foldout(showBlendshapes, $"Blendshapes ({rows.Count})", true, EditorStyles.foldoutHeader);
            if (!showBlendshapes) return;
            EditorGUILayout.LabelField("Select each name to change.", EditorStyles.miniLabel);
            float height = Mathf.Clamp(blendshapeListHeight, 72f, Mathf.Max(72f, position.height - 240f));
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            foreach (RenameRow row in rows)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                bool selected = GUILayout.Toggle(row.selected, GUIContent.none, EditorStyles.radioButton, GUILayout.Width(18));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Select Blendshape Rename");
                    row.selected = selected;
                }
                EditorGUILayout.SelectableLabel(row.original, GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();
                using (new EditorGUI.DisabledScope(!row.selected))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("New name", GUILayout.Width(62));
                    EditorGUI.BeginChangeCheck();
                    string replacement = EditorGUILayout.TextField(row.replacement);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Edit Blendshape Name");
                        row.replacement = replacement;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.LabelField(row.owner, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void HandleBlendshapeListResize(Rect rect)
        {
            int control = GUIUtility.GetControlID(FocusType.Passive, rect);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical, control);
            GUI.Label(rect, new GUIContent("", "Drag to resize the blendshape list. Collapse it using the Blendshapes header."));
            Event current = Event.current;
            switch (current.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && rect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = control;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == control)
                    {
                        blendshapeListHeight = Mathf.Clamp(blendshapeListHeight + current.delta.y, 72f, Mathf.Max(72f, position.height - 240f));
                        current.Use();
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == control)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        private void DrawSaveButton()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Saves a separate FBX. Source stays unchanged.", EditorStyles.wordWrappedMiniLabel);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = FP_Utility_Editor.OkayColor;
            using (new EditorGUI.DisabledScope(loadedBytes == null || !rows.Any(r => r.selected && r.replacement != r.original)))
            {
                if (GUILayout.Button("Save Renamed FBX Copy…", GUILayout.Height(30)))
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
                    string directory = sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ? Path.GetDirectoryName(sourcePath) : "Assets";
                    string destination = EditorUtility.SaveFilePanelInProject("Save Renamed FBX Copy",
                        Path.GetFileNameWithoutExtension(sourcePath) + "_Renamed", "fbx", "Choose a new FBX file.", directory);
                    if (!string.IsNullOrEmpty(destination))
                    {
                        try { SaveCopy(destination); }
                        catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); }
                        GUIUtility.ExitGUI();
                    }
                }
            }
            GUI.backgroundColor = old;
        }

        private void DrawMaterialOverrides()
        {
            EditorGUI.BeginChangeCheck();
            var mode = (MaterialOverrideMode)EditorGUILayout.EnumPopup("Material overrides", materialOverrideMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Change Preview Material Mode");
                materialOverrideMode = mode;
                ApplyPreviewMaterials();
            }
            using (new EditorGUI.DisabledScope(materialOverrideMode != MaterialOverrideMode.AllSlots))
            {
                EditorGUI.BeginChangeCheck();
                var material = (Material)EditorGUILayout.ObjectField("All slots", previewMaterial, typeof(Material), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Override All Preview Materials");
                    previewMaterial = material;
                    ApplyPreviewMaterials();
                }
            }
            showMaterialSlots = EditorGUILayout.Foldout(showMaterialSlots, $"FBX material slots ({materialSlots.Count})", true);
            if (showMaterialSlots)
            {
                materialScroll = EditorGUILayout.BeginScrollView(materialScroll, GUILayout.Height(140));
                foreach (MaterialSlot slot in materialSlots)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(new GUIContent(slot.owner + " · Slot " + slot.slotIndex, slot.owner), EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Original", slot.original, typeof(Material), false);
                    using (new EditorGUI.DisabledScope(materialOverrideMode != MaterialOverrideMode.IndividualSlots))
                    {
                        EditorGUI.BeginChangeCheck();
                        var replacement = (Material)EditorGUILayout.ObjectField("Override", slot.replacement, typeof(Material), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(this, "Override Preview Material Slot");
                            slot.replacement = replacement;
                            ApplyPreviewMaterials();
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.LabelField("Preview only. Empty overrides use original materials.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(previewMaterial == null && !materialSlots.Any(s => s.replacement != null)))
            {
                if (GUILayout.Button("Reset Material Overrides"))
                {
                    Undo.RecordObject(this, "Reset Preview Material Overrides");
                    previewMaterial = null;
                    foreach (MaterialSlot slot in materialSlots) slot.replacement = null;
                    ApplyPreviewMaterials();
                }
            }
        }

        private void UseSelection()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
                LoadSource(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        internal void LoadSource(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (model == null || !string.Equals(path, AssetDatabase.GetAssetPath(sourceModel), StringComparison.Ordinal))
                materialSlots.Clear();
            sourceModel = string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : model;
            ReloadSource(false);
        }

        private void ReloadSource(bool preserveRows)
        {
            CleanupPreview();
            loadedBytes = null;
            var previous = preserveRows ? rows.ToDictionary(r => r.id) : new Dictionary<long, RenameRow>();
            rows.Clear();
            if (sourceModel == null) { SetStatus("Choose an FBX to list its blendshapes.", MessageType.Info); return; }
            try
            {
                string path = AssetDatabase.GetAssetPath(sourceModel);
                if (!string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Original FBX must be an imported .fbx model asset.");
                byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
                var document = new FPBlendShapeFbxDocument(bytes);
                foreach (var channel in document.Channels)
                {
                    var entry = previous.TryGetValue(channel.Id, out RenameRow row) && row.original == channel.Name ? row :
                        new RenameRow { id = channel.Id, original = channel.Name, replacement = channel.Name };
                    entry.owner = document.GetOwnerLabel(channel);
                    rows.Add(entry);
                }
                loadedBytes = bytes;
                BuildPreview();
                SetStatus(rows.Count == 0 ? "This FBX contains no blendshape channels." :
                    $"Found {rows.Count} blendshapes. External clips and scripts that reference old names must be updated separately.",
                    rows.Count == 0 ? MessageType.Warning : MessageType.Info);
            }
            catch (Exception exception)
            {
                loadedBytes = null;
                CleanupPreview();
                SetStatus(exception.Message, MessageType.Error);
            }
        }

        internal string SaveCopy(string destination)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
            if (loadedBytes == null || !loadedBytes.SequenceEqual(File.ReadAllBytes(Path.GetFullPath(sourcePath))))
                throw new InvalidOperationException("The source FBX changed. Reload it before saving.");
            string fullDestination = Path.GetFullPath(destination);
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!fullDestination.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(destination), ".fbx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the new .fbx inside this project's Assets folder.");
            destination = "Assets/" + fullDestination.Substring(assetsRoot.Length).Replace('\\', '/');
            if (File.Exists(fullDestination) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination, AssetPathToGUIDOptions.OnlyExistingAssets)))
                throw new IOException("Choose a new filename. Existing assets cannot be overwritten.");
            var names = rows.Where(r => r.selected).ToDictionary(r => r.id, r => r.replacement);
            if (!rows.Any(r => r.selected && r.original != r.replacement)) throw new ArgumentException("Select at least one changed name.");
            byte[] renamed = new FPBlendShapeFbxDocument(loadedBytes).Rename(names);
            bool copied = false;
            try
            {
                // CopyAsset preserves the model's importer settings and lets Unity create the new GUID.
                if (!AssetDatabase.CopyAsset(sourcePath, destination)) throw new IOException("Unity could not copy the source FBX.");
                copied = true;
                File.WriteAllBytes(fullDestination, renamed);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject output = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
                if (output == null) throw new InvalidDataException("Unity could not import the renamed FBX copy.");
                if (AssetImporter.GetAtPath(destination) is ModelImporter importer && importer.importBlendShapes)
                {
                    var importedNames = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Mesh>()
                        .SelectMany(m => Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName)));
                    foreach (string name in names.Values)
                        if (!importedNames.Contains(name)) throw new InvalidDataException("Unity did not import the expected blendshape name: " + name);
                }
                EditorGUIUtility.PingObject(output);
                Selection.activeObject = output;
                SetStatus("Saved " + destination + ". Original FBX unchanged.", MessageType.Info);
                return destination;
            }
            catch
            {
                if (copied) AssetDatabase.DeleteAsset(destination);
                throw;
            }
        }

        private void BuildPreview()
        {
            preview = new PreviewRenderUtility();
            previewModel = preview.InstantiatePrefabInScene(sourceModel);
            previewModel.name = sourceModel.name + " Preview";
            foreach (Transform child in previewModel.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (Animator animator in previewModel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Camera camera in previewModel.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (Light light in previewModel.GetComponentsInChildren<Light>(true)) light.enabled = false;
            preview.AddSingleGO(previewModel);
            renderers = previewModel.GetComponentsInChildren<Renderer>(true);
            originalMaterials = renderers.Select(r => r.sharedMaterials).ToArray();
            RefreshMaterialSlots();
            bounds = new Bounds(previewModel.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            ApplyPreviewMaterials();
        }

        private void RefreshMaterialSlots()
        {
            var previous = materialSlots.ToDictionary(s => s.key, s => s.replacement);
            materialSlots.Clear();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, previewModel.transform);
                string indices = "";
                for (Transform t = renderer.transform; t != previewModel.transform; t = t.parent)
                    indices = t.GetSiblingIndex() + "/" + indices;
                int componentIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
                string rendererKey = path + "|" + indices + renderer.GetType().Name + ":" + componentIndex;
                int count = Mathf.Max(1, originalMaterials[i].Length);
                for (int slot = 0; slot < count; slot++)
                {
                    string key = rendererKey + "/" + slot;
                    materialSlots.Add(new MaterialSlot { key = key, owner = string.IsNullOrEmpty(path) ? sourceModel.name : path,
                        rendererIndex = i, slotIndex = slot, original = slot < originalMaterials[i].Length ? originalMaterials[i][slot] : null,
                        replacement = previous.TryGetValue(key, out Material material) ? material : null });
                }
            }
        }

        private void ApplyPreviewMaterials()
        {
            if (renderers == null) return;
            if (fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null) fallbackMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.65f, 0.72f, 0.8f) };
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = originalMaterials[i];
                int count = Mathf.Max(1, materials.Length);
                var applied = new Material[count];
                for (int j = 0; j < count; j++)
                {
                    Material replacement = materialOverrideMode == MaterialOverrideMode.AllSlots ? previewMaterial :
                        materialSlots.FirstOrDefault(s => s.rendererIndex == i && s.slotIndex == j)?.replacement;
                    applied[j] = replacement != null ? replacement : j < materials.Length && materials[j] != null ? materials[j] : fallbackMaterial;
                }
                renderers[i].sharedMaterials = applied;
            }
            Repaint();
        }

        private void DrawPreview(Rect rect)
        {
            if (preview == null)
            {
                GUI.Label(rect, "Assign an FBX to preview the whole model.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            Event current = Event.current;
            if (current.type == EventType.MouseUp) activeOrbitAxis = -1;
            if (rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.ScrollWheel)
                { zoom = FPMeshPreviewEditorUtility.ApplyScrollZoom(zoom, current.delta.y); current.Use(); Repaint(); }
                else if (current.type == EventType.MouseDown && current.button == 0)
                {
                    activeOrbitAxis = FPMeshPreviewEditorUtility.GetOrbitAxisAtPosition(rect, current.mousePosition);
                    if (FPMeshPreviewEditorUtility.TryGetSceneOrientationClick(rect, preview.camera, current.mousePosition, out Quaternion snapped))
                    { orbit = snapped; current.Use(); Repaint(); }
                }
                else if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 1))
                {
                    if (activeOrbitAxis >= 0) orbit = FPMeshPreviewEditorUtility.ApplyOrbitAxisDrag(orbit, activeOrbitAxis, current.delta);
                    else if (!FPMeshPreviewEditorUtility.IsOrbitGizmoPosition(rect, current.mousePosition))
                        orbit = FPMeshPreviewEditorUtility.ApplyUnityStyleOrbit(orbit, current.delta, invertOrbit);
                    current.Use(); Repaint();
                }
            }
            if (current.type == EventType.Repaint)
            {
                preview.BeginPreview(rect, GUIStyle.none);
                Camera camera = preview.camera;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                camera.clearFlags = CameraClearFlags.Color;
                camera.fieldOfView = FPMeshPreviewEditorUtility.DefaultFieldOfView;
                float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
                float distance = FPMeshPreviewEditorUtility.CalculateFitDistance(bounds, rect) * zoom;
                camera.transform.SetPositionAndRotation(bounds.center - orbit * Vector3.forward * distance, orbit);
                camera.orthographic = projection == FPMeshPreviewProjection.Orthographic;
                camera.orthographicSize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(bounds, rect, orbit) * zoom;
                camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2.4f);
                camera.farClipPlane = distance + radius * 3.4f;
                preview.lights[0].intensity = 1.1f;
                preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(20, -20, 0);
                preview.lights[1].intensity = 0.55f;
                preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(-15, 30, 0);
                preview.Render(true);
                GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
                FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, camera, projection);
            }
            FPMeshPreviewEditorUtility.DrawOrbitGizmo(rect, (direction, up) => { orbit = Quaternion.LookRotation(direction, up); Repaint(); });
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
            Repaint();
        }
    }
}
