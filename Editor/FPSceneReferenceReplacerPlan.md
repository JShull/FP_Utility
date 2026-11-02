# Scene Reference Replacer: Hierarchy Tracing and Replacement Proposal

Status: phases 1–3 implemented following the user's update request on November 1, 2026. See `FPSceneReferenceReplacer.md` for delivered coverage and limitations. No authored scene migration has been performed.

Prepared: November 1, 2026.

## Purpose

Extend the existing Scene Reference Replacer into a general Unity Editor workflow for inspecting and replacing a section/list/a gameobject with children objects in the scene hierarchy while preserving its connections to surrounding scene content.

The motivating case is replacing an older `TellVR_System` in a scene that already contains substantial authored work. The solution belongs in `FP_Utility` and must work with any scene hierarchy and generally will be a combination of nested gameobjects, e.g. a singular gameobject with 20 children that have hundreds of scripts on them that all have self references internally to each other but then within the scene there could be thousands of references back to these nested objects/scripts etc. We would also need to maintain external references outside of those nested game objects. In most cases the nested gameobjects already have references to themselves so we wouldn't need/want to replace any of them but we would need to go through events/actions that have references outside of those objects.

The core question is: **Which connections cross this nested gameobject boundary, and what must change when another nested gameobject boundary takes its place?**

This complements the Asset Tracer. The Asset Tracer searches saved asset dependencies across the project. This extension inspects live, loaded scene objects and their serialized connections, including unsaved scene edits.

## Existing implementation

Source: `Editor/FPSceneReferenceReplaceEditorWindow.cs` in `com.fuzzphyte.utility`.

Menu: **FuzzPhyte > Utility > Scene > Scene Reference Replacer**.

The inspected implementation supports:

- One old GameObject and one new GameObject.
- Optional replacement of references to one selected component script type on those objects.
- An active-scene scan with an option to include inactive objects.
- Serialized object-reference replacement using `SerializedObject` and `NextVisible`.
- Explicit PlayableDirector generic-binding replacement, including Signal Track handling.
- Undo grouping, dirty marking, and Console logging.

It does not currently provide a hierarchy-wide correspondence map, a boundary-only trace, or a selectable preview of proposed edits. Its scene scan includes objects inside the supplied roots. The Signal Track fallback can add a SignalReceiver to the replacement object, and the binding-debug toggle is displayed but is not checked at the logging call site.

Preserve the existing menu and namespace. Retain a simple object-pair workflow while adding hierarchy mode; route both through the same preview and apply behavior where practical.

## Scope and terminology

| Term | Meaning |
| --- | --- |
| Old root | Scene GameObject whose hierarchy is being inspected or retired. |
| New root | Replacement scene GameObject, supplied by the user. |
| Inside | The selected root, every descendant, and their components, including inactive objects. |
| Outside | Other objects within the selected loaded-scene scope. |
| Incoming link | An outside owner holds a reference to an object or component inside the old hierarchy. |
| Outgoing link | An owner inside the old hierarchy holds a reference to an outside scene object or component. |
| Internal link | Both owner and target belong to the same selected hierarchy. |
| Asset link | A scene owner references a persistent project/package asset. |
| Object mapping | An explicit old GameObject/component to new GameObject/component correspondence. |
| Proposed edit | One specific reference or supported binding change, with its expected current value and proposed value. |

An owner is the object that stores a serialized field or binding. A target is the object referenced by that field or binding. A script asset is not interchangeable with the component instance attached to a scene object.

## Default operating boundaries

- Start with the active scene only.
- Include inactive and disabled scene content by default.
- Require old and new roots to be distinct, non-overlapping scene hierarchies. Initially require both roots in the same normal loaded scene.
- Allow tracing with only an old root; require a new root for a replacement plan.
- Exclude both supplied hierarchies from the surrounding-scene incoming-owner set. Report replacement-to-old links separately because they also prevent retirement of the old hierarchy.
- Do not open unloaded scenes, traverse the project, or modify assets during a scene operation.
- Block writes in Play Mode, during compilation/import, or in Prefab Mode for the initial release.
- Do not instantiate, delete, reparent, rename, activate, deactivate, or save either hierarchy automatically.

Keeping both hierarchies present is a preparation state. Runtime acceptance happens after the user deliberately retires the old system and resolves any duplicate managers or startup logic.

