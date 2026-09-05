# FP Contribution Validation Tool Plan

## Status

Implementation is in progress. The shared header layer, static-validator MVP, Markdown reporting, guarded sample promotion, rollback, and focused Edit Mode tests are implemented. Unity compilation and broader acceptance workflows remain phased work.

Sample assembly-portability preflight is implemented: staging validation checks all sample scripts for sample-owned assembly boundaries, and promotion repeats the check before filesystem or manifest mutation. Current Unity import/compilation state must be clear. Focused regressions cover inherited parent/sibling assemblies, internal/external assembly references, scope-filter bypass prevention, and refusal without mutation. This observes current compiler state; automatic compilation orchestration and consumer import validation remain later phases.
## Purpose

Create a reusable FuzzPhyte Unity Editor tool for reviewing student, contractor, and external contributions before they are merged into `FP_Utility` or another FuzzPhyte Unity package.

The tool will convert the checks in `FuzzPhyte_Unity_Contribution_Validation_Checklist.md` into independently selectable validation sections. A reviewer can choose a folder or set of Unity assets, enable only the relevant checks, run them, and inspect the results without leaving the Editor.

Names such as `LabelDisplay` in the source checklist are examples only. The tool must not hard-code a feature name, namespace, assembly name, folder layout, or sample description.

## Goals

- Work with `FP_Utility` and other `FP_*` packages.
- Let the reviewer choose which checks run.
- Validate all eligible files or a reproducible random sample.
- Show passes, warnings, failures, skipped checks, and manual-review items in the EditorWindow.
- Attach evidence to findings, including asset paths and source lines where available.
- Reuse existing FuzzPhyte editor utilities and visual conventions.
- Reuse `FPScriptHeaderEditorWindow` for header inspection and repair.
- Promote a validated staging sample into `Samples~` and update `package.json` safely.
- Export a Markdown validation report suitable for pull-request review.
- Keep validation read-only unless the reviewer explicitly starts a repair or sample-promotion action.

## Non-goals for the first release

- Automatically approve or merge a pull request.
- Automatically rename or reorganize arbitrary contribution assets.
- Silently install Unity packages or third-party dependencies.
- Switch Git branches or modify repository history.
- Claim that licensing, ownership, architecture, or usability was fully verified by a static scan.
- Replace the Unity Test Framework, Package Manager, or the existing Script Header Editor.

## Location and package boundaries

All implementation will remain under:

```text
Assets/FP_Utility
```

Editor-only source will remain in the existing editor assembly:

```text
Assets/FP_Utility/Editor
```

Focused Edit Mode tests will remain in:

```text
Assets/FP_Utility/Editor/Tests
```

No runtime dependency or public runtime API is required for this tool.

## EditorWindow archetype

The tool will use a split validation-workspace layout implemented with Unity IMGUI.

Proposed menu path:

```text
FuzzPhyte/Utility/Editor/Testing/Contribution Validator
```

The menu priority will reuse `FP_UtilityData.MENU_UTILITY_EDITOR`. Colors, action emphasis, panel treatment, and status presentation will reuse `FP_Utility_Editor` and the established FuzzPhyte EditorWindow conventions.

### Layout

```text
┌──────────────────────────────────────────────────────────────────────┐
│ FP Contribution Validator                                            │
├──────────────────────────┬───────────────────────────────────────────┤
│ Scope and options        │ Results                                   │
│                          │                                           │
│ - Target folders         │ - Summary                                 │
│ - Sampling               │ - Severity filters                        │
│ - Rule sections          │ - Findings grouped by rule/file           │
│ - Rule configuration     │ - Open, select, ping, repair actions      │
│ - Sample promotion       │                                           │
├──────────────────────────┴───────────────────────────────────────────┤
│ Status, progress, errors, warnings, export                           │
└──────────────────────────────────────────────────────────────────────┘
```

Recommended minimum window size: approximately `900 x 600`.

## Validation scope

The reviewer will be able to validate:

- One Unity folder.
- Multiple Unity folders.
- The current Project selection.
- An entire FuzzPhyte package.
- Runtime, Editor, Tests, or staging-sample content.
- Optionally, files changed relative to a configurable Git base reference.

### Scope filters

