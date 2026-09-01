// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using NUnit.Framework;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class FPHHeaderCompatibilityTests
    {
        private Scene previewScene;
        private GameObject testObject;

        [SetUp]
        public void SetUp()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [Test]
        public void IsHeaderObject_AcceptsInactiveAllCapsLeaf()
        {
            testObject = new GameObject("HEADER");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.True);
        }

        [Test]
        public void IsHeaderObject_RejectsMixedCaseInactiveLeaf()
        {
            testObject = new GameObject("Header");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.False);
        }

        [Test]
        public void IsHeaderObject_RejectsHeaderWithChildren()
        {
            testObject = new GameObject("HEADER");
            var child = new GameObject("Child");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            SceneManager.MoveGameObjectToScene(child, previewScene);
            child.transform.SetParent(testObject.transform);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.False);
        }
    }
}
