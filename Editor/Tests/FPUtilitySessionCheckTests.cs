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
    using NUnit.Framework;
    using UnityEditor;

    public class FPUtilitySessionCheckTests
    {
        private const string UpdateKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.AutoUpdateUtility";
        private const string MessageKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.ShowPackageMessages";
        private bool hadUpdate, updateValue, hadMessage, messageValue;

        [SetUp]
        public void SetUp()
        {
            hadUpdate = EditorPrefs.HasKey(UpdateKey);
            updateValue = FPUtilitySessionCheck.AutoUpdateUtility;
            hadMessage = EditorPrefs.HasKey(MessageKey);
            messageValue = FPUtilitySessionCheck.ShowPackageMessages;
        }

        [TearDown]
        public void TearDown()
        {
            if (hadUpdate) EditorPrefs.SetBool(UpdateKey, updateValue);
            else EditorPrefs.DeleteKey(UpdateKey);
            if (hadMessage) EditorPrefs.SetBool(MessageKey, messageValue);
            else EditorPrefs.DeleteKey(MessageKey);
        }

        [Test]
        public void MissingPreferences_DisableUpdatesButKeepWelcomeMessages()
        {
            EditorPrefs.DeleteKey(UpdateKey);
            EditorPrefs.DeleteKey(MessageKey);
            Assert.That(FPUtilitySessionCheck.AutoUpdateUtility, Is.False);
            Assert.That(FPUtilitySessionCheck.ShowPackageMessages, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdateMenu_TogglesPersistentlyWithoutChangingMessages(bool showMessages)
        {
            FPUtilitySessionCheck.ShowPackageMessages = showMessages;
            FPUtilitySessionCheck.AutoUpdateUtility = false;
            Assert.That(EditorApplication.ExecuteMenuItem("FuzzPhyte/Utility/Package Messages/Auto Update FP_Utility"), Is.True);
            Assert.That(EditorPrefs.GetBool(UpdateKey), Is.True);
            Assert.That(FPUtilitySessionCheck.ShowPackageMessages, Is.EqualTo(showMessages));
            EditorApplication.ExecuteMenuItem("FuzzPhyte/Utility/Package Messages/Auto Update FP_Utility");
            Assert.That(EditorPrefs.GetBool(UpdateKey), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MessageMenu_DoesNotChangeUpdatePreference(bool autoUpdate)
        {
            FPUtilitySessionCheck.AutoUpdateUtility = autoUpdate;
            FPUtilitySessionCheck.ShowPackageMessages = true;
            FPUtilitySessionCheck.TogglePackageMessages();
            Assert.That(FPUtilitySessionCheck.ShowPackageMessages, Is.False);
            Assert.That(FPUtilitySessionCheck.AutoUpdateUtility, Is.EqualTo(autoUpdate));
        }
    }
}
