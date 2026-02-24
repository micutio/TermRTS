# TermRTS solution structure – comments and suggestions

A closer look at how the solution and projects are organized, with optional improvements.

---

## 1. Solution files: `.sln` vs `.slnx`

You have two solution entry points:

- **TermRTS.sln** – classic Visual Studio solution (all three projects, configs, GUIDs). Used by CI and most tooling.
- **TermRTS.slnx** – simplified solution format (three projects, explicit build dependency for Examples → TermRTS). No Test → TermRTS dependency expressed.

**Suggestions:**

- Prefer **one** solution format so CI, IDE, and scripts all use the same file. Right now CI uses `TermRTS.sln`; if you use `.slnx` in the IDE, keep both in sync or standardize on `.sln` and remove/ignore `.slnx`.
- If you keep both: add a short note in the README or this doc (e.g. “Build/CI use `TermRTS.sln`; `.slnx` is for …”).

---

## 2. Solution organization (`.sln`)

Projects are listed in a flat way: TermRTS, TermRTS.Test, TermRTS.Examples.

**Optional improvement:** Use **solution folders** to group projects (e.g. `src`, `tests`, `samples` or `examples`). That doesn’t change build behavior but makes the solution clearer in the IDE and communicates intent (engine vs tests vs examples).

---

## 3. Engine project (TermRTS)

**What works well:**

- **Namespaces match folders:** `TermRTS`, `TermRTS.Algorithms`, `TermRTS.Data`, `TermRTS.Event`, `TermRTS.Io`, `TermRTS.Serialization`, `TermRTS.Ui`. Easy to navigate.
- **Clear public API:** Interfaces (`IRenderer`, `ISimSystem`, `IStorage`, `IReadonlyStorage`, `IWritableStorage`, `IEvent`, `IEventSink`) and core types (`Core`, `Simulation`, `Scheduler`, `EntityBase`, `ComponentBase`, etc.) are public; implementation details stay internal where appropriate (`CoreState`, `SchedulerState`, `CachedEnumerable`).
- **Single dependency:** Only log4net; no UI or console framework in the engine except `TermRTS.Io.ConsoleInput`, which is a reasonable choice for a terminal-oriented engine.
- **Subfolders by concern:** Algorithms, Data, Event, Io, Serialization, Ui keep the root from being crowded.

**Optional refinements:**

- **Root namespace density:** Several important types live in the root `TermRTS` namespace (e.g. `Core`, `Simulation`, `Scheduler`, `Storage`, `Persistence`, `Profiler`). You could later introduce namespaces like `TermRTS.Persistence` or `TermRTS.Scheduling` if the root grows, but current size is fine.
- **Console coupling:** `ConsoleInput` in `TermRTS.Io` uses `Console` directly. For a terminal-focused engine this is acceptable; if you ever need a “headless” engine build, you could move that to an optional assembly or keep it and document that the engine has one console-dependent type.

---

## 4. Examples project (TermRTS.Examples)

**What works well:**

- **One entry point:** `ExampleRunner` with `Main(string[] args)`; example selection by argument (e.g. `1`–`4`). No need for a separate `Program.cs` unless you want the convention.
- **Per-example structure:** Each example has its own folder and namespace (Minimal, BouncyBall, Circuitry, Greenery). Greenery is further split (Ui, System, Command, Event, WorldGen, Ecs), which matches its complexity.
- **Examples depend only on TermRTS:** Clean dependency direction; no engine → examples reference.

**Issues to fix / consider:**

- **README vs code:** README lists examples 1–3 (Minimal, Bouncy Ball, Circuitry). The code supports **4** (Greenery). Update the README to include “[4] Greenery”.
- **Entry-point robustness:** `ExampleRunner.Main` uses `args[0]` without checking `args.Length`. With no arguments this throws `IndexOutOfRangeException`. Add a guard (e.g. show usage and return non-zero when `args.Length == 0`).
- **Discovery:** If the number of examples grows, consider moving the mapping (e.g. "1" → MinimalApp, "2" → BouncyBall, …) to a list or registry so adding an example is a single place to edit.

---

## 5. Test project (TermRTS.Test)

**What works well:**

- References only **TermRTS** (no Examples). Tests the engine in isolation.
- Test files align with engine areas: `EngineTest`, `StorageTest`, `EventQueueTest`, `SerializationTest`, `ScannerTest`, `BitwiseTest`, converter tests, etc.
- `GlobalUsings.cs` for `Xunit` keeps test files clean.
- No unnecessary subfolders for the current size.

**Optional:**

- If the test count grows, grouping tests into folders (e.g. `Engine`, `Storage`, `Serialization`, `Algorithms`) can help. Not required at current scale.

---

## 6. Dependencies overview

- **TermRTS:** log4net only. Keeps the engine easy to reuse and test.
- **TermRTS.Examples:** TermRTS + ConsoleRenderer, SimplexNoise, Xdg.Directories, log4net. All are appropriate for a terminal/examples host.
- **TermRTS.Test:** TermRTS + xUnit and test SDK. No example or UI packages.

Dependency direction is correct: engine at the bottom, tests and examples on top.

---

## 7. Summary table

| Area              | Status / suggestion |
|-------------------|---------------------|
| Solution files    | Prefer one primary (e.g. `.sln`); document or remove `.slnx`. |
| Solution folders  | Optional: add `src` / `tests` / `examples` in `.sln`. |
| Engine layout     | Namespaces and folders aligned; public API clear. |
| Examples entry    | Add `args.Length` check in `ExampleRunner.Main`; document example 4 in README. |
| Tests             | Structure and references are good. |
| Dependencies      | Clean; engine stays minimal. |

Overall the structure is clear and consistent. The main actionable items are: align solution file usage, harden the example entry point, and keep the README in sync with available examples (including Greenery).
