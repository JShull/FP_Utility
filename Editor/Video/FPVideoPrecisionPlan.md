# Video precision, metadata, grading and stabilization plan

Agreed 2026-11-04. Preserve managed C#/Unity shaders and compute shaders; no new native plugins, external decoder process, SDK DLL or package dependency. Test fixtures may be generated/checked with offline tools; these are not product dependencies.

## Milestones and gates

1. **Precision foundation — processing/export implemented; source ingest blocked.**
   - Qualify native decode using a lossless 10-bit reference, rather than trusting a texture format label.
   - Preserve stitching/grading results in FP16 linear RGB; resolve to 8-bit only for existing preview/H.264 output.
   - Export a full-resolution displayed frame as half-float linear EXR with source/processing precision provenance.
   - Do not claim end-to-end 10-bit video support until both native ingest and an appropriate video output path pass qualification.
2. **Metadata and geometry — factory projection bench-validated, opt-in.** Bounded v3 directory/protobuf inspection and X6 offset_v6 parsing are implemented. Unified-sphere projection with 13 distortion terms, measured rotations, calibration-frame scaling and selectable reversed track binding share preview, flow diagnostics, EXR and MP4. Projection is evaluated at infinity; recorded translation is retained but not applied. IMU scale/axes, exposure sample interpretation, video/sensor synchronization and factory-quality stitching remain pending. Manual calibration stays intact for comparison.
3. **Color and detail — pending.** Scopes, edge-aware shadows/highlights, color wheels, RGB curves, HSL controls, `.cube` 3D LUTs, followed by noise-aware detail processing. Share preview/export math and keep correspondence diagnostics ungraded.
4. **Stabilization and reframing — pending.** Validated gyro orientation, smoothing, horizon leveling, independent heading lock and smooth reframing.
5. **Rolling shutter — pending.** Requires validated IMU/video synchronization, sensor axes, readout duration/direction and lens mapping. Compare against a named reference before enabling.

Metadata extraction can progress independently while the ingest gate remains blocked. Adding creative curves does not solve lost source precision.

## Native decode qualification: Windows / Unity 6000.6.7f1

Separate temporary project, Edit Mode, D3D12, Linear color space, RTX 5090 Laptop. The user's live Editor and camera media were not modified by decoder experiments. Public VideoPlayer APIs only; no private playback/GetTexture bridge.

Reference: 4096x64, 25 fps, 25 identical HEVC Main10 frames, BT.709 limited-range. Each of 1024 vertical bars is four pixels wide. Luma is `64 + floor(bar * 876 / 1023)`, chroma U/V = 512. This has 877 distinct luma codes. Offline libx265 lossless/all-intra encoding, then raw YUV decoding, reproduced every source code exactly (maximum error 0).

Encoded fixture SHA-256: `1b26d0be75681c8db746db6f826d1133c8950d09efc671b79e4436fe153f0c2f`.

Tool provenance: FFmpeg `N-124093-g5134b0aceb-20260424`. Offline validation only. Fixture generation:

```text
Generate little-endian planar yuv420p10le using the bar formula above, 4096x64.
ffmpeg -stream_loop 24 -f rawvideo -pixel_format yuv420p10le -video_size 4096x64 -framerate 25 -i precision10.yuv -c:v libx265 -x265-params lossless=1:keyint=1 -color_range tv -colorspace bt709 -color_trc bt709 -color_primaries bt709 precision10.mp4
```

Qualification samples bar centers on a middle row at native resolution, with no scaling. GPU sampling uses an FP32 blit/readback so unsupported direct packed-format ReadPixels conversion cannot masquerade as a valid measurement. Each GPU format is tested in a fresh Editor process: immediate same-process recreation produced native error 10 on later players and was excluded from the precision conclusions.

| Native path | Destination | Distinct sampled levels | Conclusion |
| --- | --- | ---: | --- |
| Editor MediaDecoder | RGBA32 | 220 | Source precision is reduced before processing. |
| Editor MediaDecoder | RGBAHalf / RGBAFloat | Invalid/unchanged storage | Not a supported way to request high-precision native output. |
| VideoPlayer | ARGB32 / sRGB | 216 | Does not retain 10-bit distinctions. |
| VideoPlayer | ARGB2101010 | 206 | A packed 10-bit target does not establish 10-bit ingest. |
| VideoPlayer | ARGBHalf | 220 | FP16 target receives already-quantized values. |
| VideoPlayer | ARGBFloat | 220 | FP32 target receives already-quantized values. |

Acceptance gate: retain at least 800 of the 877 reference levels with monotonic response and bounded value error. **None of the tested native decode paths passes.** Counts describe this controlled fixture/backend, not a guarantee about all Unity versions or drivers. The CPU readback with unsupported formats and failed native players are not treated as successful decodes.

The installed MediaEncoder exposes H.264 (Baseline/Main/High) and VP8, with no H.265/Main10 or ProRes encoder attributes. Existing H.264 remains 8-bit. A future end-to-end route requires a newly qualified Unity-native API, or explicit authorization to change the native-only dependency constraint. Do not add interop or SDK dependencies implicitly.

## Delivered processing and EXR contract

