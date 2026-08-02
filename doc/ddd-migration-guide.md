# DDB migration guide for TermRTS

This guide is intended for a human maintainer who wants to migrate TermRTS toward a more Domain-Driven Behavior architecture without doing a risky rewrite.

## Guiding principle

Do not replace the engine first. Instead, make the domain behavior more explicit while preserving the existing scheduler, storage, and core runtime.

The migration should happen in small, reversible steps.

## Phase 1: Identify the real domain concepts

Before changing code, write down the domain vocabulary you want the project to express.

Examples for TermRTS:

- world state,
- entity role,
- interaction rule,
- event outcome,
- lifecycle transition,
- simulation command.

The goal is to move from implementation-driven names toward behavior-driven names.

### Suggested exercise

For each existing system or component, ask:

- what domain behavior does this represent?
- what concept would a designer or gameplay author call this?
- what would change if this rule were expressed in domain language instead of engine language?

## Phase 2: Separate engine mechanics from domain behavior

The engine runtime should remain responsible for:

- ticking,
- scheduling,
- storage,
- rendering,
- persistence.

The domain layer should become responsible for:

- the rules that define what happens in the simulation,
- transitions between states,
- event semantics,
- gameplay-specific policies.

### Practical rule

If a class is mainly about execution infrastructure, keep it where it is. If a class is mainly about the meaning of the simulation, move it toward a domain-oriented structure.

## Phase 3: Introduce a domain-facing layer

Create a new folder such as:

- [TermRTS/Domain](../TermRTS/Domain) or
- [TermRTS/Behavior](../TermRTS/Behavior)

Inside it, add classes that express the simulation domain more clearly.

### Good candidates for this layer

- rules that decide how state changes,
- command objects that describe an intent,
- policy objects that define outcomes,
- state transition objects,
- domain event types.

### Example shape

You might introduce types such as:

- ActionCommand
- SimulationRule
- WorldState
- DomainEvent
- BehaviorPolicy

These should be simple and focused. Avoid making the domain layer too abstract.

## Phase 4: Keep the current engine interfaces, but use them as adapters

Do not throw away the existing abstractions. Instead, adapt them.

The current engine already has:

- components,
- systems,
- storage,
- scheduler,
- core.

These can remain as the execution backbone.

A new domain layer can call into them rather than replacing them.

### Example migration pattern

1. A domain rule is evaluated.
2. It produces a command or event.
3. The engine subsystem applies that command through the existing core/scheduler path.

That gives you a clear separation between “what should happen” and “how it is executed.”

## Phase 5: Move gameplay logic out of generic systems

One of the most valuable changes is to reduce the amount of gameplay logic embedded directly in generic systems.

Instead of a system directly handling many behaviors, prefer:

- a small orchestrating system,
- a domain rule or policy that decides the behavior,
- a component or state object that carries the data.

### Before

A system contains many conditionals about entity behavior, state transitions, and event side effects.

### After

A system invokes a domain policy that encapsulates that behavior.

This makes the code easier to reason about and test.

## Phase 6: Introduce explicit state transitions

A DDB-style migration becomes much more powerful when you make transitions explicit.

Instead of relying on ad hoc property updates, define:

- possible states,
- allowed transitions,
- transition rules,
- triggers for transitions.

This is especially useful for systems that model:

- movement,
- resource changes,
- toggles,
- event-driven state changes.

## Phase 7: Use events as the main communication mechanism

The engine already has an event model, which is a good fit for a behavior-oriented architecture.

Use events to represent:

- domain actions,
- state changes,
- system outcomes,
- important simulation milestones.

This helps keep the engine decoupled from the domain logic.

## Phase 8: Refactor incrementally by feature area

Avoid a big-bang migration.

Work one feature at a time:

1. choose one gameplay area,
2. identify the domain concepts behind it,
3. extract the behavior into a domain-oriented class or policy,
4. keep the engine integration intact,
5. test the behavior,
6. repeat.

This is the safest way to migrate a small engine like TermRTS.

## Recommended migration order

### Step 1: Document the current behavior

Before changing code, write down the rules for one gameplay feature.

### Step 2: Extract a rule object

Move the decision logic into a dedicated class that describes the behavior in domain terms.

### Step 3: Keep the runtime integration intact

Have the existing system call into the new rule object.

### Step 4: Introduce a command or event

Represent the action as an explicit domain command or event.

### Step 5: Add tests around the rule object

This is where the migration pays off: the rules become easier to validate without needing the whole scheduler.

### Step 6: Repeat for the next feature

Continue gradually.

## Concrete refactoring patterns

### Pattern A: Extract a policy

If a system has a large block of logic that decides what should happen, turn it into a policy class.

### Pattern B: Extract a command

If a system currently mutates several components directly, create a command object that describes the intended change.

### Pattern C: Introduce a state machine

If an object changes through several distinct stages, define those stages as a state machine.

### Pattern D: Split read and write responsibilities

Keep one object responsible for evaluating state and another responsible for applying changes.

## Testing strategy during migration

As you migrate, preserve behavior by adding tests around the extracted rules.

Focus on:

- state transitions,
- event outcomes,
- rule decisions,
- cross-component interactions.

This will make the migration safer and more maintainable.

## Risks to avoid

### Avoid over-abstracting too early

Do not introduce repositories, aggregates, or rich domain services unless they clearly help the current design.

### Avoid a rewrite

The engine already works. Preserve it.

### Avoid mixing concerns

Do not let domain classes reach into scheduling or rendering internals unless that is truly necessary.

## Suggested first target for migration

The best first target is a single gameplay behavior that currently feels tangled or hard to reason about.

Good candidates:

- movement or interaction rules,
- event-driven state changes,
- resource or inventory-style behavior,
- player or actor decision logic.

Choose one feature that is meaningful but contained.

## Final recommendation

A DDB-style migration for TermRTS should be incremental and pragmatic.

The most effective approach is:

- keep the engine runtime as-is,
- introduce a domain layer for gameplay behavior,
- move rules into explicit, testable classes,
- use events and commands for interaction,
- evolve one feature at a time.

That will give you most of the maintainability benefits of DDB without turning the project into a heavy architectural exercise.
