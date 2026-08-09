---
name: fuzzphyte-unity-performance
description: Design, implement, review, and optimize performance-focused architectural solutions for FuzzPhyte Unity C# repositories. Use for Unity systems that may benefit from data-oriented design, the C# Job System, Burst, ECS/DOTS, compute shaders, GPU instancing, or other Unity-supported parallel workflows; for refactors intended to reduce allocations, main-thread work, or package dependencies; and for architecture plans that must consult repository research, remain aligned with FuzzPhyte conventions, update documentation, and include Unity Test Framework coverage.
---

# FuzzPhyte Unity Performance Architecture

Build Unity architecture that is measurable, concise, testable, package-safe, and consistent with existing FuzzPhyte libraries.

Treat the repository's general FuzzPhyte conventions as the baseline. Apply this skill as the stricter workflow for performance-sensitive architecture. Preserve existing `FuzzPhyte.*`, `FP_`, `IFP*`, `FP_Data`, event, editor-tooling, and composition conventions unless the repository explicitly defines otherwise.

## Non-Negotiable Rules

- Measure before optimizing. Do not claim a performance improvement without evidence or a clearly labeled hypothesis.
- Prefer the simplest architecture that meets the performance budget.
- Keep domain logic independent of `MonoBehaviour`; use Unity components as adapters, bindings, authoring components, or visualizers.
- Keep generated C# concise. Avoid speculative abstractions, redundant wrappers, unnecessary managers, and one-use interfaces.
- Avoid external dependencies by default.
- Use only:
  - Unity APIs included with the target Editor version.
  - Unity packages already declared in `Packages/manifest.json` or explicitly approved for addition.
  - Existing FuzzPhyte or project-owned Unity packages.
  - Standard C#/.NET APIs supported by the project's Unity scripting profile.
- Do not introduce NuGet-only libraries, native plugins, unmanaged DLLs, external runtimes, or new interoperability layers without explicit approval.
- Add or update Unity Test Framework tests with production code.
- Update relevant documentation in the same change.
- Keep a visible implementation plan and reconcile completed work against it throughout the task.

## Repository Context First

Before proposing architecture or editing code, inspect the relevant repository context:

1. Read repository instructions, existing skill files, `README` files, package documentation, assembly definitions, and nearby implementations.
2. Inspect `Packages/manifest.json` and package lock data before selecting APIs or packages.
3. Identify the Unity Editor version and installed versions of Entities, Burst, Collections, Mathematics, Test Framework, and other relevant packages.
4. Search for existing FuzzPhyte abstractions before creating new types.
5. Inspect `<repository-root>/Research/` for academic papers, technical reports, theses, standards, benchmarks, and design notes relevant to the requested system.

### Research Folder Rules

- Treat `Research/` as evidence and design context, not as unquestionable implementation requirements.
- Read only documents relevant to the task.
- Extract the algorithm, assumptions, constraints, inputs, outputs, complexity, and validation method.
- Check whether the research assumes hardware, languages, libraries, precision, data sizes, or concurrency models that differ from Unity.
- Translate research concepts into Unity-supported C# architecture rather than copying implementation dependencies.
- Cite research in architecture documentation using repository-relative paths and page, section, figure, or table references when available.
- Distinguish direct findings from engineering inference.
- State when `Research/` is absent or contains no relevant evidence; do not invent sources.
- Do not redistribute or reproduce large copyrighted passages from research documents.

## Performance Decision Order

Choose the lowest-complexity option that satisfies measured requirements:

1. **Concise managed C#**
   - Improve algorithms, data access, caching, batching, pooling, and allocation behavior first.
   - Remove unnecessary per-frame work and repeated Unity API calls.

2. **Burst-compatible jobs**
   - Use the C# Job System and Burst for CPU-bound work that can operate on independent or partitioned data.
   - Prefer blittable structs, `NativeArray<T>`, Unity Collections, explicit ownership, and explicit job dependencies.
   - Avoid managed references, hidden synchronization, and immediate `Complete()` calls that erase parallelism.

