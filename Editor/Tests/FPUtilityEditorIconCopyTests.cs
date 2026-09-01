// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    public class FPUtilityEditorIconCopyTests
    {
        [Test]
        public void BuildIconAssetCopyPlan_IncludesGizmoAndHeaderPaletteTextures()
        {
            List<KeyValuePair<string, string>> plan = FP_Utility_Editor.BuildIconAssetCopyPlan(
                "Assets/FP_Utility/Editor",
                "Assets/Gizmos/FP");

            Assert.That(
                plan.Any(entry => entry.Key == "Assets/FP_Utility/Editor/Gizmos/active.png"),
                Is.True);
            Assert.That(
                plan.Any(entry => entry.Key == "Assets/FP_Utility/Editor/Icons/HH_Open.png"),
                Is.True);
            Assert.That(
                plan.Any(entry => entry.Key.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)),
                Is.False);
            Assert.That(
                plan.All(entry => entry.Value.StartsWith("Assets/Gizmos/FP/", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                plan.Select(entry => entry.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Is.EqualTo(plan.Count));
        }
    }
}
