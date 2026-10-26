// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.
namespace FuzzPhyte.Utility.Examples
{
    using UnityEngine;

    public sealed class FP_AndroidLaunchExample : MonoBehaviour
    {
        [SerializeField] private string targetPackageName = "com.fuzzphyte.experienceb";

        public void LaunchNextExperience()
        {
            var result = FP_AndroidAppLauncher.TryLaunchApp(targetPackageName, "{\"experienceId\":\"ExampleExperience\",\"nextModule\":\"Simulation\"}");
            if (!result.Success) Debug.LogWarning(result.Message, this);
        }

        // Invoke at startup, or after resume when optional onNewIntent integration is installed.
        public void ReadHandoff()
        {
            if (FP_AndroidAppLauncher.TryGetLaunchPayload(out var payload, out _))
                Debug.Log(payload.data, this);
        }
    }
}