- FP16 linear RGB working texture on supported graphics devices, with an explicit 8-bit SDR resolve for preview and MP4. Existing Unity SDR input interpretation is preserved; BT.709/sRGB input-management differences have not been redefined here.
- Gamma-project shader outputs are decoded before writing the linear working buffer, then explicitly encoded for SDR display. Linear projects use hardware sRGB resolve. Devices without FP16 support retain the old 8-bit path and disable EXR frame export.
- EXR uses RGBAHalf, ZIP compression and matching-format CPU readback. It preserves the processed FP16 result; it does not recover original 10-bit camera samples, create HDR, remove baked camera processing, or bypass SDR tone-curve clipping.
- Frame export re-decodes the displayed timestamp at full source resolution; it never exports an enlarged scrub-preview texture. It uses current projection/grade settings, excludes preview-only diagnostics/grid, and records the actual decoded timestamp and settings in JSON.
- Existing output/JSON files are protected from overwrite. One EXR frame and its report are published from temporary files. Image sequence export and 10-bit video encoding remain pending.
- Memory cost: an additional 8 bytes per output pixel for the working texture (64 MiB at 4096x2048), plus the existing 4-byte SDR resolve. EXR readback/encoding temporarily adds CPU storage. No per-frame preview GPU readback is introduced.

Processing acceptance: a synthetic FP32 877-level ramp must retain at least 800 levels in working storage and EXR roundtrip, with absolute linear error below 0.002; the final 8-bit resolve must have no more than 256 levels. Keep these processing tests separate from the blocked native-ingest test.

## Metadata evidence already inspected

The user's `VID_20261103_102313_00_002.insv` has two 3840x3840 HEVC Main10/BT.709 video tracks, 50 fps, plus AAC audio. Its version-3 Insta360 trailer includes metadata, a raw IMU record, exposure records for both lenses, gyro configuration/timing, rolling-shutter timing, `offset_v6` and `original_offset_v6` calibration strings. `gamma_mode` is `standard`. No separate GPS sample record was identified; a GPS-related timestamp is insufficient to claim a usable route.

The tool now interprets the supported factory lens model as an opt-in projection. IMU and timing fields are not applied. Sensor units, sign/axis conventions and timestamps still require validation before stabilization.

## Factory projection qualification

X6 `offset_v6`, packed generation 6, two 27-number lens blocks, lens type 193. The calibration frame is 15488x7744, with each lens occupying 7744x7744; coded tracks are 3840x3840. Scale intrinsics by the calibration frame, not by treating focal lengths as coded pixels. Calibration points use pixel coordinates with Y down; Unity rays use Y up. The model uses normalized sphere projection `m=(X,Y)/(Z+xi)` and a five-term radial polynomial plus extended tangential and thin-prism terms. The v6 tangential parameter order differs from the older v3 Brown model.

Track type 4 declares reversal. The first decoded track defines the default forward/upright rig pose, preserving the manual workflow's hemisphere convention; this is a global choice of body orientation, not a change to the relative lens calibration. The factory rotations map vendor-world to camera as `Ry(pitch+90) Rz(yaw) Rx(roll)` with a Z half-turn on calibration lens 0. Conjugation by `diag(1,-1,1)` carries camera/rig vectors between Y-down and Unity Y-up coordinates. Projection retains the user's FOV only as a coverage cutoff, not as a radial scale factor.

Acceptance set before integration: compare C# projection to independent NumPy double-precision rotation matrices/scalar equations within 0.02 source pixels; compare the actual shader in FP32 readback against CPU mapping within 0.25 source pixels at 7680x3840. Avoid FP16/8-bit readback when measuring UV error. Test both hemispheres, reversed binding, nonzero rotations/distortion, manual restoration, deep export snapshots, invalid data rejection and calibration-profile roundtrip. These are numerical/contract checks, not proof of equivalence to Insta360 Studio or correct near-field stitching.

Source references: `telemetry-parser/src/insta360/rig.rs`, `extra_info.rs`, and the `MeiDistortion::RadtanPro` equations in `video-types` 0.1.2 `src/geometry.rs` (crates.io). These MIT/Apache-licensed primary implementation descriptions inform an independent C#/HLSL implementation; no Rust runtime or vendor SDK is included. Track binding/body conventions still require visual scene review; the UI exposes a reversal override. No per-body vendor mask polygons are reconstructed.

The current supported model is deliberately restricted to X6 lens type 193, two matching square calibration frames and known packed mirror flags. Unsupported factory data leaves ordinary video/manual projection available. Timing fields are inspected in their recorded representation; no guessed unit conversion, gyro axis mapping, orientation fusion or rolling-shutter correction is applied.

Integration qualification, Unity 6000.6.7f1 / D3D12: **65/65 video EditMode tests passed**, including seven factory-specific cases and mirror-flag coverage. A manual/factory comparison at source 1100.54 s confirmed consistent gross orientation and image coverage. A two-frame 1024x512 factory-mode H.264 sample retained 50 fps and recorded its model in JSON. The actual Load embedded calibration UI and manual-field disabling were visually exercised. This short sample does not qualify a full recording, near-field quality, or proprietary Studio equivalence.

Primary implementation references for future metadata parsing: [telemetry-parser Insta360 reader](https://github.com/AdrianEddy/telemetry-parser/tree/master/src/insta360), and [Insta360 Media SDK documentation](https://insta360develop.github.io/Insta360-Developer_Docs/en/x/desktop/media/). References inform our managed implementation; the native SDK is not a dependency.
