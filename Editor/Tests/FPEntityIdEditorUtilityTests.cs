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
    using UnityEditor;
    using UnityEngine;

    public class FPEntityIdEditorUtilityTests
    {
        [Test]
        public void EntityIdGuidHelpers_RoundTripLoadedMonoScriptAsset()
        {
            var data = ScriptableObject.CreateInstance<FuzzPhyte.Utility.FP_Card>();
            MonoScript script = MonoScript.FromScriptableObject(data);
            string assetPath = AssetDatabase.GetAssetPath(script);
            GUID expectedGuid = AssetDatabase.GUIDFromAssetPath(assetPath);

            EntityId entityId = FP_Utility_Editor.GetEntityIdFromGUID(expectedGuid);
            GUID actualGuid = FP_Utility_Editor.ReturnGUIDFromEntityId(entityId, out bool success);

            Assert.That(entityId, Is.EqualTo(script.GetEntityId()));
            Assert.That(success, Is.True);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid));

            Object.DestroyImmediate(data);
        }

        [Test]
        public void EntityIdGuidHelpers_InvalidGuidReturnsNoneAndFailure()
        {
            EntityId entityId = FP_Utility_Editor.GetEntityIdFromGUID(default);
            GUID guid = FP_Utility_Editor.ReturnGUIDFromEntityId(entityId, out bool success);

            Assert.That(entityId, Is.EqualTo(EntityId.None));
            Assert.That(success, Is.False);
            Assert.That(guid, Is.EqualTo(default(GUID)));
        }
    }
}
