# Scene Reference Replacer

Open **FuzzPhyte > Utility > Scene > Scene Reference Replacer**.

The tool inspects the active loaded scene, including inactive objects and disabled components. It does not open other scenes, change assets, save scenes, add missing components, or retire either hierarchy.

## Resolving missing targets

In **Preview**, enable **Show blocked edits only**. Missing mapping diagnostics distinguish an absent component type from types that exist elsewhere under the replacement root. Each affected connection retains its property path and persistent callback details.

Click **Review candidates** on a blocked row or mapping to open **Suggestions**. The tool searches the replacement hierarchy and checks each candidate against all supported incoming fields, Timeline bindings and persisted callbacks for that target. Components of a different type are considered when persisted callbacks are present and compatible; GameObject suggestions require an exact name. Incompatible exact-type matches explain their blocker. Searching does not assign a mapping or dirty the scene.

Check the candidate's purpose: a breakfast objective and a jeans objective can share a script and method without serving the same role. **Use this reviewed mapping** assigns only the mapping. Rebuild the replacement preview, review resulting edits and resolve many-to-one conflicts before applying references.

## Keeping scene-specific objects

When an old objective/action belongs to the authored scene rather than the shared replacement system, use **Queue object to keep** on its mapping/blocked row, or select an old descendant in the Hierarchy and queue it from **Keep Objects**.

1. Review the explicitly queued GameObject subtrees. Queuing a component queues its entire GameObject and descendants, including every other attached component.
2. Choose a destination parent outside both system hierarchies in the same scene, or leave it empty to make the queued objects scene roots. No container is created automatically.
3. Click **Build Preservation Preview**. The preview lists source/destination parents, subtree sizes, effective activation, and supported links back to the part of the old hierarchy that will remain. A selected ancestor includes its selected descendants in one move.
4. Resolve blockers. The old system root itself and objects outside it cannot be queued for a move. Children inside prefab instances require manual unpacking first; eligible outermost prefab instance roots can move without unpacking. Destination parents must be outside prefab instances. Moves that would change effective activation are blocked until you deliberately adjust source/destination activation and rebuild.
5. Click **Apply Preservation Moves**. This is separate from **Apply Selected Changes**. It moves only the previewed subtrees, retains their identity and existing reference assignments, uses one Undo group, checks for stale state, and rolls back tool moves on unexpected failure. World position/rotation are retained; review nonuniform scale/shear inherited from different parents. No activation changes, deletion, copying, component creation or scene saving is performed.
6. Read the fresh audit and rebuild replacement mappings. Preserved owners that still reference the old system now appear as incoming links. Preservation does not automatically remove those dependencies or establish that it is safe to retire the old system.

Preservation queues can survive a window reload; preservation previews and candidate searches must be rebuilt. **Copy preservation report** captures the move preview for review. Test project-specific initialization and transform callbacks: arbitrary callback side effects outside tool-owned hierarchy moves are outside this operation's Undo contract. No TellVR objects are preserved automatically.

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

If several old owners map to one new owner, multiple outgoing proposals can address the same destination field or binding. These rows display a warning. Select only one proposal for that destination; selecting more than one blocks the entire apply before any writes. Acknowledging a many-to-one object mapping does not resolve conflicting field values.

## Stale previews and reports

Scene/project changes and Undo/Redo invalidate the transient preview. Apply also compares the live scene inventory and serialized object snapshots and revalidates every selected field and binding. This is deliberately conservative: an unrelated serialized change can require a rebuild. Manual mapping choices survive a rebuild while both roots remain the same; they do not survive domain reload.

**Copy Markdown Report** and **Export Markdown Report** include inventory, connection categories, warnings, and proposed edits. Reports describe the captured scan, rather than guaranteeing runtime independence. No mapping presets or persistent object identities are stored.

## Supported coverage and limits

The **Warnings** tab now includes a read-only Director reference audit: Director counts, exposed-field and asset-held-field counts, references targeting the old hierarchy, and unreadable asset counts. Paginated rows show the Director, playable/track/clip asset, track/clip location and serialized field, exposed key, override/default resolution status and resolved target, with Select/Ping controls. The Markdown report includes these locations. Re-run Trace or Rebuild Preview to refresh the captured audit. These rows are context, not proposed replacement edits. Coverage is limited to assigned playable assets and Timeline tracks/clips; arbitrary nested custom asset graphs and orphan Director table entries are explicitly excluded.