- Include Runtime.
- Include Editor.
- Include Tests.
- Include staging sample content.
- Include or exclude extensions.
- Exclude folder patterns.
- Exclude filename patterns.
- Include or exclude untracked Git files when Git scope is enabled.

All path handling will be normalized and constrained to the selected roots. Generated Unity project directories such as `Library`, `Temp`, `Logs`, and `obj` will never become scan roots.

## Reproducible random sampling

The reviewer can choose:

- All eligible files.
- A fixed random file count.
- A random percentage.
- A deterministic seed.
- Resample with a new seed.

The eligible paths will be normalized and sorted before seeded selection so the same inputs and seed reproduce the same sample.

Critical structural files will support an **Always Include** option. These include:

- `package.json`
- Assembly definitions
- Assembly reference files
- Sample manifests or documentation required by a selected check
- Other explicitly configured critical patterns

Random sampling will therefore reduce repetitive file review without accidentally omitting package-defining files.

## Rule selection and presets

Every rule group will have:

- An enable/disable checkbox.
- An expandable configuration section.
- A result count.
- Select All and Clear All controls.
- A clear explanation of what the rule can and cannot prove.

Convenience presets will only toggle groups; they will not hide the underlying choices.

Proposed presets:

- Quick Static Review
- Code and Assemblies
- Assets and Samples
- Unity Compile and Tests
- Full Contribution Review

## Result model

Each result will have one of these states:

- Pass
- Warning
- Fail
- Not Run
- Cancelled
- Manual Review Required
- Manually Accepted

Each finding should contain:

- Rule identifier and display name.
- Severity/status.
- Short explanation.
- Asset path.
- Source line where available.
- Suggested remediation.
- Whether it blocks sample promotion.
- Optional reviewer note or waiver reason.

Result actions should include:

- Open the source file at the relevant line.
- Select or ping an imported Unity asset.
- Reveal the physical file when it is hidden from the Asset Database.
- Copy the finding.
- Send applicable scripts to the Script Header Editor.

## Validation rule catalog

### 1. Naming and organization

Configurable checks:

- File and primary class names match.
- Framework interfaces use the `IFP` prefix.
- Optional `FP_` type-prefix policy.
- Required namespace prefix.
- Forbidden legacy terms.
- Required or forbidden filename patterns.
- Generic namespaces such as `Common`, `Helpers`, or `Utilities`.
- Runtime, Editor, Tests, and sample files are in appropriate folders.
- Sample scenes, prefabs, materials, textures, and sprites are organized according to configured rules.
- Files outside the selected contribution root are reported.

Feature-specific values will be editable strings or patterns rather than hard-coded rules.

### 2. Assembly-definition validation

Configurable checks:

- Assembly filename and `name` pattern.
- Root namespace pattern.
- Assembly location.
- Runtime versus Editor platform configuration.
- `autoReferenced` expectation.
- `allowUnsafeCode` expectation.
- GUID-reference policy.
- Missing or duplicate references.
- Runtime assemblies referencing Editor assemblies.
- Suspicious or unnecessary references.
- Source-code dependencies not represented in assembly/package configuration.

### 3. Namespace and source-layout validation

- Namespace exists.
- Namespace starts with the configured FuzzPhyte root.
- Student-project or generic namespace roots are rejected or warned.
- `using` directives follow the configured FuzzPhyte placement convention.
- Editor-only scripts are separated from runtime code.

### 4. Script-header validation and repair

`FPScriptHeaderEditorWindow` will remain the user-facing source of truth for configuring and applying headers.

The reusable header behavior currently private to the window will be extracted into an internal editor utility shared by:

```text
FPContributionValidationWindow
              │
              ▼
    FPScriptHeaderUtility
              ▲
              │
FPScriptHeaderEditorWindow
```

The shared utility will own:

- Header normalization.
- Leading-header detection.
- Exact-match inspection.
- Required-content inspection when selected.
- Line-ending detection and preservation.
- Text-encoding preservation.
- Header replacement.
- Skip-unchanged behavior.
- Backup and rollback records.

Header results will distinguish:

- Correct header.
- Missing header.
- Different or outdated FuzzPhyte header.
- Existing third-party copyright.
- Malformed leading comment/header.
- Unreadable script.
- Skipped generated or excluded script.

The validator will provide **Fix with Script Header Editor**, which opens the existing window with the failing scripts already loaded. A bulk replacement shortcut may be added, but it must call the same shared utility, show a preview, create backups, and require confirmation.