## User workflow

1. Open the existing Scene Reference Replacer.
2. Choose **Hierarchy** mode and assign **Old Root**. Support **Use Selection**.
3. Click **Trace Scene Links** to inspect the hierarchy inventory and boundary connections without changing anything.
4. Add the replacement hierarchy to the scene and assign **New Root**.
5. Click **Build Replacement Preview**. Review suggested mappings and proposed incoming edits.
6. Resolve renamed, moved, missing, and ambiguous objects through manual mappings.
7. Review outgoing scene connections separately and opt into any supported transfers to new owners.
8. Select edits, inspect blockers, and click **Apply Selected Changes**.
9. Read the changed/skipped/failed counts, then rescan for remaining references to the old hierarchy.
10. Retire the old hierarchy manually, validate behavior, and save the scene deliberately.

Tracing, mapping, and preview generation must leave scene content unchanged. Applying changes must be a distinct, reviewable action.

## Trace and inventory

Inventory every GameObject and component in the old hierarchy, including components with no detected external links. Show missing scripts as warnings without attempting to repair them.

Scan serialized object references through Unity Editor APIs, including non-visible serialized properties where supported. Do not restrict traversal to Inspector-visible properties. Exclude Unity structural/ownership fields from writable results; hierarchy ownership and prefab metadata are not ordinary application references.

Handle arrays, lists, nested serialized data, and serialized managed-reference contents where Unity exposes their fields. Do not use broad runtime reflection as a substitute for Unity serialization.

Group results into:

- **Incoming:** surrounding scene content pointing into the old hierarchy.
- **Outgoing:** old hierarchy pointing to surrounding scene content.
- **Replacement to Old:** references from the new hierarchy back into the old hierarchy.
- **Internal:** available for inspection, collapsed by default.
- **Assets:** optional read-only context, excluded from scene replacement by default.
- **Warnings:** unreadable objects, unsupported bindings, missing scripts, and incomplete scans.

For each connection show scene, owner hierarchy path, owner component type, serialized property path or binding key, current target, and connection category. UnityEvent rows should additionally show method name and call state when available. Provide Select/Ping for owner and target, text filtering, counts, and an exportable Markdown report.

Name paths are display and matching aids, not object identities. Escape or disambiguate path segments for display so duplicate names and names containing separators are not misleading.

## Hierarchy correspondence

### Automatic suggestions

1. Pair old and new roots explicitly; root names may differ.
2. Match descendants by an exact sequence of relative name segments under their respective roots.
3. Suggest a descendant match only when the path is unique in both hierarchies. A duplicate-name sibling anywhere along the path makes it ambiguous.
4. Match components by exact runtime type on a matched GameObject.
5. Automatically suggest a component pair only when that type occurs exactly once on each matched object.
6. Treat Transform and RectTransform as distinct types for automatic suggestions.

Do not choose by sibling order, first occurrence, approximate name, or component index when multiple candidates exist. A deterministic guess can still wire the wrong object.

### Manual review

Allow manual mappings for renamed/moved descendants and repeated components. Validate each chosen mapping against object kind, component compatibility, hierarchy membership, and the receiving property's supported type. Reject mappings back into the old hierarchy.

Show statuses such as **Suggested**, **Manually Assigned**, **Missing**, **Ambiguous**, **Incompatible**, and **Ignored**. Suggested mappings become eligible edits in the preview; mapping alone does not mutate anything.

Require explicit review if several old objects map to one new object. Changed component types must use manual mappings and remain blocked where their fields or event methods are incompatible.

## Incoming reference replacement

For an outside owner referencing an old target, propose replacing that target with its mapped new target. Preserve the owner, field, array position, and unrelated serialized values.

Example:

```text
KitchenButton / Button
  m_OnClick...m_Target
  OldSystem/Journal / JournalController
    -> NewSystem/Journal / JournalController
```

Apply only selected, compatible edits. An unmapped target stays unchanged and appears as unresolved. Do not clear unresolved fields to null.

Treat references from the replacement hierarchy into the old hierarchy as a separate review group. They may be intentional during preparation, but must remain visible in the final audit.

## Outgoing reference transfer

Replacing incoming references does not preserve scene-specific wiring stored on the old system itself.

Example: an old manager's `InteractableItems` list references the authored scene's interactables. A newly supplied manager may have an empty or different list.

