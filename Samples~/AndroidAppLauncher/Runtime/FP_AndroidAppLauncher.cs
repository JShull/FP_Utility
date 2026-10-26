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

    public static class FP_AndroidAppLauncher
    {
        public const string PayloadExtraKey = "com.fuzzphyte.utility.android.handoff.v1";

        /// <summary>Conservative Android application ID syntax; does not verify installation.</summary>
        public static bool IsValidPackageName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return false;
            string[] segments = packageName.Split('.');
            if (segments.Length < 2) return false;
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || !IsLetter(segment[0])) return false;
                foreach (char c in segment)
                    if (!IsLetter(c) && !(c >= '0' && c <= '9') && c != '_') return false;
            }
            return true;
        }

        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        public static bool LaunchApp(string packageName) => TryLaunchApp(packageName).Success;

        /// <summary>Call from Unity's main thread while foregrounded. Success means startActivity returned.</summary>
        public static FP_AndroidLaunchResult TryLaunchApp(string packageName, string handoffData = null)
        {
            if (!IsValidPackageName(packageName))
                return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.InvalidPackageName, "Expected a dotted Android application ID.");
            string json = null;
            if (handoffData != null && !FP_AndroidHandoffPayload.TrySerialize(handoffData, out json, out string error))
                return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.InvalidPayload, error);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                        return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.LaunchFailed, "Unity Activity unavailable.");
                    using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
                    using (var intent = manager.Call<AndroidJavaObject>("getLaunchIntentForPackage", packageName))
                    {
                        if (intent == null)
                            return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.ApplicationUnavailable,
                                "No visible launchable Activity. Check installation, package visibility, and device policy.");
                        if (json != null)
                        {
                            using (var updated = intent.Call<AndroidJavaObject>("putExtra", PayloadExtraKey, json)) { }
                        }
                        activity.Call("startActivity", intent);
                        return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.Success, "Launch requested; destination readiness is not confirmed.");
                    }
                }
            }
            catch (Exception exception)
            {
                return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.LaunchFailed, exception.Message);
            }
#else
            return new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.UnsupportedPlatform, "Launching requires an Android player.");
#endif
        }

        /// <summary>Reads the Activity's current Intent; consumes only after successful validation.</summary>
        public static bool TryGetLaunchPayload(out FP_AndroidHandoffPayload payload, out string error, bool consume = true)
        {
            payload = null;
            error = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) { error = "Unity Activity unavailable."; return false; }
                    using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    {
                        if (intent == null) { error = "Launch Intent unavailable."; return false; }
                        string json = intent.Call<string>("getStringExtra", PayloadExtraKey);
                        if (!FP_AndroidHandoffPayload.TryDeserialize(json, out var candidate, out error)) return false;
                        if (consume) intent.Call("removeExtra", PayloadExtraKey);
                        payload = candidate;
                        return true;
                    }
                }
            }
            catch (Exception exception) { error = exception.Message; return false; }
#else
            error = "Receiving requires an Android player.";
            return false;
#endif
        }
    }
}
