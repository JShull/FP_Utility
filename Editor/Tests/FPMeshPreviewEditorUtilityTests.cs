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
    using UnityEngine;

    public sealed class FPMeshPreviewEditorUtilityTests
    {
        [Test]
        public void PainterFlushPreservesDenseSelectionAndDoesNotResaveWithoutChanges()
        {
            System.Type painterType = null, authorType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                painterType ??= assembly.GetType("FuzzPhyte.Utility.Editor.MeshTools.FPMeshVertexPainterWindow");
                authorType ??= assembly.GetType("FuzzPhyte.Utility.MeshTools.FPMeshVertexPaintAuthoring");
            }
            Assert.That(painterType, Is.Not.Null);
            Assert.That(authorType, Is.Not.Null);
            var window = ScriptableObject.CreateInstance(painterType);
            var go = new GameObject("Dense painter test");
            var mesh = new Mesh();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = window.GetType();
            try
            {
                mesh.vertices = new Vector3[20000];
                var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                var author = go.AddComponent(authorType);
                type.GetField("authoring", flags).SetValue(window, author);
                ((System.Collections.Generic.List<MeshFilter>)type.GetField("sourceMeshes", flags).GetValue(window)).Add(filter);
                var selected = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < 20000; i++) selected.Add(i);
                ((System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>)type.GetField("selectedByMesh", flags).GetValue(window)).Add(0, selected);
                type.GetField("pendingPaintWrite", flags).SetValue(window, true);
                type.GetMethod("FlushPendingPaint", flags).Invoke(window, null);
                Assert.That(((System.Collections.ICollection)authorType.GetProperty("PaintedVertices").GetValue(author)).Count, Is.EqualTo(20000));
                Assert.That((bool)type.GetField("pendingPaintWrite", flags).GetValue(window), Is.False);
                selected.Clear();
                type.GetMethod("FlushPendingPaint", flags).Invoke(window, null);
                Assert.That(((System.Collections.ICollection)authorType.GetProperty("PaintedVertices").GetValue(author)).Count, Is.EqualTo(20000));
                UnityEditor.Undo.ClearUndo(author);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PanKeepsContentUnderPointerInEitherProjection(bool orthographic)
        {
            var go = new GameObject("Preview pan test");
            var target = new RenderTexture(800, 600, 0);
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.orthographic = orthographic; camera.orthographicSize = 0.3f;
                camera.fieldOfView = 30f; camera.aspect = 800f / 600f;
                camera.transform.rotation = Quaternion.Euler(24, -36, 0);
                Vector3 focus = new Vector3(1, 2, 3);
                camera.transform.position = focus - camera.transform.forward * 2f;
                Vector3 before = camera.WorldToViewportPoint(focus);
                var drag = new Vector2(32, -18);
                camera.transform.position += FPMeshPreviewEditorUtility.CalculatePanDelta(camera, new Rect(0, 0, 800, 600), focus, drag);
                Vector3 after = camera.WorldToViewportPoint(focus);
                Assert.That((after.x - before.x) * 800, Is.EqualTo(drag.x).Within(0.001f));
                Assert.That((before.y - after.y) * 600, Is.EqualTo(drag.y).Within(0.001f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(target); }
        }

        [Test]
        public void CalculateFitDistance_ScalesWithTinyMeshBounds()
        {
            var previewRect = new Rect(0f, 0f, 800f, 600f);
            var tinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.001f);
            var unitBounds = new Bounds(Vector3.zero, Vector3.one);

            float tinyDistance = FPMeshPreviewEditorUtility.CalculateFitDistance(
                tinyBounds,
                previewRect);
            float unitDistance = FPMeshPreviewEditorUtility.CalculateFitDistance(
                unitBounds,
                previewRect);

            Assert.That(tinyDistance, Is.EqualTo(unitDistance * 0.001f).Within(0.000001f));
        }

        [Test]
        public void CalculateOrthographicSize_ScalesWithTinyMeshBounds()
        {
            var previewRect = new Rect(0f, 0f, 800f, 600f);
            var tinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.001f);
            var unitBounds = new Bounds(Vector3.zero, Vector3.one);

            float tinySize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(
                tinyBounds,
                previewRect,
                Quaternion.identity);
            float unitSize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(
                unitBounds,
                previewRect,
                Quaternion.identity);

            Assert.That(tinySize, Is.EqualTo(unitSize * 0.001f).Within(0.000001f));
        }

        [Test]
        public void ApplyScrollZoom_ClampsToSharedLimits()
        {
            float closest = FPMeshPreviewEditorUtility.ApplyScrollZoom(1f, -1000f);
            float farthest = FPMeshPreviewEditorUtility.ApplyScrollZoom(1f, 1000f);

            Assert.That(closest, Is.EqualTo(FPMeshPreviewEditorUtility.MinimumPreviewZoom));
            Assert.That(farthest, Is.EqualTo(FPMeshPreviewEditorUtility.MaximumPreviewZoom));
        }
    }
}
