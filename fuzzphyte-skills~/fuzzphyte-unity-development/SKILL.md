---
name: fuzzphyte-unity-development
description: Build, modify, review, and document reusable Unity C# libraries that follow FuzzPhyte architecture and naming conventions. Use for general FP_ package development, refactors, ScriptableObject data systems, runtime services, MonoBehaviour adapters, editor tooling, events, interfaces, package structure, documentation, and Unity Test Framework coverage. Preserve existing repository patterns, keep code concise, minimize dependencies, maintain an agreed implementation plan, and leave Unity-generated metadata, project files, refresh, compilation, and asset importing to the Unity Editor.
---

# FuzzPhyte Unity Development

Build reusable Unity libraries under the `FuzzPhyte.*` namespace without introducing architectural drift, unnecessary dependencies, or editor-generated artifacts.

Treat the repository as the source of truth. Match its established patterns before introducing new ones.

## Required Workflow

### 1. Inspect Before Editing

- Read the repository structure, nearby code, assembly definitions, package metadata, tests, and documentation.
- Check `git status` before changing files when repository access is available.
- Identify the existing namespace, naming, data, event, editor, and test patterns.
- Reuse existing FuzzPhyte packages and abstractions instead of recreating equivalent systems.
- Do not infer that a repository compiles merely because the source looks valid.

### 2. Establish and Maintain a Plan

- State a concise implementation plan before making substantial changes.
- Include intended architecture, files, tests, documentation, and dependency impact.
- Keep work aligned with the agreed plan.
- Update the plan when repository evidence requires a change.
- Explain material deviations instead of silently changing direction.
- Confirm before introducing a new architectural layer, package dependency, or public API expansion.

### 3. Implement the Smallest Cohesive Change

- Prefer focused edits over broad rewrites.
- Preserve public APIs unless the task explicitly requires a breaking change.
- Avoid unrelated formatting, renaming, or cleanup.
- Keep runtime logic deterministic and independently testable where practical.
- Add only the abstractions required by the current system.

### 4. Update Tests and Documentation

- Add or update Unity Test Framework tests for meaningful behavior changes.
- Update package documentation, examples, API notes, and changelogs when affected.
- Keep code, tests, and documentation consistent with one another.

### 5. Hand Work Back to Unity

- Stop after source, tests, and documentation are updated.
- Let the Unity Editor refresh assets, generate metadata, regenerate IDE project files, and compile assemblies.
- Report what Unity still needs to validate.
- Do not claim compilation or test success unless Unity actually performed that validation and the result was observed.

## Unity Owns Generated Artifacts

Do not manually create, regenerate, rewrite, or guess Unity- or IDE-generated files.

### Never Generate or Repair by Hand

- `*.meta`
- `*.csproj`
- `*.sln`
- `*.csproj.user`
- `.vs/`
- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- generated IDE workspace or solution files

### Required Behavior

- Do not invent Unity GUIDs.
- Do not create a `.meta` file for a new or moved asset.
- Do not copy a `.meta` file to represent a different asset.
- Do not regenerate Visual Studio or Rider project files.
- Do not run `dotnet build`, `msbuild`, IDE build commands, or project-generation scripts as a substitute for Unity compilation.
- Do not launch Unity in batch mode or trigger a forced reimport unless the user explicitly requests that workflow.
- Let the open Unity Editor detect changes, refresh, generate missing `.meta` files, regenerate project files, import assets, and compile.

Unity-generated `.meta` files should normally remain version-controlled after Unity creates them. The agent may include or review Unity-created metadata after refresh, but must not author or guess it.

Treat `Packages/manifest.json`, `Packages/packages-lock.json`, `.asmdef` files, `ProjectSettings/`, and package source files as intentional project configuration rather than disposable generated output. Modify them only when required by the approved plan.

## Namespaces and Package Structure

- Place reusable libraries under the `FuzzPhyte.*` root namespace.
- Organize sub-namespaces by system responsibility rather than folder layout.
- Avoid generic roots such as `Core`, `Common`, or `Helpers`.
- Avoid project-specific names inside reusable packages.
- Follow the package's existing `Runtime`, `Editor`, `Tests`, `Samples~`, and documentation structure.
- Respect assembly boundaries and keep editor-only code out of runtime assemblies.

Examples:

- `FuzzPhyte.Utility`
- `FuzzPhyte.SystemEvent`
- `FuzzPhyte.Network`
- `FuzzPhyte.FiniteStateMachine`
- `FuzzPhyte.Controller`
- `FuzzPhyte.Connections`

## ScriptableObject and Data Architecture

Use `FP_Data` as the base for ScriptableObjects that represent reusable FuzzPhyte data assets when that base exists in the repository or required package.

- Keep ScriptableObjects focused on configuration and persistent data.
- Keep domain logic in reusable services or systems.
- Avoid scene dependencies and hard references to scene objects.
- Prefer stable identifiers or keys over long-lived scene references.
- Do not duplicate identity, versioning, or editor behavior already supplied by `FP_Data`.

```csharp
public abstract class FP_Data : ScriptableObject
{
    // Shared identity and lifecycle behavior belongs in the established base.
}
```

## Core Libraries and Unity Adapters

Keep core logic independent of scene lifecycle wherever practical.

### Core Code

Prefer:

- Plain C# types
- Deterministic behavior
- Explicit dependencies
- Small public surfaces
- Minimal `UnityEngine` coupling
- Isolated tests

### MonoBehaviours

Use MonoBehaviours as:

- Adapters
- Binders
- Visualizers
- Scene-facing controllers
- Unity lifecycle hooks