Support a separate, opt-in **Transfer Outgoing Scene Links** operation:

- Map the old owner component to its new owner component.
- Find the corresponding destination serialized property.
- Preview the destination's current value, the old scene target, and the proposed assignment.
- Transfer only a compatible, existing field that the user selects.
- Treat populated destination values as conflicts requiring explicit selection.
- Do not copy whole components or overwrite unrelated settings from the new system.

The first transfer implementation should support simple object-reference fields and existing compatible collection slots. A destination slot at the same index is not proof that it represents the same logical item; collection transfers require explicit review. Changed collection sizes/order, absent fields, renamed fields, and changed serialized layouts remain unresolved.

### Events require structural migration

An old UnityEvent may contain scene-specific callbacks absent from the new event. Replacing target fields alone cannot recreate those callbacks.

Initially, trace these callbacks and report them as **Manual Transfer Required**. Do not infer listener correspondence by array index or transplant individual target/argument fields into unrelated destination listeners.

A later explicit event-transfer feature may copy or merge listener records. It must preview target, method, invocation mode, arguments, call state, order, duplicates, and conflicts, and remap any copied internal targets to their new counterparts. This is separate from ordinary incoming event-target replacement.

## UnityEvents and Timeline

### UnityEvents

Incoming persistent listener targets and object arguments are candidates when exposed as serialized references. Preserve method names, arguments, listener order, invocation modes, and call state.

Before applying a target change, validate that the destination still supports the persisted method and relevant signature. A non-null replacement does not prove that a callback is valid. If validation cannot establish compatibility, flag the listener for review and block automatic application.

Runtime-added listeners and nonserialized delegates are outside this edit-time trace.

### PlayableDirector and Timeline

Retain explicit inspection of generic bindings through the existing director workflow. Represent these as typed binding edits rather than depending only on native serialized field layouts.

- List track name, binding key, owner director, current target, and proposed target.
- Validate output binding compatibility.
- Deduplicate bindings exposed through both serialized inspection and explicit director APIs.
- Report scene-held exposed-reference entries where they can be enumerated and resolved reliably; otherwise disclose their unsupported coverage.
- Keep shared Timeline assets and clip/track assets read-only.
- Do not automatically add a SignalReceiver or any other missing component during replacement.

Initial outgoing transfers may support generic bindings when corresponding new director/output binding keys can be established unambiguously. Missing tracks, different playable assets, and unresolved exposed-reference keys require manual review.

## Preview, stale state, and application

The preview is an explicit change list, not just a count. Each row contains:

| Field | Purpose |
| --- | --- |
| Selected | Whether this particular edit is included. |
| Owner and location | Component/property path or supported binding key that will change. |
| Expected current value | Detects edits made after the preview was created. |
| Proposed value | Concrete replacement object or component. |
| Mapping reason | Exact unique match or manual assignment. |
| Status | Ready, conflict, stale, unsupported, or blocked, with a reason. |

Show total discovered, eligible, selected, unresolved, and blocked counts. Filters affect display only; clearly state whether bulk selection affects filtered rows or the whole plan.

Invalidate the preview when roots, mappings, scope, hierarchy, or relevant serialized state change. Subscribe/unsubscribe to appropriate Editor notifications and Undo/Redo. Immediately before applying, revalidate every selected row against live owners, properties, bindings, expected values, mapping membership, and compatibility.

If any selected row fails preflight, change nothing and require a refreshed preview or deselection of that row. Use one named Undo group for a successful apply, record prefab-instance modifications where required, and mark affected scenes dirty. On an unexpected apply failure, roll back the tool's Undo group and report the error; do not announce partial success as completion.

Do not auto-save scenes or assets. Post-apply results must distinguish actual changes from skips, failures, and rollback. Run a fresh trace to show remaining links into the old hierarchy within the chosen scope.

## Implementation shape

Keep this inside the existing editor-only Utility package and reuse its namespace, menu priorities, and visual conventions. No TellVR dependency or new external package is needed.

Suggested responsibilities, with final filenames determined during implementation:

- Existing `FPSceneReferenceReplaceEditorWindow`: settings, inventory, filters, mappings, preview selection, actions, and status.
- Focused scanner/planner code: hierarchy membership, mapping suggestions, boundary connections, and proposed edits.
- Focused apply code: preflight, supported property/binding operations, Undo, prefab overrides, and result reporting.