- Traversal uses `SerializedObject` and `Next`, including hidden fields, lists, nested data, and managed-reference contents exposed by Unity. Structural ownership and prefab metadata are excluded from writable application references.
- Incoming persistent UnityEvent targets and object arguments retain listener order, method, mode, arguments and call state. The destination method/signature is validated. Unresolved dynamic/native signatures remain blocked. Runtime listeners and delegates are outside this trace.
- Outgoing UnityEvents require structural listener migration and are reported as **Manual Transfer Required**. This release does not recreate or merge callbacks, resize/reorder collections, migrate renamed fields, or copy whole component configurations. Collection transfers require matching declared collection types, element layouts and sizes; unverified layouts remain blocked.
- PlayableDirector output bindings are enumerated explicitly and deduplicated against native serialized scene-binding fields. Outgoing bindings require the same playable asset and an unambiguous identical output key/type on the destination director. Different playable assets remain manual even if they expose the same key. Shared Timeline assets are read-only. Exposed-reference tables and asset-held clip/track references require manual review.
- Unknown receiving field types use conservative destination-current-value type compatibility. An empty native or polymorphic destination with an unknown declared field type remains blocked; the source target is not evidence of that destination's type. Some otherwise valid manual mappings therefore require manual transfer.
- Other loaded/unloaded scenes, scene-global settings, asset-backed graphs, string/name lookups, static state, custom registries and runtime-created connections are outside the supported audit. No-reference findings apply only to the completed selected scope.
- Writes are blocked in Play Mode, during compilation/import, and in Prefab Mode. Cancelled or unreadable scans cannot apply changes. A rollback restores tool-owned reference changes; the scene may remain marked dirty by Unity.
- Selected destination values and binding compatibility are checked immediately before each write and again after all writes. Detected callback changes to selected destinations roll back the transaction. Callback side effects outside the selected owners are outside the tool's Undo contract. A selection whose values already match is reported as skipped without dirtying the scene or creating an Undo group.

## Validation

`FPSceneReferencePlannerTests` uses a Unity-created empty scene fixture copied into disposable test folders, leaving existing open scenes unsaved and intact. Focused Edit Mode coverage includes hidden/managed-reference fields, inactive content, boundary categories, unique/ambiguous mappings, changed-type compatibility, many-to-one review, stale selection, rollback, opt-in outgoing fields/slots, UnityEvents, explicit generic Timeline bindings, Undo/Redo, cancellation, and prefab overrides through scene save/reload.

Validated November 1, 2026 in the connected FPLibraries Unity 6000.6.7f1 Editor: compilation completed with zero errors/warnings, and all 42 focused Edit Mode cases passed. Five regression cases reproduced failures before the phase 2–3 completion fixes. Coverage checks destination collisions, unknown empty native destinations, changed collection types, callbacks changing earlier/later selected fields, outgoing Timeline compatibility and Undo/Redo, matching-value skips, repeated-component manual assignments, other loaded scenes, changed asset outputs and opening the menu without a selection. Existing replacement configuration and persistent callbacks are preserved during outgoing field transfers.

Review/preservation tests cover moved and absent types, same-role uncertainty, changed-type callback candidates, incompatible methods, duplicate GameObject names, read-only candidate searches/previews, subtree normalization, retained pose/reference assignments, fresh dependency audits, Undo/Redo, stale previews, failure rollback, prefab boundaries, inherited activation, cancellation and retaining unrelated manual mappings after preservation.

Empty, populated, conflicting-outgoing, Suggestions and Keep Objects layouts were visually inspected using disposable scene content. The package-wide test suite and project-specific runtime acceptance were not run. These changes are in the Utility source checkout; publishing a package release and refreshing consuming projects are separate steps.

Project-specific runtime acceptance, very large list responsiveness, missing-script repair, and broader event/list migrations are separate work. See [the design plan](FPSceneReferenceReplacerPlan.md) for acceptance scenarios and deferred extensions.
