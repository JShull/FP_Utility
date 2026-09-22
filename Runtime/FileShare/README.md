# FP Utility File Share

Reusable single-file uploads from a Unity application to a Unity Editor inbox. The runtime assembly has no ARKit, RecorderFace, or Netcode dependency. The receiver and window are Editor-only.

## First transfer on Windows

1. Open **FuzzPhyte > Utility > Editor > File Share**.
2. Keep `127.0.0.1` for a same-machine test. Choose an inbox outside `Assets`, `Packages`, and `Library`, then click **Start receiver**.
3. Click **Use this receiver in test sender**. Choose a recording JSON or another local file, then click **Send / retry same file**.
4. A successful send reports a matching saved-file receipt. Use **Reveal** to inspect the inbox, or **Import into Assets** to copy a supported data file into the project and select it. Import uses a unique destination and never overwrites an asset.
5. Click **Stop receiver** when finished. Closing the window, reloading scripts, changing Play Mode, or exiting the Editor also stops it. It never starts automatically.

For another device, choose this computer's Wi-Fi/Ethernet IPv4 address before starting. Enter the displayed receiver address and pairing token on that device. `127.0.0.1` on a phone refers to the phone, not this computer. Pairing tokens change on receiver restart and are not serialized or written to logs/preferences.

New receiver tokens are 12 random characters (60 bits), excluding ambiguous `0/O` and `1/I`. Enter them as shown or use lowercase and optional spaces/dashes; the updated sender and receiver normalize the short format. Older 32-character Base64 tokens remain case-sensitive and unchanged. Existing apps can send a new short token exactly as displayed without a protocol update. Invalid-token responses incur a 250 ms delay to limit online guessing. This is a session token for trusted-LAN use, not a public-service password or a six-digit pairing code. HTTP still provides no encryption.

The test sender lets you retry the same file with the same transfer ID. Choose **New transfer ID** after changing the file or when intentionally sending another copy. A retry with the same ID and identical contents returns the existing receipt, including after a receiver restart; conflicting content/name returns HTTP 409.

## Runtime integration

Reference `com.fuzzphyte.utility.fileshare` from the calling assembly. Call on Unity's main thread:

```csharp
using FuzzPhyte.Utility.FileShare;

// Save the recording locally first. Retain this ID with the recording for retries.
var transferId = System.Guid.NewGuid();
var receipt = await FPFileShareSender.SendAsync(
    localRecordingPath,
    "http://192.168.1.20:18765/",
    pairingToken,
    transferId,
    progress: new System.Progress<float>(fraction => uploadProgress = fraction),
    cancellationToken: cancellationToken);
// Receipt means the receiver verified and committed the file, not merely uploaded it.
```

The sender hashes on a worker thread and streams from disk with `UploadHandlerFile`; it does not buffer a whole recording in memory. Keep the source unchanged until completion. Handle exceptions and cancellation in your UI. No automatic retries or source-file deletion occur. A lost response/cancellation after server commit is an uncertain outcome: retry with the same ID to resolve it without duplicating the file.

### Caller diagnostics

Transfer/receipt failures throw `FPFileShareException` (an `IOException`) with a stable `Failure` enum and `HttpStatusCode` (zero when no response is available). Categories: `TokenRejected`, `Conflict`, `SizeLimit`, `ChecksumMismatch`, `ConnectionFailure`, `Timeout`, `PolicyRejected`, `ReceiptMismatch`, and `HttpError`. Present its `Message` for actionable feedback; do not log tokens, request headers or file contents. Unity's known HTTP/ATS rejection messages are classified as policy errors; unfamiliar platform messages may still be reported as connection failures or their original exception. This is diagnostic classification, not a connectivity probe.

Cancellation still throws `OperationCanceledException` and keeps the task cancelled. Argument validation and local file I/O retain their normal exception types. A synchronous policy exception from Unity is retained as `InnerException`; use `Failure`/`Message` rather than `GetBaseException()` for UI classification.

```csharp
try { /* await FPFileShareSender.SendAsync(...); */ }
catch (OperationCanceledException) { /* Retain local file and transfer ID. */ }
catch (FPFileShareException ex)
{
    // Show ex.Message; use ex.Failure and ex.HttpStatusCode for UI decisions.
    // PolicyRejected requires an app build configuration fix, not a new token.
}
```

The iPhone test reported `Non-secure network connections disabled in Player Settings`, before reaching a receiver. The consuming app must configure Unity's HTTP policy and applicable iOS ATS/local-network permissions and rebuild. FileShare does not weaken those policies automatically. A JSON file is an independent upload: no WAV, audio recording, or microphone is required.

## Protocol and storage

