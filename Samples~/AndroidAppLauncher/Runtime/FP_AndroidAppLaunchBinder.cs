// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.
namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Events;

    public sealed class FP_AndroidAppLaunchBinder : MonoBehaviour
    {
        [SerializeField] private string targetPackageName = "com.fuzzphyte.experienceb";
        [SerializeField, TextArea] private string handoffData;
        [SerializeField] private UnityEvent onLaunchRequested = new UnityEvent();
        [SerializeField] private UnityEvent<string> onLaunchFailed = new UnityEvent<string>();
        public string TargetPackageName { get => targetPackageName; set => targetPackageName = value; }
        public event Action<FP_AndroidLaunchResult> LaunchCompleted;

        public void LaunchTargetApplication()
        {
            var result = FP_AndroidAppLauncher.TryLaunchApp(targetPackageName, string.IsNullOrEmpty(handoffData) ? null : handoffData);
            LaunchCompleted?.Invoke(result);
            if (result.Success) onLaunchRequested.Invoke();
            else onLaunchFailed.Invoke(result.Message);
        }
    }
}
