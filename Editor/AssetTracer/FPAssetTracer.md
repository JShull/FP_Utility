# FP Asset Tracer

Open **FuzzPhyte > Utility > Editor > Debug > Asset Tracer**, or right-click a Project asset and choose **FuzzPhyte > Utility > Editor > Debug > Asset Tracer**.

1. Drag any saved asset into **Target asset**, or select it in the Project browser and click **Use Selection**.
2. Optionally include package assets as search candidates. By default the scan covers all imported assets under `Assets/`.
3. Click **Search References**. Scanning runs in cancellable Editor update steps.
4. Filter results by path. Turn off **Show indirect references** to see only immediate users.
5. **Select / Ping** locates the result in the Project browser. **Details** shows one shortest dependency chain, serialized property paths, component hierarchy names, and material texture slots where exposed. **Copy Paths** copies all filtered results, across pages.

Direct means the asset file depends on the target file. Indirect means it depends on another asset that eventually uses the target. Example: prefab → material → texture. The implementation reverses Unity's direct [AssetDatabase.GetDependencies](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.GetDependencies.html) relationships and handles cycles.

## Exact references and limitations

- AssetDatabase dependencies are file-level. A sprite or embedded material can share a file with other objects. Details explicitly distinguish the exact selected object from another object in the same file; file-level results alone are not proof of sub-asset usage.
- Scene details read saved text and show source line numbers, YAML document IDs, and nearby fields, including prefab override `propertyPath` entries. They do not open or change scenes. Binary scenes and importer-managed dependencies might have no readable field detail.
- Prefab and material details inspect imported objects without saving changes. Inherited or importer-managed dependencies might not expose a serialized object-reference field.
- Runtime Resources/Addressables/string lookups, arbitrary GUID strings in custom data, and unsaved changes are outside this dependency scan. A zero result is not a safe-deletion guarantee.
- Results are a snapshot. Asset changes invalidate the status; rescan after edits, imports, or package changes. Cancelled scans are incomplete. The Packages option controls referring assets; project assets can still refer to a package target with that option off.

The tool belongs to the existing Editor-only `com.fuzzphyte.utility.editor` assembly and adds no package dependencies. Searching and reviewing do not change target assets, prefabs, or scenes. Cleanup is a separate explicit action.

## Review and cleanup

After a complete scan, click **Review Real Uses / Missing-Prefab Overrides**. This checks every direct result, including results hidden by the path filter. It groups missing source prefab GUIDs and distinguishes files containing only orphan override records from other/unresolved uses. A matching GUID on a material, a normal component field, an unrecognized serialization layout, or an importer dependency prevents the tool from declaring that all saved direct uses are orphan records. Packages outside the selected scope and runtime/unsaved references remain unverified.

Each reviewed result offers:

- **Review Cleanup Details**: readable prefab/asset names alongside missing source GUIDs, instance and target fileIDs, and property paths.
- **Open Scene for Unity Override Cleanup**: opens the scene additively. It does not close or save existing scenes.
- **Unity: Remove Unused Overrides**: calls `PrefabUtility.RemoveUnusedOverrides` with `InteractionMode.UserAction` on loaded prefab roots whose modifications reference the selected object. Unity removes all unused overrides on those roots, including unrelated unused fields, while preserving valid overrides. Review the confirmation, use Undo if needed, and save the scene explicitly. This is not guaranteed to remove records for a completely deleted prefab.
- **Remove Missing-Prefab Reference Overrides**: a separate fallback for closed text scenes/prefabs. Confirm the source prefabs were intentionally deleted. It removes only standard four-line object-reference modifications pointing to the selected asset whose target prefab GUID matches a missing source prefab. It leaves all other overrides and the missing instance itself intact. Other surviving data might still require separate review.

The fallback rechecks source availability, file contents, loaded scene/prefab stages, and version-control editability immediately before writing. It refuses package assets. A byte-for-byte backup and restore instructions are stored under `Library/FP_Utility/AssetTracerCleanup/<unique-id>/` before changing the file. The fallback has no Unity Undo: restore through version control or copy the backup over the original with the scene/prefab closed, then refresh Unity. Keep the backup if clearing Library.

Cleanup confirmation appears inside the tracer: review the proposed change, then click **Confirm and Run Cleanup** or **Cancel Cleanup**. It does not rely on modal dialogs, which may be logged/dismissed by an automated Editor. A confirmation prompt alone is not evidence of a completed operation; the outcome and backup path appear in the window.

After successful closed-asset cleanup, the tracer waits for imports to settle, rescans, and reruns the reference audit automatically. For native cleanup that dirties a scene, it waits for you to save that scene before refreshing saved-reference results. It never saves your scene automatically. **Search References** remains available for a manual refresh.

Review remaining dependencies before concluding there are no saved uses. Lighting data is never edited; its indirect reference disappears only if Unity's refreshed dependency chain no longer reaches the texture. The tool never deletes the selected asset and never declares runtime-safe deletion.

Reference details also resolve saved GameObject names for component fields and source GameObject/component names for prefab overrides when available. Saved instance name overrides are listed separately because an instance can contain several renamed objects. Deleted prefab sources use their last known asset path when Unity still has it; unavailable names are explicitly labelled rather than inferred. GUIDs, fileIDs, raw serialized context, and scene line numbers remain available. Inspecting these names does not open or modify the scene. Click **Details** or **Review Cleanup Details** again to regenerate an already displayed report.