Existing non-FuzzPhyte ownership or copyright text will block automatic replacement until reviewed explicitly.

### 5. Runtime and Editor separation

- `using UnityEditor` inside Runtime.
- Qualified `UnityEditor.*` use inside Runtime.
- Editor scripts outside an Editor assembly/folder.
- Runtime assembly references to Editor assemblies.
- Scattered compiler directives used in place of clear assembly separation.

### 6. Object identity

- `GetInstanceID()` usage.
- Stored Unity instance IDs.
- Suspicious `Dictionary<int, UnityEngine.Object>` patterns.
- Missing use of the established Unity 6 `EntityId` or FuzzPhyte stable-identity architecture when identity is required.

### 7. Dependency audit

- Assembly references.
- Package manifest dependencies.
- Known Unity packages and third-party frameworks used by source code.
- Third-party DLLs or copied source trees.
- Dependencies outside the selected package.
- Existing FuzzPhyte features that appear to have been reimplemented.

Automatic results will be an inventory and consistency check. Whether a dependency is justified remains a reviewer decision.

### 8. Public API and architecture review

Heuristic findings:

- New public classes, methods, fields, and properties.
- Public mutable fields that might be serialized private fields.
- Missing XML documentation on public APIs.
- Large MonoBehaviours that mix reusable logic with scene lifecycle.
- Mutable global/static state.
- Deep inheritance or unclear responsibilities.

These findings require human review and will not automatically fail a contribution unless the reviewer configures them as blockers.

### 9. Prefab and Unity-asset validation

- Missing scripts.
- Missing serialized object references.
- Required component presence.
- Dependencies outside the contribution/package.
- References to unrelated scenes or assets.
- Multiple-instance and runtime behavior recorded through optional tests.

Checks that require loading or modifying scenes will be explicit actions and will not run silently during a static scan.

### 10. Sample validation

- Expected sample folder structure.
- Missing scripts or references.
- Dependencies outside the package or sample.
- Sample code reaching into internal/private implementation details where detectable.
- Missing sample setup documentation.
- Compilation and test evidence.
- Manual Play Mode verification status.

### 11. Repository cleanliness

- `.vs/`
- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- `UserSettings/`
- `*.csproj`
- `*.sln`
- `*.csproj.user`
- Temporary files and screenshots.
- Unexpected large binaries.
- Duplicate or unused asset candidates.

Legitimate Unity-created metadata is not considered junk.

### 12. Documentation

- README update when setup or usage changes.
- Changelog entry.
- Public API documentation.
- Sample setup instructions.
- Dependency notes.
- Known limitations.
- Special configuration requirements.

### 13. Unity compilation and tests

Explicit optional actions:

- Refresh Unity assets.
- Wait for compilation.
- Capture contribution-related compiler errors and warnings.
- Run selected Edit Mode tests.
- Run selected Play Mode tests.
- Record pass, fail, skipped/inconclusive counts, duration, and failing names.

Unity test execution will use the supported Unity Test Framework API or an established repository command. Any direct Test Runner dependency will be isolated and reviewed before changing `package.json`.

### 14. Player build and clean-install checks

These will begin as explicit/manual gates with evidence fields:

- Clean project import result.
- Windows x64 Player build result.
- Optional additional platform result.
- Sample behavior in the built Player.

Automating builds must account for the existing `FPBuildProcessor` behavior before it is enabled. The validator must not unexpectedly modify an open scene as part of a build check.

### 15. Ownership and licensing

This section will remain a manual acceptance gate with:

- Contributor ownership statement.
- Third-party asset/source inventory.
- License and redistribution notes.
- Reviewer name/note.

The tool must not imply that a filename scan proves ownership or license compatibility.

## Sample promotion workflow

The EditorWindow will include a guarded **Sample Promotion** section for moving a tested staging sample into the package's hidden `Samples~` directory and updating `package.json`.

### Promotion fields

- Staging sample folder.
- Destination package root.
- Destination folder name.
- Package Manager display name.
- Manifest description.
- Generated manifest path preview: `Samples~/<FolderName>`.
- Required validation groups.
- Latest validation status.
- Preview Changes.
- Promote to Package Sample.
- Roll Back Last Promotion.

### Promotion gate