3. **ECS/DOTS**
   - Use ECS when entity scale, homogeneous data, lifecycle churn, query patterns, or parallel system execution justify the conversion cost.
   - Keep components as unmanaged data.
   - Keep behavior in systems and jobs.
   - Prefer `ISystem`, Burst-compatible code, explicit update ordering, and data layouts that minimize structural changes.
   - Do not select ECS merely because a feature is performance-sensitive.

4. **GPU workflows**
   - Use compute shaders, `GraphicsBuffer`, indirect drawing, GPU instancing, or other Unity-supported GPU APIs for highly parallel workloads with sufficient data volume.
   - Minimize CPU-GPU synchronization and readback.
   - Define buffer ownership, stride, lifetime, dispatch sizing, platform capability, fallback behavior, and cleanup.
   - Keep authoritative gameplay logic on the CPU unless nondeterminism, latency, and readback costs are acceptable.

5. **Hybrid architecture**
   - Use managed orchestration, Burst jobs, ECS, and GPU processing together only when boundaries and data transfer costs are explicit.
   - Keep conversion points few, visible, and tested.

Reject an advanced approach when profiling evidence, workload size, target hardware, maintainability, determinism, or package constraints do not justify it.

## Required Workflow

### 1. Define the Performance Contract

Record or propose:

- Target Unity version and platforms.
- Workload size and expected growth.
- Frame-time, memory, allocation, throughput, latency, or startup budget.
- Determinism and precision requirements.
- Current baseline and known bottleneck.
- Supported packages and prohibited dependencies.

When the user has not supplied a budget, state reasonable assumptions and mark them for confirmation.

### 2. Build the Plan

Before implementation, provide a concise plan containing:

- Current architecture summary.
- Relevant research findings.
- Measured or suspected bottleneck.
- Options considered.
- Chosen approach and why it is the least-complex sufficient option.
- Data flow and ownership.
- Thread, job, entity, or GPU boundaries.
- Package and assembly-definition impact.
- Files to add or modify.
- UTF test strategy.
- Profiling and acceptance criteria.
- Documentation to update.

Ask for plan confirmation before substantial implementation when interactive review is expected. When the user requested autonomous execution, proceed with explicit assumptions rather than blocking.

### 3. Implement a Thin Vertical Slice

- Implement the smallest end-to-end path that validates the architecture.
- Preserve public APIs unless a breaking change is required and documented.
- Keep types and methods focused but do not fragment straightforward logic into excessive files or layers.
- Prefer descriptive names over comments.
- Comment only on non-obvious constraints, ownership, synchronization, or research-derived behavior.
- Dispose native allocations deterministically.
- Avoid per-frame allocations, LINQ in hot paths, reflection, boxing, string formatting, and repeated component lookups in measured hot code.
- Keep Editor-only validation and authoring code out of player assemblies.

### 4. Confirm Against the Plan

After each meaningful implementation phase:

- Mark plan items complete, changed, deferred, or blocked.
- Compare the actual package, API, file, and data-flow changes to the plan.
- Document deviations and their performance or maintenance impact.
- Request approval only when a deviation changes public APIs, dependencies, architecture, research interpretation, target support, or acceptance criteria.

Do not silently drift from the agreed architecture.

### 5. Test with Unity Test Framework

Use the Unity Test Framework version installed in the repository. Follow the package documentation for that version.

- Create dedicated test assemblies with `.asmdef` files and explicit references.
- Use NUnit-style Arrange, Act, Assert tests for deterministic logic.
- Use Edit Mode tests for plain C# logic, data transformations, validation, jobs that can complete deterministically, and authoring utilities.
- Use Play Mode tests for scene integration, player-loop behavior, GameObject or ECS runtime integration, asynchronous behavior, and platform-facing APIs.
- Use `[UnityTest]` only when a test must span frames or yield.
- Test normal cases, boundaries, invalid input, disposal, cancellation or teardown, and deterministic behavior where applicable.
- For jobs, test scheduling dependencies, result correctness, and native-container lifetime.
- For ECS, create isolated test worlds and clean them up deterministically.
- For GPU workflows, separate CPU-side contract tests from platform-dependent integration tests and provide a supported fallback or skip condition.
- Keep tests concise and behavior-focused; do not duplicate implementation details in assertions.
- Do not rely on UTF features unsupported by the installed package version.

