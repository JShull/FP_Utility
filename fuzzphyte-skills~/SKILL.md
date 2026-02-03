---
name: fuzzphyte-skills
description: Architectural conventions, patterns, and coding standards for FuzzPhyte FP_ Unity libraries
compatibility: Unity Editor (6000.0). Intended for Unity Package Manager libraries written in C#, using ScriptableObjects, custom Editor tooling, and optional ECS patterns.
license: Dual-licensed. Educational use under the MIT License; business/commercial use under the GNU General Public License v3.0. See the repository root README and LICENSE file for full terms.
metadata:
  author: FuzzPhyte
  owner: John Shull
  version: "1.0.0"
  scope: unity-library
  intent: enforce-consistency
---

# FuzzPhyte Unity Library Skills

This document defines the **core architectural philosophy, coding conventions, and system patterns** used across all `FP_` Unity libraries under the `FuzzPhyte.*` namespace.

These rules are **intentional**, **opinionated**, and designed to support:

- Reusable Unity packages
- Scalable systems
- Tooling-friendly workflows
- AI-assisted development without architectural drift

AI agents should treat this file as **authoritative guidance** when generating or modifying code.

---

## 1. Namespaces & Package Structure

### Namespace Rules

- All libraries **must** live under the `FuzzPhyte.*` root namespace.
- Sub-namespaces reflect *system responsibility*, not Unity folder layout.

**Examples:**

- `FuzzPhyte.Utility`
- `FuzzPhyte.SystemEvent`
- `FuzzPhyte.Network`
- `FuzzPhyte.FiniteStateMachine`
- `FuzzPhyte.Controller`
- `FuzzPhyte.Connections`

**Avoid:**

- Generic namespaces like `Core`, `Common`, `Helpers`
- Project-specific namespaces inside reusable libraries

---

## 2. ScriptableObjects & Data Architecture

### `FP_Data` Is the Base for All Data Assets

- All ScriptableObjects that represent **data** must inherit from `FP_Data`.
- `FP_Data` establishes:
  - Identity
  - Versioning
  - Consistent editor handling
  - System-wide interoperability

**Intent example:**

```csharp
public abstract class FP_Data : ScriptableObject
{
    // Base identity / lifecycle handled here
}
```

### Rules

#### ScriptableObjects

- ScriptableObjects are **configuration + data**, not logic containers.
- No scene dependencies.
- Avoid hard references to scene objects.
- Prefer storing **IDs / keys** rather than direct object references when long-lived.

---

## 3. MonoBehaviours vs Libraries

### Core Libraries

- Core libraries **must not** depend on MonoBehaviours.
- No scene-bound assumptions.
- Must be usable and testable in isolation.

Core library code should aim for:

- Plain C# types
- Deterministic behavior
- Minimal `UnityEngine` dependencies  
  (ideally none beyond `ScriptableObject` data types, when required)

### Unity Components

MonoBehaviours act as:

- Adapters
- Bindings
- Visualizers
- Runtime hooks

**They do not own domain logic.**  
Logic should live in reusable systems or services that can be called from components.

---

## 4. Events, Actions, and Binding Philosophy

FuzzPhyte systems prefer **decoupled communication**.

### Preferred Order

1. **Actions / Delegates**
   - Lightweight
   - Explicit ownership
   - Easy to test
   - Great for runtime-only wiring

2. **FP Event System**
   - Used when:
     - Cross-system communication is needed
     - Editor-driven configuration is beneficial
     - Events need to be observable or standardized

3. **Listeners / Binders**
   - Components that bind runtime behavior to data or events
   - Usually MonoBehaviours
   - Keep systems loosely coupled

### Guiding Intent

> Systems should not know who is listening — only that something may respond.

### Terminology

- **Event**: a broadcasted signal (often `FPEvent`-based)
- **Listener**: something that reacts to the event
- **Binder**: a Unity-facing component that bridges scene objects to core logic/events

---

## 5. Interfaces & Naming Rules

### Interfaces

- Interfaces **must not** start with a plain `I`.
- Use the `IFP` prefix instead which aligns to c# requirements

**Valid:**

- `IFPSingleton`
- `IFPStyleReceiver`

**Invalid:**

- `ISingleton`
- `IManager`

This avoids:

- Ambiguity across repos and packages
- Conflicts with external libraries
- AI-generated code drifting toward generic patterns

---

## 6. Static Classes & Singletons

### Static Classes

Allowed when they are:

- Stateless
- Deterministic
- Utility-focused

**Good candidates:**

- Math helpers
- Conversion utilities
- Constants
- Lightweight data lookups
- Small, pure helper methods

**Avoid:**

- Hidden mutable state
- Runtime caches without explicit lifecycle controls

### Singletons

- Explicitly defined via `IFPSingleton`  
  (or an FP-consistent singleton pattern)
- Initialization must be intentional and visible
- Prefer dependency injection or explicit service registration when feasible

**Rule of thumb:**

- If it can be passed as a dependency, pass it.
- If it must be global, document why.

---

## 7. Editor Scripts Are First-Class Citizens

Editor tooling is a core part of the FuzzPhyte approach.

### Philosophy

> If a system is complex, it deserves editor tooling.

### Editor Scripts Are Used To

- Reduce runtime complexity
- Prevent configuration errors
- Improve developer experience
- Visualize abstract systems
- Provide validation, automated setup, and repair tooling

### Common Examples

- Custom inspectors for `FP_Data` derivatives
- Editor windows for:
  - Generating ScriptableObjects
  - Managing keys / IDs
  - Building graphs or state machines
- Validation tools
- Gizmo and Handles visualization helpers

**Note:** Editor code may be opinionated and verbose — that is a feature.

---

## 8. Composition Over Inheritance

- Favor composition and data-driven configuration.
- Inheritance is reserved for:
  - Clear “is-a” relationships
  - Framework-level abstractions  
    (e.g., `FP_Data`, core event base types)

Avoid deep inheritance trees.

---

## 9. ECS / DOTS (When Applicable)

When ECS is used:

- Components are **pure data only** and there's a need for a lot of them in a runtime environment.
- No logic in components.
- Avoid managed references unless explicitly justified.

---

## 10. FP Naming Conventions

### Prefix Usage

- The `FP_` prefix is used for:
  - Core system types
  - Shared patterns and base classes
  - Library-level features intended to travel across repos

### Consistency Rules

- Match existing naming patterns within the library.
- Avoid introducing new prefixes unless there is a strong reason.
- Prefer clarity over abbreviations.

---

## 11. AI Expectations

AI agents working in this repository should:

- Follow these conventions by default.
- Ask before introducing:
  - New patterns
  - New architectural layers
  - New external dependencies
- Prefer consistency over novelty.
- Mirror existing `FP_` conventions rather than inventing alternatives.

**When in doubt:**

> Implement the simplest solution that matches existing FP patterns.

---

## 12. Guiding Principle

FuzzPhyte libraries are designed to be:

- Modular
- Composable
- Tool-friendly
- Long-lived