Promotion remains disabled unless:

- The selected required checks have run.
- No required result failed.
- Required compilation/tests passed.
- No blocking missing references were found.
- The destination does not already exist.
- The manifest does not contain a conflicting sample path.
- The staging content has not changed since validation.

The validation run will store a fingerprint based on normalized paths and relevant file state. If the source changes, the result becomes stale and promotion must be validated again.

Warnings and manual-review items may be accepted only with a reviewer note when the gate permits waivers.

### Promotion transaction

Before committing the move, the tool will display:

- Source folder.
- Destination folder.
- Files affected.
- Metadata handling.
- Exact `package.json` sample entry.

The transaction will:

1. Validate source, destination, and manifest paths.
2. Block on dirty/open sample scenes until the reviewer saves or closes them.
3. Create a recoverable manifest backup under `Library/FP_Utility/ContributionValidationBackups`.
4. Move the complete sample into `Samples~/<FolderName>` through Unity Editor-controlled operations.
5. Preserve existing sample metadata and internal references; never invent metadata.
6. Append or update the corresponding `samples` entry in `package.json`.
7. Preserve all unrelated manifest content and existing sample entries.
8. Reparse and verify the manifest.
9. Refresh Unity and report the final result.
10. Restore both the staging folder and manifest if any transaction step fails.

Expected manifest entry:

```json
{
  "displayName": "Configured Sample Name",
  "description": "Configured description of what the sample demonstrates.",
  "path": "Samples~/ConfiguredFolderName"
}
```

Unity ignores `Samples~` during normal Asset Database import. Functional validation must therefore occur while the sample is still in its imported staging location. A later phase can add a Package Manager import-and-revalidate action for verifying the consumer experience after promotion.

## Safety requirements

- Static validation is read-only.
- Repair actions are separate from validation actions.
- Invalid actions remain visible but disabled with an explanation.
- Header changes retain the existing backup workflow.
- Sample promotion requires a preview and confirmation.
- Promotion has transactional rollback on failure.
- No `.meta`, `.csproj`, or `.sln` files are authored by the implementation process.
- The tool must not overwrite an existing sample folder unexpectedly.
- The tool must not rewrite unrelated `package.json` fields.
- Long scans show progress and support cancellation.
- Expected validation failures appear in the window rather than relying on Console output.

## Persistent and transient state

Persistent EditorWindow state:

- Selected roots.
- Scope filters.
- Sampling mode, count/percentage, and seed.
- Enabled rules.
- Rule configuration.
- Result filters.
- Sample-promotion form values.

Transient state:

- Current scan iterator.
- Progress.
- Result caches.
- Hover/selection state.
- Cancellation state.
- Temporary manifest preview.

Saved reusable profiles may be considered after the initial workflow is stable. The first release does not require a new public ScriptableObject format.

## Proposed source structure

```text
Assets/FP_Utility/Editor/
├── FPScriptHeaderEditorWindow.cs
├── FPScriptHeaderUtility.cs
└── ContributionValidation/
    ├── FPContributionValidationWindow.cs
    ├── FPContributionValidationModels.cs
    ├── FPContributionValidationUtility.cs
    ├── FPContributionValidationRules.cs
    ├── FPContributionValidationReportWriter.cs
    └── FPSamplePromotionUtility.cs

Assets/FP_Utility/Editor/Tests/
├── FPScriptHeaderUtilityTests.cs
├── FPContributionValidationUtilityTests.cs
└── FPSamplePromotionUtilityTests.cs
```

The exact file split may be reduced during implementation if repository evidence shows that fewer cohesive files are clearer. The implementation should not create a generic validation framework beyond what these concrete workflows require.

## Testing plan

### Header utility Edit Mode tests

- Exact header match.
- Missing header.
- Different FuzzPhyte header.
- Existing third-party copyright.
- Line-comment and block-comment headers.
- Unterminated block comment.
- UTF-8 BOM preservation.
- CRLF and LF preservation.
- Empty script.
- Skip-unchanged behavior.
- Source body preserved after replacement.
- Backup created before modification.
- Duplicate targets ignored.

### Collection and sampling tests

- Scope remains inside selected roots.
- Include/exclude filters work.
- Same seed produces the same sample.
- Different seed can produce a different sample.
- Requested count is clamped safely.
- Critical files are always included.
- Empty folders and empty eligible sets are handled.
- Duplicate roots do not duplicate files.

