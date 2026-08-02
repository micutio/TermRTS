# TermRTS Developer Documentation

This document gives contributors a practical overview of the TermRTS codebase and the main extension points for building, testing, and extending the engine.

## 1. Project overview

TermRTS is a small simulation engine for terminal-based real-time systems. The core idea is to combine:

- an entity/component model similar to ECS patterns,
- a scheduler-driven simulation loop,
- event-based communication between systems,
- optional persistence and rendering hooks.

The solution is organized into several subprojects:

- [TermRTS](../TermRTS) — the core engine library.
- [TermRTS.Examples](../TermRTS.Examples) — runnable sample applications.
- [TermRTS.Test](../TermRTS.Test) — unit tests for the engine.
- [TermRTS.Examples.Test](../TermRTS.Examples.Test) — tests for the example projects.
- [TermRTS.Benchmark](../TermRTS.Benchmark) — benchmarking harness.

## 2. Architecture at a glance

The engine is centered around a few core abstractions:

- [TermRTS/Entity.cs](../TermRTS/Entity.cs) — entities are lightweight identifiers for components.
- [TermRTS/ComponentBase.cs](../TermRTS/ComponentBase.cs) — base class for all simulation components.
- [TermRTS/ISimSystem.cs](../TermRTS/ISimSystem.cs) — interface for systems that process components.
- [TermRTS/IRenderer.cs](../TermRTS/IRenderer.cs) — rendering interface used by the scheduler.
- [TermRTS/Scheduler.cs](../TermRTS/Scheduler.cs) — main loop, event dispatch, and timing control.
- [TermRTS/Core.cs](../TermRTS/Core.cs) — simulation tick execution, entity/component lifecycle, and buffering.
- [TermRTS/Simulation.cs](../TermRTS/Simulation.cs) — high-level wrapper around the scheduler and persistence.
- [TermRTS/Persistence.cs](../TermRTS/Persistence.cs) — JSON save/load support.

### Main execution flow

1. A [TermRTS/Scheduler.cs](../TermRTS/Scheduler.cs) instance runs the main loop.
2. The scheduler advances the simulation through repeated ticks.
3. The [TermRTS/Core.cs](../TermRTS/Core.cs) instance executes systems and applies component changes.
4. Events emitted during a tick are routed through the scheduler.
5. Rendering is invoked at the end of each simulation step.

## 3. Key design conventions

### Components own behavior and state

Prefer putting simulation state and logic into components rather than systems. The project comments explicitly encourage systems to remain stateless because that makes serialization and reasoning about the engine easier.

### Entities are IDs, not containers

Entities are lightweight and primarily serve as identifiers. Components are what hold data.

### Use double-buffered properties for state changes

[TermRTS/ComponentBase.cs](../TermRTS/ComponentBase.cs) provides support for double-buffered properties. This is useful when you need state updates that should not become visible until the next tick boundary.

### Prefer scheduling additions/removals through the core

New entities and components are queued and applied at the end of the next tick. This keeps simulation updates deterministic and avoids mid-tick mutation hazards.

## 4. How to build the solution

From the repository root, run:

```sh
dotnet build TermRTS.slnx
```

This builds the engine and all referenced projects.

## 5. How to run examples

The example runner is in [TermRTS.Examples/ExampleRunner.cs](../TermRTS.Examples/ExampleRunner.cs).

Run an example with:

```sh
dotnet run --project TermRTS.Examples -- <example number>
```

Available examples:

- 1 = Minimal App
- 2 = Bouncy Ball
- 3 = Circuitry
- 4 = Greenery

Example:

```sh
dotnet run --project TermRTS.Examples -- 1
```

## 6. How to run tests

The test project is [TermRTS.Test/TermRTS.Test.csproj](../TermRTS.Test/TermRTS.Test.csproj).

Run:

```sh
dotnet test TermRTS.Test/TermRTS.Test.csproj
```

The solution also includes example-focused tests under [TermRTS.Examples.Test](../TermRTS.Examples.Test).

## 7. How to add a new system

1. Implement the [TermRTS/ISimSystem.cs](../TermRTS/ISimSystem.cs) interface.
2. Write a system that reads the storage and emits events when needed.
3. Register the system with the engine through the appropriate initialization code in the example or application entry point.
4. Keep the system stateless whenever possible.

A typical system receives:

- the current tick duration,
- read-only storage,
- an event buffer for scheduling follow-up events.

## 8. How to add a new component

1. Create a new class deriving from [TermRTS/ComponentBase.cs](../TermRTS/ComponentBase.cs).
2. Add any state you need as regular properties or double-buffered properties.
3. Create or attach an entity that should own the component.
4. Add the component to the simulation through the core's scheduling APIs.

## 9. Rendering and rendering backends

Rendering is intentionally abstracted through [TermRTS/IRenderer.cs](../TermRTS/IRenderer.cs). A renderer can inspect the storage contents at the end of each tick and present the current state to the user.

If you want to add a new rendering backend, implement the renderer interface and wire it into the engine initialization.

## 10. Persistence and serialization

Persistence support is implemented in [TermRTS/Persistence.cs](../TermRTS/Persistence.cs) and exposed through [TermRTS/Simulation.cs](../TermRTS/Simulation.cs).

Important points:

- simulation state can be serialized to JSON,
- the scheduler and core state are restored through the persistence layer,
- custom component types must remain serializable for persistence features to work as expected.

## 11. Benchmarking and profiling

The benchmarking project lives in [TermRTS.Benchmark](../TermRTS.Benchmark). It is a good place to evaluate changes in performance, storage layout, and event throughput.

## 12. Recommended workflow for contributors

- Start by reading the core files in the order above: entity/component/system/scheduler/core.
- Prefer small, focused changes and add or update tests alongside behavior changes.
- Keep systems stateless and components stateful.
- Use the existing examples as references for integration patterns.
- Run the relevant test suite before submitting changes.