Use Unity's Performance Testing package only when it is already installed or explicitly approved. Otherwise, record repeatable profiling steps and acceptance thresholds in documentation.

Reference: [Unity Test Framework manual](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/index.html).

### 6. Profile and Validate

Select tools appropriate to the architecture:

- Unity Profiler and Profile Analyzer for CPU and frame behavior.
- Memory Profiler and allocation call stacks for memory behavior.
- Burst Inspector for Burst compilation and generated code checks.
- Entities Hierarchy, Systems, and Journaling tools for ECS behavior.
- Frame Debugger, RenderDoc where supported, and platform GPU profilers for rendering or compute work.

Record:

- Test scene or dataset.
- Hardware and build target.
- Editor or player build type.
- Baseline measurement.
- New measurement.
- Variance or sample count.
- Whether the acceptance criteria passed.

Never use Editor-only timing as the sole proof of player performance.

### 7. Update Documentation

Update the closest relevant `README`, architecture note, package documentation, changelog, or XML documentation with:

- Purpose and supported use cases.
- Architecture and data flow.
- Required Unity and package versions.
- Setup and usage.
- Ownership and disposal rules.
- Threading, ECS, or GPU constraints.
- Research basis and repository-relative citations.
- Test locations and execution instructions.
- Profiling procedure, baseline, result, and acceptance criteria.
- Known limitations and fallback behavior.
- Breaking changes and migration steps.

Documentation must describe the implemented system, not the planned system.

## Concise Code Standard

Apply these rules to generated code:

- Return the smallest complete implementation that satisfies the requirement.
- Reuse existing project types before adding new abstractions.
- Avoid `Manager`, `Helper`, `Utility`, `Base`, or `Service` types unless the responsibility is precise and established in the repository.
- Avoid pass-through methods and interfaces with one implementation unless they protect a meaningful boundary.
- Prefer immutable inputs, explicit outputs, and narrow public APIs.
- Keep configuration in data assets and logic in testable C# systems.
- Use early returns to reduce nesting.
- Use `readonly`, `in`, `ref`, spans, native containers, and structs only when supported and materially useful.
- Do not compress code into clever expressions that reduce readability or debuggability.
- Include `using` directives, namespaces, disposal, validation, and error paths needed for production use.

## Dependency Gate

Before adding a package or dependency, report:

- Exact package name and version.
- Whether it is already present.
- Why built-in Unity or existing FuzzPhyte code is insufficient.
- Runtime, Editor, build-size, licensing, maintenance, and platform impact.
- Removal or fallback strategy.

Do not modify `Packages/manifest.json` without explicit approval unless the user already approved the package in the task.

## Architecture Output Contract

For architecture and implementation tasks, provide:

1. **Plan status** — confirmed assumptions, completed items, and deviations.
2. **Decision** — selected architecture and rejected alternatives.
3. **Research basis** — relevant `Research/` sources and engineering interpretation.
4. **Dependency impact** — packages added, reused, or avoided.
5. **Implementation** — concise production code and changed files.
6. **Tests** — UTF coverage and how to run it.
7. **Documentation** — files updated and key operational guidance.
8. **Validation** — measurements, acceptance result, and remaining risks.

Do not claim completion when tests, documentation, or performance validation are missing. State exactly what was not verified.

## Final Review Checklist

- [ ] Repository and package context inspected.
- [ ] Relevant `Research/` documents reviewed and cited.
- [ ] Performance contract defined.
- [ ] Simplest sufficient architecture selected.
- [ ] No unapproved external dependency introduced.
- [ ] Public API and assembly impacts documented.
- [ ] Native, entity, job, and GPU lifetimes are explicit.
- [ ] Code is concise and consistent with FuzzPhyte conventions.
- [ ] Edit Mode and Play Mode coverage is appropriate.
- [ ] UTF tests pass or failures are reported.
- [ ] Documentation matches the implementation.
- [ ] Baseline and post-change measurements are recorded.
- [ ] Final work is reconciled against the plan.