Separate scan/plan/apply behavior sufficiently for Edit Mode tests without building a generic migration framework. Reuse existing replacement logic only after checking its traversal, scope, and mutation behavior against this proposal.

Use live Unity object references in an in-memory plan. Do not introduce `GetInstanceID()` keys. Persisted reports/mapping presets are a later feature and must use the repository's established stable-identity conventions; object names alone are insufficient. Clear or rebuild transient plans after domain reload rather than applying stale handles.

Inspect and reuse `FP_UtilityData` and shared editor helpers for established menu/hierarchy/UI conventions. Domain-specific relative matching rules belong in the scanner, not duplicated inside UI rendering. Let Unity generate metadata.

## Delivery phases

1. **Read-only trace:** hierarchy inventory, incoming/outgoing groups, inactive content, serialized field paths, explicit Timeline binding coverage, warnings, and report export.
2. **Incoming replacement:** unique mapping suggestions, manual overrides, per-edit preview, event/binding compatibility checks, atomic preflight, Undo, and post-apply audit. Preserve simple pair mode.
3. **Outgoing transfer:** compatible scalar fields and reviewed existing collection slots; binding transfers where correspondence is reliable. Report structural/event differences without guessing.
4. **Optional extensions:** explicit event/list migration, saved mappings, broader multi-scene migration, and project-specific adapters only after concrete use cases justify them.

The first usable replacement milestone is phase 2. Do not describe it as preserving all authored system configuration until the supported outgoing transfers and outstanding manual work have been reviewed.

## Acceptance and validation

Use temporary test scenes and objects, independent of TellVR content. Validate through Unity compilation and the Unity Test Framework.

| Scenario | Required result |
| --- | --- |
| Outside field points to an old child component | Trace includes exact owner/property/target; selected apply points to mapped new component. |
| Owner or target is inactive | Included by default. |
| Old hierarchy has internal references | Visible on request; excluded from external replacement by default. |
| Duplicate sibling names or repeated component types | Ambiguous; no silent first-match selection. |
| Renamed or moved child | Unresolved until explicitly mapped. |
| Missing replacement component | Blocked; no component automatically added. |
| Hidden serialized references and nested fields | Discovered where supported by Unity serialization; unsupported traversal is disclosed. |
| Incoming UnityEvent target/object argument | Target compatibility checked; other listener data preserved. |
| Event method absent on replacement | Blocked with a clear diagnostic. |
| Old event contains outgoing scene listeners | Reported; no accidental listener-index transfer. |
| Timeline generic binding targets an old descendant | Appears once in preview; selected apply uses the mapped compatible target. |
| New hierarchy still points into old hierarchy | Separate warning group remains visible after surrounding-scene swaps. |
| Old manager holds scene-object lists | Outgoing report includes them; incompatible destination layouts remain unresolved. |
| Scene/reference changes after preview | Apply is blocked before any mutation. |
| Prefab instance owner is changed | Override survives save/reload; Undo/Redo restores the expected references. |
| Apply fails unexpectedly | Tool changes are rolled back; result reports failure. |
| Another loaded scene is outside selected scope | Untouched; final audit states the excluded scope. |
| Scan or preview is cancelled/incomplete | No mutation; result cannot be presented as complete. |
| Trace/build preview only | No scene dirtying, hierarchy edits, or asset writes. |

Add interactive checks for selection/pinging, long result lists, filters, domain reload, Undo/Redo, and readable warnings. Verify scene save/reload separately from the in-memory replacement test.

## Limits and retirement criteria

The tool traces supported serialized scene references and explicit supported bindings. It cannot establish all runtime relationships. String/name lookups, static state, runtime subscriptions, custom registries, code-created references, and unloaded scenes may still depend on the old system. Asset-backed graphs/configuration are contextual dependencies, not part of a scene-only rewrite.

An audit with zero remaining supported references means only that none were found in the completed selected scope. Before deleting the old hierarchy, review unresolved/unsupported results, outgoing scene data, asset configuration, and runtime initialization. Exercise the scene's interactions, events, journal/UI, audio, and Timeline behavior relevant to that project.

`TellVR_System` is an acceptance example for the general workflow, not a special case in its implementation.
