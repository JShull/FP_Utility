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
