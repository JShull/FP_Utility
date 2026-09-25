// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.TestTools;
    using Object = UnityEngine.Object;

    public sealed class FPSceneViewGridTests
    {
        private Scene testScene;
        private Camera first, second;
        private FPSceneViewGrid provider;
        private int initialCount;

        [SetUp]
        public void SetUp()
        {
            initialCount = FPRuntimeGridPlane.Active.Count;
            testScene = EditorSceneManager.NewPreviewScene();
            first = CameraObject("First grid camera"); second = CameraObject("Second grid camera");
            provider = ScriptableObject.CreateInstance<FPSceneViewGrid>();
        }

        private Camera CameraObject(string name)
        {
            var obj = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(obj, testScene);
            var camera = obj.GetComponent<Camera>();
            camera.enabled = false; camera.cameraType = CameraType.SceneView;
            return camera;
        }

        [TearDown]
        public void TearDown()
        {
            if (provider != null) Object.DestroyImmediate(provider);
            if (testScene.IsValid()) EditorSceneManager.ClosePreviewScene(testScene);
        }

        [Test]
        public void ConfigureOwnsPreviewSceneAndPreservesWorkingScene()
        {
            var scene = SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            int roots = scene.rootCount;
            var origin = new Vector3(2, 3, 4); var rotation = Quaternion.Euler(90, 20, 0);
            provider.Configure(first, origin, rotation, new Vector2(3, 4), 0.005f);
            Assert.That(provider.Grid.transform.position, Is.EqualTo(origin));
            Assert.That(Quaternion.Angle(provider.Grid.transform.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(provider.Grid.SpacingWorldMeters, Is.EqualTo(0.005f).Within(1e-8));
            Assert.That(provider.Grid.MajorEveryComputed, Is.EqualTo(10));
            Assert.That(EditorSceneManager.IsPreviewScene(provider.Grid.gameObject.scene), Is.True);
            Assert.That(FPRuntimeGridPlane.Active.Contains(provider.Grid), Is.True);
            Assert.That(scene.isDirty, Is.EqualTo(dirty));
            Assert.That(scene.rootCount, Is.EqualTo(roots));
        }

        [Test]
        public void ScopeRejectsOtherViewsGameAndDestroyedTarget()
        {
            provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f);
            var grid = provider.Grid;
            Assert.That(grid.IsVisibleTo(first), Is.True);
            Assert.That(grid.IsVisibleTo(second), Is.False);
            first.cameraType = CameraType.Game;
            Assert.That(grid.IsVisibleTo(first), Is.False);
            Object.DestroyImmediate(first.gameObject);
            Assert.That(grid.IsVisibleTo(second), Is.False);
            Assert.That(grid.IsVisibleTo(null), Is.False);
        }

        [Test]
        public void ReconfigureMovesScopeWithoutAddingAnotherGrid()
        {
            provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f);
            var grid = provider.Grid;
            provider.Configure(second, Vector3.one, Quaternion.Euler(30, 40, 50), Vector2.one * 2, 0.02f);
            Assert.That(provider.Grid, Is.SameAs(grid));
            Assert.That(grid.IsVisibleTo(first), Is.False);
            Assert.That(grid.IsVisibleTo(second), Is.True);
            Assert.That(FPRuntimeGridPlane.Active.Count, Is.EqualTo(initialCount + 1));
        }

        [Test]
        public void DisableAndDestroyUnregisterAndClosePreviewScene()
        {
            provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f);
            var grid = provider.Grid;
            grid.enabled = false;
            Assert.That(FPRuntimeGridPlane.Active.Contains(grid), Is.False);
            grid.enabled = true;
            Assert.That(FPRuntimeGridPlane.Active.Contains(grid), Is.True);
            var scene = grid.gameObject.scene;
            Object.DestroyImmediate(provider);
            Assert.That(grid == null, Is.True);
            Assert.That(scene.IsValid(), Is.False);
            Assert.That(FPRuntimeGridPlane.Active.Count, Is.EqualTo(initialCount));
        }

        [UnityTest]
        public IEnumerator ClosingCameraReleasesGridOnEditorUpdate()
        {
            provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f);
            Object.DestroyImmediate(first.gameObject);
            yield return null;
            yield return null;
            Assert.That(provider.Grid == null, Is.True);
            Assert.That(FPRuntimeGridPlane.Active.Count, Is.EqualTo(initialCount));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(float.NaN)]
        [TestCase(1001)]
        public void InvalidSpacingCreatesNoGrid(float spacing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, spacing));
            Assert.That(provider.Grid == null, Is.True);
            Assert.That(FPRuntimeGridPlane.Active.Count, Is.EqualTo(initialCount));
        }

        [Test]
        public void InvalidCameraAndGeometryPreserveCurrentGrid()
        {
            provider.Configure(first, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f);
            var grid = provider.Grid;
            second.cameraType = CameraType.Game;
            Assert.Throws<ArgumentException>(() => provider.Configure(second, Vector3.zero, Quaternion.identity, Vector2.one, 0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => provider.Configure(first, Vector3.zero, new Quaternion(), Vector2.one, 0.001f));
            Assert.That(grid.IsVisibleTo(first), Is.True);
            Assert.That(grid.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}
