# Android App Launcher

Optional FP Utility sample for launching independently installed Android APKs by application ID. This starts an installed app; it does not install, download, or load an APK file into Unity. No third-party library or native plugin is required. See the package LICENSE.md.

## Import and configure

1. Build/target will only work set to Android. Make sure each APK has a distinct Player Settings application identifier, e.g. `com.fuzzphyte.applicationstart` and `com.fuzzphyte.applicationend`. Avoid targeting the caller itself unless intentional.
2. Explicitly configure destination visibility below.
3. Build and install both APKs on the same device/user profile. Confirm both open independently.
4. Add `FP_AndroidAppLaunchBinder` to a GameObject. Set Target Package Name and optionally Handoff Data. Connect a UI Button, UnityEvent, Timeline signal receiver, sequence, or C# Action to `LaunchTargetApplication()`.

The binder's launch-requested UnityEvent means startActivity returned; it does not confirm destination initialization. Its failure UnityEvent supplies a diagnostic string. `LaunchCompleted` supplies the full result to C# subscribers. No singleton or automatic scene action is created. The example component provides explicit launch and receive methods.

## Launch API

```csharp
using FuzzPhyte.Utility;

// Invoke on Unity's main thread from a foreground application, e.g. a button.
var result = FP_AndroidAppLauncher.TryLaunchApp("com.fuzzphyte.experienceb");
// Or: bool requested = FP_AndroidAppLauncher.LaunchApp(packageName);
```

Invalid IDs return InvalidPackageName. A null launch Intent returns ApplicationUnavailable: check installation, package visibility, launchable Activity, and device policy. Native exceptions return LaunchFailed. Expected failures do not log automatically. Editor and other players return UnsupportedPlatform after input validation. JNI wrappers are disposed. The caller is never quit or terminated.

## Explicit manifest configuration

Android 11+ filters package information for applications targeting API 30+. This launcher resolves destinations through PackageManager, so declare known destinations. Enable **Custom Main Manifest** under Android Player Settings > Publishing Settings and edit the generated `Assets/Plugins/Android/AndroidManifest.xml`. Preserve Unity's generated application/Activity entries and existing XR configuration. Merge this reference fragment into the root `<manifest>`, as a sibling of `<application>`:

```xml
<!-- Reference only: replace with your installed application IDs. -->
<queries>
    <package android:name="com.fuzzphyte.applicationstart" />
    <package android:name="com.fuzzphyte.applicationend" />
</queries>
```

Merge into an existing queries element if present. Add one package element for each destination. Each APK needs visibility for its own outgoing destinations. Unity/Gradle merges project/library manifests into the final APK manifest; inspect the final merged manifest in an exported Gradle project. Importing this sample makes no project changes and ships no active manifest. Do not request QUERY_ALL_PACKAGES.

References: [package visibility](https://developer.android.com/training/package-visibility), [known packages](https://developer.android.com/training/package-visibility/declaring), [Unity manifest configuration](https://docs.unity3d.com/6000.0/Documentation/Manual/android-manifest.html).

## Data handoff

```csharp
var result = FP_AndroidAppLauncher.TryLaunchApp(
    "com.fuzzphyte.experienceb",
    "{\"experienceId\":\"ExampleExperience\",\"nextModule\":\"Simulation\"}");

// In B at startup, or after resume with the integration below:
if (FP_AndroidAppLauncher.TryGetLaunchPayload(out var payload, out var error))
{
    string applicationData = payload.data;
    // Parse/validate this against your application's domain contract.
}
```

The second argument accepts plain strings or caller-serialized JSON. Null omits data; empty/whitespace or oversized data returns InvalidPayload. Unity JsonUtility wraps it as `{"version":1,"data":"..."}` under `com.fuzzphyte.utility.android.handoff.v1`. The complete envelope is limited to 16 KiB UTF-8, including JSON escaping. This is a conservative sample limit, not Android's transaction limit.

Receiving validates envelope size, JSON object shape, version 1, and nonempty data. Missing/invalid data returns false, an error, and null payload. Successful reads remove the extra by default; pass `consume: false` to inspect without removal. This is local consumption, not durable exactly-once delivery. No shared files are used. Inner JSON/schema validation belongs to the consuming app. Treat Intent data as untrusted; it does not authenticate a sender and should not contain credentials.

## Activity lifecycle and limits

A fresh Activity exposes its startup Intent through `currentActivity.getIntent()`. An already-running Activity can receive `onNewIntent(Intent)` instead, depending on launch mode and task stack. Android documents that getIntent continues to return the original Intent unless setIntent updates it. A Unity focus/resume callback alone therefore cannot guarantee fresh handoff data.

For reliable warm delivery, explicitly integrate with the receiving project's existing Activity subclass: override onNewIntent, call `super.onNewIntent(intent)`, then `setIntent(intent)`. Read after resume or use your own notification bridge after updating the Intent. Preserve Unity lifecycle handling and derive from the actual configured UnityPlayerActivity or UnityPlayerGameActivity; their manifest/base-class requirements differ. This sample installs no custom Activity, bridge, launch-mode changes, or custom flags. Review generated Activity code and test cold and warm delivery. Repeated launches are not a message queue; add domain request IDs/acknowledgments if required.

References: [Android Activity](https://developer.android.com/reference/android/app/Activity), [tasks and launch modes](https://developer.android.com/guide/components/activities/tasks-and-back-stack).

Background launch restrictions, device policy, locked profiles, and vendor behavior can alter/prevent launching. Trigger from foreground user interaction. Meta Quest / Android XR may impose headset shell, kiosk, entitlement, and Activity constraints; validate on actual target hardware. An app without a standard launchable Activity may not resolve. Native code runs only under `UNITY_ANDROID && !UNITY_EDITOR`; imported scripts provide safe fallbacks elsewhere. Runtime/examples have independent assembly definitions, with no core FP Utility or NUnit dependency. Tests have an Editor-only test assembly.

## Validation

After import run Edit Mode assembly `com.fuzzphyte.utility.androidlauncher.tests` in Unity Test Runner. It covers identifiers, result defaults, Editor fallback, payload serialization/deserialization, malformed/missing/versioned envelopes, bounds, and binder delegation. Edit Mode tests do not establish Android JNI behavior.

Manual two-APK procedure:

1. Cold-start A and launch B; confirm B initializes. Repeat B to A.
2. Target a missing APK; expect ApplicationUnavailable without a crash. Separately test a missing visibility declaration, checking the final manifest to distinguish filtering from absence.
3. Cold-launch B with string/JSON data; check exact data/version and verify a second consuming read fails.
4. Launch B while already running with different payloads. Test with and without optional onNewIntent/setIntent integration. Record Activity base class, launch mode, task stack, and delivered data.
5. Repeat A/B launches, Back, pause/resume, no-payload launches, and process recreation; inspect stale data, duplicate processing, and Activity instances. Repeat on target XR hardware if applicable.
6. Check invalid IDs/oversized data fail locally, expected failures do not automatically log, and the caller is not terminated.

Implementation-session validation: static packaging/source checks only. The host Unity Editor's Pipeline endpoint was unreachable, so sample import, Unity compilation, automated test execution, Android player build, and physical-device validation remain unverified.
