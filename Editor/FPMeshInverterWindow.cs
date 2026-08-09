// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Creates and saves an inside-out copy of an existing mesh.
    /// </summary>
    public class FPMeshInverterWindow : EditorWindow
    {
        [SerializeField]
        private UnityEngine.Object sourceObject;
        [SerializeField]
        private string outputMeshName = "Mesh_Inverted";

        [MenuItem("FuzzPhyte/Utility/Mesh/Invert Mesh", priority = FP_UtilityData.MENU_UTILITY_MESH + 5)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshInverterWindow>("Invert Mesh");
            window.minSize = new Vector2(390f, 205f);
            window.SyncSelectedSource();
        }

        [MenuItem("Assets/FuzzPhyte/Invert Mesh and Save...", false, 2200)]
        private static void OpenFromAssetSelection()
        {
            ShowWindow();
        }

        [MenuItem("Assets/FuzzPhyte/Invert Mesh and Save...", true)]
        private static bool ValidateOpenFromAssetSelection()
        {
            return TryResolveSourceMesh(Selection.activeObject, out _);
        }

        private void OnEnable()
        {
            if (sourceObject == null)
            {
                SyncSelectedSource();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Invert Mesh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates an inside-out copy by reversing surface winding and flipping normals. " +
                "The source mesh is not modified.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            sourceObject = EditorGUILayout.ObjectField(
                "Object / Mesh",
                sourceObject,
                typeof(UnityEngine.Object),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyDefaultOutputName();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Use Current Selection", GUILayout.Width(160f)))
            {
                SyncSelectedSource();
            }
            EditorGUILayout.EndHorizontal();

            Mesh sourceMesh = ResolveSourceMesh();
            if (sourceObject != null && sourceMesh == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Mesh asset, MeshFilter, SkinnedMeshRenderer, or GameObject with a mesh.",
                    MessageType.Warning);
            }
            else if (sourceMesh != null)
            {
                EditorGUILayout.LabelField(
                    "Source Mesh",
                    $"{sourceMesh.name} ({sourceMesh.vertexCount:N0} vertices, {sourceMesh.subMeshCount:N0} submeshes)");
                if (!sourceMesh.isReadable)
                {
                    EditorGUILayout.HelpBox(
                        "This mesh is not readable. Enable Read/Write on its model importer before saving an inverted copy.",
                        MessageType.Warning);
                }
            }

            outputMeshName = EditorGUILayout.TextField("Output Mesh Name", outputMeshName);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(
                sourceMesh == null ||
                !sourceMesh.isReadable ||
                string.IsNullOrWhiteSpace(outputMeshName)))
            {
                if (GUILayout.Button("Invert and Save Mesh Asset", GUILayout.Height(30f)))
                {
                    InvertAndSave(sourceMesh);
                }
            }
        }

        private void SyncSelectedSource()
        {
            UnityEngine.Object selectedObject = Selection.activeObject;
            if (!TryResolveSourceMesh(selectedObject, out _) && Selection.activeGameObject != null)
            {
                selectedObject = Selection.activeGameObject;
            }

            if (!TryResolveSourceMesh(selectedObject, out _))
            {
                return;
            }

            sourceObject = selectedObject;
            ApplyDefaultOutputName();
            Repaint();
        }

        private void ApplyDefaultOutputName()
        {
            Mesh mesh = ResolveSourceMesh();
            if (mesh == null)
            {
                return;
            }

            string sourceName = string.IsNullOrWhiteSpace(mesh.name) ? "Mesh" : mesh.name;
            outputMeshName = $"{sourceName}_Inverted";
        }

        private Mesh ResolveSourceMesh()
        {
            TryResolveSourceMesh(sourceObject, out Mesh mesh);
            return mesh;
        }

        private static bool TryResolveSourceMesh(UnityEngine.Object candidate, out Mesh mesh)
        {
            mesh = candidate as Mesh;
            if (mesh != null)
            {
                return true;
            }

            if (candidate is MeshFilter meshFilter)
            {
                mesh = meshFilter.sharedMesh;
                return mesh != null;
            }

            if (candidate is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
                return mesh != null;
            }

            GameObject gameObject = candidate as GameObject;
            if (gameObject == null && candidate is Component component)
            {
                gameObject = component.gameObject;
            }

            if (gameObject == null)
            {
                return false;
            }

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                mesh = meshFilter.sharedMesh;
                return true;
            }

            skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
                return true;
            }

            return false;
        }

        private void InvertAndSave(Mesh sourceMesh)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Inverted Mesh",
                outputMeshName.Trim(),
                "asset",
                "Choose where to save the inverted mesh asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Mesh invertedMesh = null;
            try
            {
                invertedMesh = FPMeshInversionUtility.CreateInvertedCopy(sourceMesh, outputMeshName);
                string result = FP_Utility_Editor.CreateAssetAt(invertedMesh, path);
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (savedMesh == null)
                {
                    DestroyImmediate(invertedMesh);
                    EditorUtility.DisplayDialog(
                        "Unable to Save Inverted Mesh",
                        result,
                        "OK");
                    return;
                }

                Selection.activeObject = savedMesh;
                EditorGUIUtility.PingObject(savedMesh);
                Debug.Log($"[Invert Mesh] Saved inverted mesh to {path}", savedMesh);
            }
            catch (Exception exception)
            {
                if (invertedMesh != null && !EditorUtility.IsPersistent(invertedMesh))
                {
                    DestroyImmediate(invertedMesh);
                }

                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Unable to Invert Mesh",
                    exception.Message,
                    "OK");
            }
        }
    }
}
