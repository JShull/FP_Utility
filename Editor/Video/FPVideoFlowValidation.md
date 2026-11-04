# Native GPU seam flow — validation contract

Status: experimental method reconstruction, not OpenCV equivalence or camera calibration.
No native plugin, OpenCV assembly, model weights or inference package is required by the tool.

## Method and data

Reference: Gunnar Farnebäck, *Two-Frame Motion Estimation Based on Polynomial
Expansion* (SCIA 2003), sections 2, 3.2, 3.4 and 3.5, equations 1, 13 and 21–23:
https://www.ida.liu.se/ext/WITAS-ev/Computer_Vision_Technologies/Papers/scia03_farneback.pdf

Original implementation from these equations; no OpenCV/reference-source code is copied.
Gaussian quadratic fitting uses a 5×5 neighborhood (sigma 1.1); local displacement
constraints are Gaussian-averaged over 9×9 (sigma 2). Four pyramid levels and
iterative, rounded prior-displacement updates are used. An isotropic regularizer
and structure/validity tests contain poorly conditioned 2×2 solves.

Inputs are the two calibrated lens views at the same encoded video timestamp.
Two independent cylindrical longitude/elevation charts cover the opposite seam
meridians. Chart elevation is limited to ±80 degrees; poles fall back to ordinary
stitching. No neighborhoods or displacements may cross between the two charts.
Flow A→B is measured in chart pixels: positive X increases longitude, positive Y
increases elevation (Unity texture bottom-left coordinates). Warp limits are
specified in degrees, using each chart's angular sampling. This is a visual
correspondence estimate, not calibrated scene depth or physical object motion.

GPU data and reductions use float32 render textures. The constant least-squares
fit matrix is formed/inverted on the CPU in double precision, then uploaded as
float32. Linear-light luminance is scaled to 0–255 for conditioning. With temporal
smoothing disabled, the same frame/settings produce the same calculation independent of
scrub direction, prior playback or export start point. Cross-GPU bitwise identity
is not claimed.

Bidirectional consistency, photometric error, image masks and local structure gate
corrections. Displacements are bounded. Unreliable overlap matches retain the ordinary
uncorrected seam blend rather than forcing a false correspondence or a hard lens cut. Correction tapers
outside the seam band. Optional vector smoothing is described below; gyro/camera
stabilization and motion-compensated temporal tracking are not implemented.

## Acceptance before real-footage use

- Known synthetic translations of zero and small positive/negative X/Y shifts:
  median endpoint error ≤0.5 chart pixels, p95 ≤1 pixel on a central textured ROI.
- Flat images and invalid/occluded masks: confidence near zero; finite output.
- No cross-chart matching, boundary wrap or unbounded displacement.
- Repeated solves: maximum displacement difference ≤0.001 chart pixels on this GPU.
- Synthetic dual-fisheye reprojection with an imposed one-degree lens yaw error:
  median correspondence error ≤0.5 degrees in the central overlap; confidence
  coverage ≥40%; corrected difference mean ≤70% of raw difference on accepted pixels.
- Diagnostic modes never enter normal exports. Flow-off output remains unchanged.
- Log coverage and residual error on the real sample at the user's 196.4-degree
  FOV. Visual improvement alone does not prove reference equivalence or correctness
  at moving arms, water, reflections, thin structures or disocclusions.

An installed Python/OpenCV executable may be used as an **external test comparator**
only, with its version and settings recorded. It is not a product dependency.
Performance target: bounded texture ownership/reuse and a usable frozen-frame
preview at the default flow resolution; measure GPU completion separately from
CPU dispatch time. Native decoding/encoding time and flow time are distinct costs.

## Evidence

Validated 2026-11-03 in the running Unity 6000.6.7f1 Editor, Direct3D 12,
NVIDIA GeForce RTX 5090 Laptop GPU. All **25 FPVideo EditMode tests passed**
(18 existing video tests, five solver cases, two renderer/profile cases), none
skipped. Unity compilation reported no errors or warnings.

The named quantitative baseline is analytic ground truth, not OpenCV. The
128×128 linear grayscale fixture is a sum of four sinusoids, with continuous
translation applied before sampling. The central 64×64 ROI produced:

| True displacement (pixels) | Median endpoint error | p95 error | Confidence ≥0.35 |
| --- | ---: | ---: | ---: |
| (0, 0) | 0 | 0 | 100% |
| (2.5, 1.5) | 0.00326 | 0.00382 | 100% |
| (-2, 1) | 0.00044 | 0.00087 | 100% |

The independent-chart, flat/masked rejection, bounded-vector and repeated-solve
tests passed. The synthetic 196.4° dual-fisheye fixture with physical B yaw 181°
and assumed B yaw 180° passed the predeclared angular error, confidence and
corrected-residual bounds above. Zero strength reproduced flow-off pixels within
1e-5. Profile tests cover v2 round-trip, legacy disabling and atomic rejection of
an unknown enabled method. These analytic textures are smooth and do not cover
depth boundaries or real sensor noise; their small errors are not a real-scene
accuracy estimate. No OpenCV equivalence comparison was performed.