### Static-rule tests

- File/type match and mismatch.
- `IFP` interface convention.
- Required and forbidden patterns.
- Correct, missing, and invalid namespaces.
- Runtime `UnityEditor` leak.
- `GetInstanceID()` detection.
- Correct runtime and Editor assembly definitions.
- Missing and suspicious assembly references.
- Repository-junk detection.

### Sample-promotion tests

- Valid manifest sample insertion.
- Existing samples are preserved.
- Duplicate path is rejected.
- Invalid manifest is rejected without mutation.
- Existing destination is rejected.
- Stale validation fingerprint blocks promotion.
- Move and manifest update succeed together.
- Failure restores source and manifest.
- Rollback restores the staging location and previous manifest.

Temporary Unity assets used by tests will be created and removed through Unity APIs. Unity will own any generated metadata.

## Documentation updates during implementation

- Add a Contribution Validator section to `README.md`.
- Add an entry under the current `CHANGELOG.md` Unreleased section.
- Document rule meanings, limitations, sampling behavior, header integration, promotion safety, and report export.
- Document any optional Test Framework integration or other dependency impact.

## Delivery phases

### Phase 1: Shared header behavior

- Extract reusable header inspection/replacement behavior.
- Preserve the current Script Header Editor UI and existing workflow.
- Add the preloaded-script handoff API.
- Add focused header utility tests.

### Phase 2: Static validator MVP

- Build the FuzzPhyte-styled EditorWindow.
- Add folder/selection scope.
- Add deterministic sampling.
- Implement naming, namespace, assembly, header, Runtime/Editor, identity, dependency-inventory, cleanliness, and documentation checks.
- Add result filtering, asset navigation, cancellation, and Markdown export.

### Phase 3: Unity asset checks

- Add prefab and scene missing-reference checks.
- Add dependency-boundary checks through Unity asset APIs.
- Add sample structure and setup checks.
- Add manual-review notes and waivers.

### Phase 4: Sample promotion

- Add promotion preflight and fingerprint gate.
- Add move/manifest preview.
- Add the transactional `Samples~` move.
- Update and verify the `samples` array.
- Add rollback and promotion tests.

### Phase 5: Compile and test orchestration

- Integrate active-Editor refresh and compilation observation.
- Run selected Edit Mode and Play Mode tests.
- Capture structured results in the window and exported report.
- Avoid introducing an undeclared Test Framework dependency.

### Phase 6: Acceptance workflows

- Add optional Git-diff scope.
- Add clean-install and Player-build evidence.
- Add Package Manager sample import-and-revalidate support.
- Generate the full pull-request acceptance report aligned with the source checklist.

## Dependency impact

The static validator, header integration, report export, and sample manifest editing should use existing Unity and .NET APIs and require no new third-party dependency.

Direct Unity Test Runner integration must be evaluated against the package's declared dependencies before implementation. No dependency will be added silently.

## Definition of done for the first usable release

- The window opens from the FuzzPhyte Utility testing menu.
- A reviewer can select one or more valid Unity roots.
- All-files and deterministic-random sampling work.
- Critical files can bypass sampling.
- Every implemented rule group can be enabled or disabled independently.
- Findings display status, evidence, path, source line, and remediation.
- Failed header scripts can be handed to the existing Script Header Editor.
- Static validation does not mutate contribution content.
- Results export to Markdown.
- Relevant Edit Mode tests pass in Unity.
- Unity refresh and compilation complete without new errors.
- README and changelog are updated.

Sample promotion is considered complete when a passing staged sample can be previewed, moved into `Samples~`, registered in `package.json`, verified, and rolled back without damaging unrelated manifest content or asset references.

## Implementation validation sequence

When implementation begins:

1. Confirm the Unity project root from `ProjectSettings/ProjectVersion.txt`.
2. Recheck the nested `Assets/FP_Utility` Git working tree and preserve unrelated changes.
3. Implement one phase at a time.
4. Let Unity generate new metadata.
5. Refresh and compile through the active Unity Editor when available.
6. Run focused Edit Mode tests first.
7. Run the containing editor test assembly when practical.
8. Review the final diff for generated files, unrelated edits, dependency changes, and accidental public API expansion.
