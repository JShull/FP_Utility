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
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Disposable editor-owned grid provider. Uses the installed grid renderer feature without changing renderer assets.
    /// Configure uses only UnityEngine types so optional editor consumers can bind this API without a package reference.
    /// </summary>
    public sealed class FPSceneViewGrid : ScriptableObject
    {
        private Scene previewScene;
        private GameObject root;
        private FPRuntimeGridPlane grid;
        private Camera target;
        public FPRuntimeGridPlane Grid => grid;

        private void OnEnable()
        {
            hideFlags = HideFlags.HideAndDontSave;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += CheckCamera;
        }

        public void Configure(Camera camera, Vector3 origin, Quaternion rotation, Vector2 extents, float spacingMetres)
        {
            if (camera == null || camera.cameraType != CameraType.SceneView)
                throw new ArgumentException("An authoring grid requires a SceneView camera.", nameof(camera));
            if (!Finite(origin.x) || !Finite(origin.y) || !Finite(origin.z)
                || !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)
                || Math.Abs(Quaternion.Dot(rotation, rotation) - 1) > 1e-4f
                || !Finite(extents.x) || !Finite(extents.y) || extents.x <= 0 || extents.y <= 0
                || !Finite(spacingMetres) || spacingMetres < 1e-6f || spacingMetres > 1000)
                throw new ArgumentOutOfRangeException(nameof(spacingMetres), "Grid requires finite geometry and spacing from 0.000001 to 1000 metres.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Release(); return; }
            if (grid != null && target == camera && root.transform.position == origin && root.transform.rotation == rotation
                && grid.ExtentsWorld == extents && grid.SpacingInUnits == spacingMetres) return;
            if (root == null)
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                root = EditorUtility.CreateGameObjectWithHideFlags("FP SceneView Authoring Grid", HideFlags.HideAndDontSave);
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                grid = root.AddComponent<FPRuntimeGridPlane>();
                grid.Units = UnitOfMeasure.Meter;
                grid.UseMajorSpacing = false;
                grid.MinorColor = new Color(0.55f, 0.6f, 0.65f, 0.16f);
                grid.MajorColor = new Color(0.7f, 0.75f, 0.8f, 0.32f);
            }
            target = camera;
            root.transform.SetPositionAndRotation(origin, rotation);
            grid.SetCameraScope(camera, true);
            grid.ExtentsWorld = extents;
            grid.SpacingInUnits = spacingMetres;
            grid.RecalculateWorldSpacing();
            root.SetActive(true);
            SceneView.RepaintAll();
        }

        /// <summary>Empty means the active SceneView renderer has an enabled, configured grid feature.</summary>
        public string GetStatus()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return "The authoring grid requires URP.";
            using (var serialized = new SerializedObject(pipeline))
            {
                var renderers = serialized.FindProperty("m_RendererDataList");
                var selected = serialized.FindProperty("m_DefaultRendererIndex");
                if (renderers == null || selected == null || selected.intValue < 0 || selected.intValue >= renderers.arraySize)
                    return "Cannot resolve the SceneView renderer. Check the active URP asset.";
                var data = renderers.GetArrayElementAtIndex(selected.intValue).objectReferenceValue as ScriptableRendererData;
                if (data != null && data.rendererFeatures.OfType<FPRuntimeGridFeature>().Any(f => f != null && f.SupportsSceneView)) return "";
            }
            return "Enable FPRuntimeGridFeature with Draw In Scene View and a FuzzPhyte/GridPlaneURP material on the active URP default renderer.";
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void CheckCamera() { if (root != null && target == null) Release(); }
        private void OnPlayMode(PlayModeStateChange state) { if (state != PlayModeStateChange.EnteredEditMode) Release(); }
        private void Release()
        {
            bool existed = root != null;
            if (root != null) DestroyImmediate(root);
            root = null; grid = null; target = null;
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            previewScene = default;
            if (existed) SceneView.RepaintAll();
        }
        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.update -= CheckCamera;
            Release();
        }
    }
}