Real-data spot check: `LRV_20261103_102313_01_002.lrv`, paused timestamp **10.92 s**,
shared FOV **196.4°**, default centers/radii/rotations, directional seam yaw 0°,
blend width 10°. Flow settings: height 512, five iterations, limit 2°, strength 1,
confidence threshold 0.5. Two 104×512 charts comprise the 208×512 atlas. 30,186
of 106,496 atlas pixels (28.3%) reached the confidence threshold. This denominator
includes non-overlap; it is not a percentage of valid overlap or an accuracy score.
The corrected/raw RGB residual ratio was **0.529** over 16,818 nonzero accepted
diagnostic pixels in a 1024×512 equirectangular render (view rotation X=-90°).
Magenta rejects and black/outside pixels were excluded. This metric does not
establish that moving limbs, occlusions or depth discontinuities are correct.

Seven warmed renders including chart generation, bidirectional solve, 1024×512
composite and synchronous flow-field CPU readback had median **6.85 ms**. This is
completed wall-clock work, not a GPU timestamp or isolated solver duration; it
excludes media decoding/encoding and cannot predict full export throughput.
Default solver ownership is approximately **31 MiB** of reusable textures.
No CPU readback is performed by production flow; export's existing final image
readback remains necessary for Unity MediaEncoder.

A native **10.00–10.40 s** sample export at 2048×1024 completed with 10 frames,
19,200 audio sample frames, SHA-256 and the enabled method/settings in its JSON
report. Scratch images and this export were written under the user's temporary
directory, not beside the original LRV. The user's Editor settings were not
changed by those probes. Full-clip temporal quality, other GPUs/backends, large
occlusions and comparison against manufacturer stitching remain unqualified.

## Optional temporal vector smoothing

Engineering filter, not an extension claimed by the Farnebäck paper. The GPU
applies `filtered = lerp(current, previousFiltered, exp(-dt / responseSeconds))`
to XY displacement only. Confidence/error remain tied to the current match.
History is rejected when current or prior confidence is below the user threshold,
image alpha is invalid, displacement disagreement reaches the motion-reset angle,
or local A-luminance changes by 0.08 (linear 0–1 scale). Disagreement/appearance
gates taper from half those thresholds. A proposed smoothed warp must also stay
in its own chart and retain a valid B sample, with luminance residual at most
`min(0.12, rawResidual + 0.02)`. Otherwise the raw current match is used.

History requires forward source timestamps separated by one source-frame duration
(1% tolerance). A repeated timestamp (0.1% frame tolerance) reuses the filtered
field without integrating again. Gaps, reverse steps, invalid timestamps, seeks,
changed calibration/flow settings, raw diagnostic views and source reloads reset
history. Export owns separate history, initialized at export In without preroll.
Preview/export agree for the same start and consecutive frame sequence, not for
an arbitrarily warmed preview versus a new trimmed export. Skipped preview frames
reset instead of extrapolating. No RGB images are temporally averaged.

Acceptance set before validation: alternating ±0.2-pixel vector noise around a
one-pixel translation must fall below 0.1-pixel RMS after ten warmup frames at
25 fps / 0.08-s response, stay within the observation range, and never update on
a paused repaint. Low confidence, appearance changes, large vector jumps and bad
photometric matches must use the current match immediately. Discontinuous times
must reset; separate renderers at different output resolutions must agree given
the same sequence, including after calibration changes. These are synthetic
filter/continuity contracts, not real-scene motion accuracy claims.

Observed on the same Unity/DX12/RTX 5090 Laptop environment: **30/30 video EditMode
tests passed**, none skipped. Controlled vector noise RMS fell from **0.20000 to
0.04900 pixels** (75.5% lower), meeting the 50% reduction bound. Confidence,
appearance, large-motion, photometric, pause, seek/gap, settings reset, sequence
agreement and profile-migration tests passed. Response defaults to 0.08 s, motion
reset to 1°, smoothing disabled. History adds three reusable ARGBFloat textures
(4.875 MiB at 208×512), one compute pass and GPU copies, with no production CPU
readback. A 50-sample CPU/readback microbenchmark had 3.60-ms raw and 2.61-ms
smoothed medians but 17–19-ms p95 outliers; Editor/allocation noise prevents using
this as an isolated GPU cost estimate or evidence of a speed improvement.

A 10.00–12.00-s native LRV export at 2048×1024 and 196.4° FOV completed with
smoothing enabled: **50 frames**, **96,000 audio sample frames**, hash and temporal
method/settings in the report. It used the user's current flow strength 0.62,
warp limit 1.64° and confidence 0.653, on a settings snapshot. Artifacts stayed in
the user's temporary directory. Full-clip subjective flicker/lag quality remains
for review. Same-pixel history is deliberately rejected on changes; it cannot
track an occluded limb or guarantee a stable seam when confidence itself flickers.

## Rejection fallback correction

User comparison at approximately 45.2 s favored ordinary directional blending
with a 10° feather over flow. Inspection identified that rejected matches changed
the composite to a hard dominant-lens cut. That policy has been removed: rejected
matches now leave the same unwarped lens colors and feather weights as flow-off.
Accepted matches still apply the existing bounded warp; its visual quality is
not established by this fix. No lens or user settings were adjusted.

**31/31 video EditMode tests passed**, none skipped, after this change. The new
regression uses flat red/blue hemispheres (no reliable texture for correspondence)
and requires the mixed seam pixels to equal the 10° flow-off composite within
1e-5. This verifies fallback continuity, not improvement of accepted real-scene
matches or removal of all frame-to-frame artifacts.