- HTTP POST `/fp-fileshare/v1/files/{transferId:N}`, raw body, required Content-Length. Chunked requests are not supported.
- `X-FP-Token`: current receiver pairing token.
- `X-FP-Name`: Base64 of a UTF-8 basename (maximum 160 characters; paths, control characters, and cross-platform invalid filename characters are rejected).
- `X-FP-SHA256`: lowercase 64-character SHA-256 digest of the body.
- HTTP 201 means newly saved; HTTP 200 means already received. Receipt headers `X-FP-Transfer`, `X-FP-Name`, `X-FP-SHA256`, and `X-FP-Length` must match the upload before the sender reports success.
- Rejections: 400 invalid metadata/route; 401 invalid token; 409 ID conflict; 411 missing length; 413 size limit; 422 checksum mismatch. Other receive failures use 500 when a response is still possible.
- Default size limit: 128 MiB; default whole-request deadline: 120 seconds. One request is processed at a time. UI retains the latest 30 completed files during the current window lifetime.
- Files stage in `<inbox>/.fp-fileshare-staging` and are flushed, verified, and moved within the same volume to `<transferId>_<originalName>`. Existing files are never overwritten. Normal cancellation/failure removes its partial file. After a process crash, orphaned `.part` files may remain in staging; remove them only while the receiver is stopped. A lock file prevents two receivers using the same inbox simultaneously.
- Receiving accepts arbitrary file bytes outside the asset tree. The window's explicit import action accepts JSON, WAV, PNG, JPEG, TXT, and CSV only. Nothing is automatically executed or imported from the network.

## Network and platform scope

This first receiver uses HTTP on one explicit IPv4 address. Use it only on a trusted LAN: the token is access control, **not encryption**. There is no TLS setup, discovery, resumable/chunked upload, background iOS transfer, or live face streaming. The app must remain foregrounded for the initial iOS workflow. The inbox is not a file download server.

The receiver uses the Unity Editor's available `HttpListener` implementation to avoid a new server dependency. Listener behavior varies by OS/runtime; Windows loopback validation does not establish macOS behavior. A production/public-network service would need a separately chosen, supported server/TLS solution.

If Windows reports access denied when starting the listener, an administrator may need to reserve the **exact displayed prefix** for your Windows account. Example only (replace the address and account):

```powershell
netsh http add urlacl url=http://192.168.1.20:18765/ user=COMPUTER\User
# Remove that exact reservation when no longer needed:
netsh http delete urlacl url=http://192.168.1.20:18765/
```

A private-network firewall rule may also be needed for device access. Do not expose the port publicly. The package does not change URL reservations, firewall rules, signing, Player Settings, or iOS permissions.

For iOS integration, configure and test `NSLocalNetworkUsageDescription`, the applicable App Transport Security local-network settings, and Unity's HTTP policy for the chosen build. Bonjour declarations are unnecessary for this manual-address milestone. Build/sign on macOS and validate on the actual device. FP_RecorderFace still needs a local-save/send adapter; it is not wired by this module.

References: [Unity UploadHandlerFile](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UploadHandlerFile.html), [HttpListener platform considerations](https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener), [Apple local network privacy](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy), [ATS local networking](https://developer.apple.com/documentation/bundleresources/information-property-list/nsapptransportsecurity/nsallowslocalnetworking).

## Validation

Run the Edit Mode assembly `com.fuzzphyte.utility.fileshare.editor.tests`. Tests use temporary inboxes and loopback listeners, covering the real runtime sender, multi-megabyte files, duplicate receipts across restart, mismatched content, invalid token/name/checksum, size limits, and cancellation/receiver shutdown cleanup. Device networking, iOS/IL2CPP, and macOS receiver behavior require separate validation.

Windows validation on Unity 6000.6.0f1 (2026-09-22): compilation passed. All 16 test cases passed when their NUnit assertions and coroutine bodies were executed through a temporary Editor coroutine with fixture setup/teardown. The standard Test Runner workflow was not completed because the open scene had unsaved changes; the scene was preserved. A separate File Share window upload of FP_RecorderFace's `BobTest0.json` (5,170,921 bytes) returned a verified receipt and matched the original SHA-256. This is Editor loopback validation, not an iOS or LAN-device test.

Follow-up validation on the same date: the shorter-token and structured-error updates compiled, and all 31 FileShare cases passed (19 integration/protocol and 12 diagnostic/token cases), alongside 21 RecorderFace regression cases. Assertions/coroutine bodies ran through the same scene-preserving Editor approach, with unexpected Unity-error capture. Real loopback tests covered lowercase/grouped short tokens, unavailable receiver, timeout, authentication, conflicts, size rejection, verified receipts, retries and cancellation. No iPhone-to-Editor success is claimed; the reported device build blocked HTTP before contacting the receiver.