Do not make a MonoBehaviour the default owner of reusable domain logic.

## Events, Actions, and Bindings

Prefer decoupled communication in this order unless repository patterns require otherwise:

1. Actions or delegates for lightweight runtime wiring
2. Existing FP event systems for standardized or editor-configurable communication
3. Listener or binder components for Unity-facing reactions

Use these terms consistently:

- **Event**: a broadcast signal
- **Listener**: a receiver that reacts to an event
- **Binder**: a Unity-facing component that connects scene objects to reusable logic or events

Systems should publish intent without depending on specific listeners.

## Interfaces and Naming

- Prefix FuzzPhyte interfaces with `IFP`, not a plain `I`.
- Match established `FP_` naming before creating a new convention.
- Use `FP_` for shared framework types, base classes, and reusable library features when consistent with the package.
- Prefer complete, descriptive names over unclear abbreviations.
- Preserve existing serialized field names unless a migration is planned.

Valid examples:

- `IFPSingleton`
- `IFPStyleReceiver`

Avoid:

- `ISingleton`
- `IManager`

## Static Types and Global Access

Use static classes only for stateless, deterministic behavior such as:

- Math helpers
- Conversion methods
- Constants
- Small pure utilities
- Read-only lookups

Avoid hidden mutable state and unmanaged global caches.

Use singletons only through an established FuzzPhyte pattern such as `IFPSingleton`.

- Make initialization and shutdown explicit.
- Prefer passing dependencies when practical.
- Document why global access is required.

## Editor Tooling

Treat editor tooling as a first-class part of complex systems.

Use editor code to:

- Validate configuration
- Generate or repair author-controlled assets through Unity APIs
- Reduce repetitive setup
- Visualize data and relationships
- Improve package usability

Keep editor code in editor-only folders and assemblies.

Editor tools may create Unity assets through supported Unity APIs while running inside the Editor. External agents must still leave `.meta` creation and asset import to Unity.

## Composition and Extensibility

- Prefer composition and data-driven configuration.
- Reserve inheritance for clear is-a relationships and framework-level bases.
- Avoid deep inheritance trees.
- Avoid speculative extension points.
- Keep APIs extensible only where an actual repository use case requires it.

## Dependencies

- Prefer Unity-supported APIs and packages already installed in the repository.
- Prefer existing FuzzPhyte packages over new third-party libraries.
- Do not add external libraries, copied source dependencies, or new Unity packages without explicit approval.
- Check license, platform, assembly, and package compatibility before proposing a dependency.
- Avoid adding a dependency for behavior that can be implemented clearly with a small amount of native C# or existing Unity functionality.

## Concise Code Standard

Write code that is direct, readable, and proportionate to the requirement.

- Prefer one clear abstraction over multiple thin wrappers.
- Remove dead branches, unused fields, placeholder APIs, and speculative factories.
- Avoid pass-through methods that add no policy or behavior.
- Avoid comments that merely restate code.
- Comment architectural intent, Unity lifecycle constraints, serialization risks, and non-obvious tradeoffs.
- Keep methods focused without fragmenting simple logic into excessive helpers.
- Use early returns when they improve clarity.
- Do not compress code at the expense of debugging, testability, or Unity serialization safety.

## ECS and Performance-Specific Work

Use ECS, Jobs, Burst, compute shaders, GPU instancing, or other parallel workflows only when justified by workload and repository requirements.

- Keep ECS components data-only.
- Avoid managed references in ECS data unless explicitly justified.
- Do not introduce DOTS or GPU infrastructure into a generic library without evidence and approval.
- Apply the specialized `fuzzphyte-unity-performance` skill alongside this skill when the task is primarily about performance architecture, parallel processing, ECS/DOTS, Burst, Jobs, compute shaders, or GPU workflows.

## Unity Test Framework

Use the Unity Test Framework for library validation.

### Edit Mode Tests

Use Edit Mode tests for:

- Plain C# logic
- Data transformations
- ScriptableObject validation
- Editor tooling
- Deterministic package behavior

### Play Mode Tests

Use Play Mode tests for:

- MonoBehaviour lifecycle behavior
- Scene bindings
- Coroutines and frame-dependent behavior
- Runtime integration
- Unity object destruction and timing

### Test Rules

- Follow Arrange, Act, Assert.
- Test public behavior and important failure paths.
- Add regression tests for corrected defects.
- Avoid tests that depend on unrelated project scenes or assets.
- Dispose native resources and destroy created Unity objects.
- Match the repository's existing test assembly and namespace conventions.
- Do not generate test `.meta` files; let Unity create them after refresh.
- Do not claim tests passed unless they ran in Unity and the results were observed.

## Documentation Expectations

Update documentation when changes affect:

- Public APIs
- Setup or installation
- Dependencies
- Assembly definitions
- Serialized data
- Editor workflows
- Samples
- Known limitations
- Migration steps

Prefer concise Markdown near the package. Update the existing README, changelog, XML documentation, or package documentation rather than creating redundant files.

## Final Review and Handoff

Before finishing:

- Compare implementation against the current plan.
- Review the diff for unrelated changes.
- Confirm no generated `.meta`, `.csproj`, `.sln`, `Library`, `Temp`, `Logs`, or `obj` content was authored.
- Confirm no unapproved dependency was introduced.
- Confirm tests and documentation were added or updated as required.
- Identify files Unity must import or generate metadata for.
- State that the Unity Editor must refresh and compile the changes.
- Report any validation that could not be performed outside Unity.

When uncertain, implement the simplest solution that matches existing FuzzPhyte patterns and leave Unity-owned work to Unity.
