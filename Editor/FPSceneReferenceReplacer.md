# Scene Reference Replacer

Open **FuzzPhyte > Utility > Scene > Scene Reference Replacer**.

The tool inspects the active loaded scene, including inactive objects and disabled components. It does not open other scenes, change assets, save scenes, add missing components, or retire either hierarchy.

## Workflow

1. Choose **Hierarchy**, assign an **Old Root**, and click **Trace Scene Links**. A replacement is optional for tracing.
2. Inspect **Inventory** and **Connections**. Connection groups distinguish incoming, outgoing, replacement-to-old, internal, and asset references. Internal and asset links are available by choosing their group; they are not proposed incoming changes.
3. Assign a distinct, non-overlapping **New Root** in the same active scene. Click **Build Replacement Preview**.
4. Review **Mappings**. Roots pair regardless of their names; descendants require unique exact relative name segments, and components require one exact type on each object. Duplicate ancestor names, repeated component types, renamed/moved children, and missing components remain unresolved.
5. Drag a scene GameObject or a component from its Inspector header into **New member** to resolve a mapping. **Ignore mapping** leaves its links unchanged. Many-to-one mappings require explicit review for each affected mapping. Click **Rebuild Preview** after changing mappings.
6. Review **Preview**. Surrounding-scene incoming edits start selected. Replacement-to-old edits require individual selection. Blocked rows explain their incompatibility. Results use pages of 100 rows. Filters affect display only; bulk selection selects all filtered ready incoming rows across pages, while deselection affects all filtered rows across pages.
7. Optionally enable **Transfer outgoing scene links** and rebuild. Supported transfers copy only individual scene-reference fields or existing compatible collection slots. Every outgoing transfer requires individual selection. Populated destinations are shown as conflicts; selecting one explicitly authorizes overwriting that slot. Equal collection sizes do not prove equal logical order: review every slot.
8. Click **Apply Selected Changes**. The tool checks the whole selection before writing, uses one Undo group, records prefab overrides, and rescans for remaining incoming and replacement-to-old links. Unexpected write failures roll back the tool's Undo group. Read changed/skipped/failed counts in **Status**.
9. Review outstanding connections, deliberately retire the old hierarchy, validate scene behavior, and save the scene yourself.

**Object Pair** retains the simpler root GameObject / selected component-script workflow using the same trace, preview, compatibility, and apply path. It excludes owners inside the old hierarchy from incoming replacement; replacement-to-old links are reviewed separately.

## Stale previews and reports

Scene/project changes and Undo/Redo invalidate the transient preview. Apply also compares the live scene inventory and serialized object snapshots and revalidates every selected field and binding. This is deliberately conservative: an unrelated serialized change can require a rebuild. Manual mapping choices survive a rebuild while both roots remain the same; they do not survive domain reload.

**Copy Markdown Report** and **Export Markdown Report** include inventory, connection categories, warnings, and proposed edits. Reports describe the captured scan, rather than guaranteeing runtime independence. No mapping presets or persistent object identities are stored.

## Supported coverage and limits

- Traversal uses `SerializedObject` and `Next`, including hidden fields, lists, nested data, and managed-reference contents exposed by Unity. Structural ownership and prefab metadata are excluded from writable application references.
- Incoming persistent UnityEvent targets and object arguments retain listener order, method, mode, arguments and call state. The destination method/signature is validated. Unresolved dynamic/native signatures remain blocked. Runtime listeners and delegates are outside this trace.
- Outgoing UnityEvents require structural listener migration and are reported as **Manual Transfer Required**. This release does not recreate or merge callbacks, resize/reorder collections, migrate renamed fields, or copy whole component configurations.
- PlayableDirector output bindings are enumerated explicitly and deduplicated against native serialized scene-binding fields. Outgoing bindings require an unambiguous identical output key on the destination director. Shared Timeline assets are read-only. Exposed-reference tables and asset-held clip/track references require manual review.
- Unknown receiving field types use conservative existing-target type compatibility. Some valid changed-type/manual mappings can therefore remain blocked, especially native or polymorphic serialized layouts.
- Other loaded/unloaded scenes, scene-global settings, asset-backed graphs, string/name lookups, static state, custom registries and runtime-created connections are outside the supported audit. No-reference findings apply only to the completed selected scope.
- Writes are blocked in Play Mode, during compilation/import, and in Prefab Mode. Cancelled or unreadable scans cannot apply changes. A rollback restores tool-owned reference changes; the scene may remain marked dirty by Unity.

## Validation

`FPSceneReferencePlannerTests` uses a Unity-created empty scene fixture copied into disposable test folders, leaving existing open scenes unsaved and intact. Focused Edit Mode coverage includes hidden/managed-reference fields, inactive content, boundary categories, unique/ambiguous mappings, changed-type compatibility, many-to-one review, stale selection, rollback, opt-in outgoing fields/slots, UnityEvents, explicit generic Timeline bindings, Undo/Redo, cancellation, and prefab overrides through scene save/reload.

Validated November 1, 2026 in the connected FPLibraries Unity 6000.6.7f1 Editor: compilation completed with zero errors/warnings, and all 17 focused Edit Mode cases passed. Empty and populated preview layouts were visually inspected. The package-wide test suite and project-specific runtime acceptance were not run.

Project-specific runtime acceptance, very large list responsiveness, missing-script repair, and broader event/list migrations are separate work. See [the design plan](FPSceneReferenceReplacerPlan.md) for acceptance scenarios and deferred extensions.
