// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    internal static class FPAnimationBindingRenameUtility
    {
        internal const string BlendShapePrefix = "blendShape.";

        internal static EditorCurveBinding[] GetBindings(AnimationClip clip) => clip == null ? Array.Empty<EditorCurveBinding>() :
            AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip))
                .OrderBy(b => b.path, StringComparer.Ordinal).ThenBy(b => b.type?.FullName, StringComparer.Ordinal)
                .ThenBy(b => b.propertyName, StringComparer.Ordinal).ToArray();

        internal static bool IsBlendShape(EditorCurveBinding binding) => binding.type == typeof(SkinnedMeshRenderer) &&
            binding.propertyName.StartsWith(BlendShapePrefix, StringComparison.Ordinal);

        private static bool SameProperty(EditorCurveBinding a, EditorCurveBinding b) =>
            a.path == b.path && a.type == b.type && a.propertyName == b.propertyName;

        internal static string Validate(AnimationClip clip, EditorCurveBinding binding, string newProperty)
        {
            if (clip == null) return "Assign an AnimationClip.";
            if (binding.type == null) return "Select a property with an available component type.";
            if (string.IsNullOrWhiteSpace(newProperty) || newProperty != newProperty.Trim() || newProperty.Any(char.IsControl))
                return "Enter a nonempty name without surrounding spaces or control characters.";
            if (IsBlendShape(binding) && (!newProperty.StartsWith(BlendShapePrefix, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(newProperty.Substring(BlendShapePrefix.Length))))
                return "A blendshape binding needs a name after blendShape.";
            if (newProperty == binding.propertyName) return "Enter a different name.";
            EditorCurveBinding[] current = GetBindings(clip);
            if (!current.Any(b => SameProperty(b, binding) && b.isPPtrCurve == binding.isPPtrCurve && b.isDiscreteCurve == binding.isDiscreteCurve))
                return "The selected property changed or was removed. Refresh the list.";
            var destination = binding;
            destination.propertyName = newProperty;
            if (current.Any(b => SameProperty(b, destination))) return "That property already exists at this path. Its curve will not be overwritten.";
            return null;
        }

        internal static void Rename(AnimationClip clip, EditorCurveBinding binding, string newProperty, bool recordUndo = true)
        {
            string error = Validate(clip, binding, newProperty);
            if (error != null) throw new ArgumentException(error);
            var destination = binding;
            destination.propertyName = newProperty;
            // Copy the binding struct rather than recreating it: retain discrete/object-reference flags.
            if (recordUndo)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Rename Animation Binding");
                Undo.RegisterCompleteObjectUndo(clip, "Rename Animation Binding");
            }
            if (binding.isPPtrCurve)
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurves(clip, new[] { binding, destination }, new[] { null, keys });
            }
            else
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurves(clip, new[] { binding, destination }, new[] { null, curve });
            }
            EditorUtility.SetDirty(clip);
        }

        internal static bool CanEditInPlace(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            return clip != null && AssetDatabase.IsMainAsset(clip) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                string.Equals(Path.GetExtension(path), ".anim", StringComparison.OrdinalIgnoreCase) &&
                (clip.hideFlags & HideFlags.NotEditable) == 0 && AssetDatabase.IsOpenForEdit(path) &&
                File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) == 0;
        }

        internal static AnimationClip SaveCopy(AnimationClip source, EditorCurveBinding binding, string newProperty, string destination)
        {
            string error = Validate(source, binding, newProperty);
            if (error != null) throw new ArgumentException(error);
            string fullPath = Path.GetFullPath(destination);
            string root = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(fullPath), ".anim", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the .anim copy inside this project's Assets folder.");
            destination = "Assets/" + fullPath.Substring(root.Length).Replace('\\', '/');
            if (File.Exists(fullPath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination, AssetPathToGUIDOptions.OnlyExistingAssets)))
                throw new IOException("Choose a new filename. Existing clips cannot be overwritten.");
            AnimationClip copy = UnityEngine.Object.Instantiate(source);
            bool created = false;
            try
            {
                copy.hideFlags = HideFlags.None;
                copy.name = Path.GetFileNameWithoutExtension(destination);
                Rename(copy, binding, newProperty, false);
                AssetDatabase.CreateAsset(copy, destination);
                created = true;
                AssetDatabase.SaveAssetIfDirty(copy);
                return copy;
            }
            catch
            {
                if (created) AssetDatabase.DeleteAsset(destination);
                else UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
        }
    }

    public sealed class FPAnimationBindingRenameWindow : EditorWindow
    {
        [SerializeField] private AnimationClip sourceClip;
        [SerializeField] private string search = "";
        [SerializeField] private string selectedPath;
        [SerializeField] private string selectedProperty;
        [SerializeField] private string selectedType;
        [SerializeField] private bool selectedObjectReference;
        [SerializeField] private string newName;
        private EditorCurveBinding[] bindings = Array.Empty<EditorCurveBinding>();
        private int selectedIndex = -1;
        private Vector2 scroll;
        private string status = "Choose a clip, select one animated property, then enter its corrected name.";
        private MessageType statusType = MessageType.Info;

        [MenuItem("FuzzPhyte/Utility/Animation/Binding Rename", priority = FP_UtilityData.MENU_UTILITY_ANIMATION)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPAnimationBindingRenameWindow>("Animation Binding Rename");
            window.minSize = new Vector2(640, 480);
            if (window.sourceClip == null) window.UseSelection();
        }

        private void OnEnable()
        {
            minSize = new Vector2(640, 480);
            Undo.undoRedoPerformed += RefreshBindings;
            RefreshBindings();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= RefreshBindings;
        private void OnFocus() => RefreshBindings();

        internal void LoadClip(AnimationClip clip)
        {
            sourceClip = clip;
            selectedProperty = null;
            newName = "";
            search = "";
            RefreshBindings();
            status = "Select one property to rename. Keyframes and renderer paths are retained.";
            statusType = MessageType.Info;
        }

        private void RefreshBindings()
        {
            bindings = FPAnimationBindingRenameUtility.GetBindings(sourceClip);
            selectedIndex = Array.FindIndex(bindings, b => b.path == selectedPath && b.propertyName == selectedProperty &&
                b.type?.AssemblyQualifiedName == selectedType && b.isPPtrCurve == selectedObjectReference);
            Repaint();
        }

        private void UseSelection()
        {
            if (Selection.activeObject is AnimationClip clip) LoadClip(clip);
            else { status = "Select an AnimationClip asset or assign one in the Clip field."; statusType = MessageType.Info; }
        }

        private void SelectBinding(int index, bool resetName)
        {
            selectedIndex = index;
            EditorCurveBinding binding = bindings[index];
            selectedPath = binding.path;
            selectedProperty = binding.propertyName;
            selectedType = binding.type?.AssemblyQualifiedName;
            selectedObjectReference = binding.isPPtrCurve;
            if (resetName) newName = FPAnimationBindingRenameUtility.IsBlendShape(binding)
                ? binding.propertyName.Substring(FPAnimationBindingRenameUtility.BlendShapePrefix.Length) : binding.propertyName;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Animation Binding Rename", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var clip = (AnimationClip)EditorGUILayout.ObjectField("Clip", sourceClip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) LoadClip(clip);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selection", GUILayout.Width(110))) UseSelection();
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) RefreshBindings();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{bindings.Length} animated properties", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            search = EditorGUILayout.TextField("Filter", search);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            scroll = EditorGUILayout.BeginScrollView(scroll, EditorStyles.helpBox);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                string label = binding.propertyName;
                string path = string.IsNullOrEmpty(binding.path) ? "(root)" : binding.path;
                string type = binding.type?.Name ?? "Missing component type";
                if (!string.IsNullOrEmpty(search) && (label + " " + path + " " + type).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selectedIndex == i, GUIContent.none, EditorStyles.radioButton, GUILayout.Width(18)) && selectedIndex != i)
                    SelectBinding(i, true);
                if (GUILayout.Button(new GUIContent(label, label), EditorStyles.label)) SelectBinding(i, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(path + "  ·  " + type + (binding.isPPtrCurve ? "  ·  Object reference" : ""), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawRenameControls();
            EditorGUILayout.HelpBox(status, statusType);
        }

        private void DrawRenameControls()
        {
            if (selectedIndex < 0 || selectedIndex >= bindings.Length)
            {
                EditorGUILayout.HelpBox("Select a property from the list. The clip name and binding path are not changed.", MessageType.Info);
                return;
            }
            EditorCurveBinding binding = bindings[selectedIndex];
            bool blendshape = FPAnimationBindingRenameUtility.IsBlendShape(binding);
            EditorGUILayout.LabelField("Selected", binding.propertyName);
            newName = EditorGUILayout.TextField(blendshape ? "New blendshape name" : "New property name", newName);
            string property = blendshape ? FPAnimationBindingRenameUtility.BlendShapePrefix + newName : newName;
            string error = FPAnimationBindingRenameUtility.Validate(sourceClip, binding, property);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Info);
            bool editable = FPAnimationBindingRenameUtility.CanEditInPlace(sourceClip);
            if (!editable) EditorGUILayout.HelpBox("Imported, packaged or read-only clips require a separate .anim copy.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(error != null || !editable))
                if (GUILayout.Button("Rename in Original (Undo)", GUILayout.Height(28))) ApplyRename(binding, property);
            using (new EditorGUI.DisabledScope(error != null))
                if (GUILayout.Button("Save Renamed Copy…", GUILayout.Height(28))) SaveRenamedCopy(binding, property);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyRename(EditorCurveBinding binding, string property)
        {
            try
            {
                if (!FPAnimationBindingRenameUtility.CanEditInPlace(sourceClip)) throw new InvalidOperationException("This clip cannot be edited in place. Save a copy instead.");
                FPAnimationBindingRenameUtility.Rename(sourceClip, binding, property);
                AssetDatabase.SaveAssetIfDirty(sourceClip);
                selectedProperty = property;
                RefreshBindings();
                status = "Renamed and saved " + property + ". Undo is available.";
                statusType = MessageType.Info;
                EditorGUIUtility.PingObject(sourceClip);
            }
            catch (Exception exception) { status = exception.Message; statusType = MessageType.Error; }
        }

        private void SaveRenamedCopy(EditorCurveBinding binding, string property)
        {
            string path = AssetDatabase.GetAssetPath(sourceClip);
            string directory = path.StartsWith("Assets/", StringComparison.Ordinal) ? Path.GetDirectoryName(path) : "Assets";
            string destination = EditorUtility.SaveFilePanelInProject("Save Renamed Animation Copy", sourceClip.name + "_Renamed", "anim", "Choose a new animation asset.", directory);
            if (string.IsNullOrEmpty(destination)) return;
            try
            {
                AnimationClip copy = FPAnimationBindingRenameUtility.SaveCopy(sourceClip, binding, property, destination);
                status = "Saved " + destination + ". Source clip unchanged.";
                statusType = MessageType.Info;
                Selection.activeObject = copy;
                EditorGUIUtility.PingObject(copy);
            }
            catch (Exception exception) { status = exception.Message; statusType = MessageType.Error; }
            GUIUtility.ExitGUI();
        }
    }
}
