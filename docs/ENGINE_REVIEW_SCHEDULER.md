# Engine review: Scheduler and related code (readability & performance)

Focused on `Scheduler.cs`, with `Core.cs`, `EventQueue.cs`, and related types.

---

## Scheduler.cs

### Readability

- **Doc typo (line 17):** "offers a manual way of schedule" → e.g. "offers a manual way to schedule an event".
- **ProcessInput:** `eventItem.Item1` / `eventItem.Item2` are unclear. Use deconstruction: `(var evt, var scheduledTime)` and then `evt.EvtType` so intent is obvious.
- **AddEventSink:** When the list already exists, `_eventSinks[payloadType] = sinks` is redundant (same reference). Logic can be simplified so the list is only created and assigned when missing.
- **RemoveEventSink:** Uses `_eventSinks[payloadType].Remove(sink)` directly. If `payloadType` is not in the dictionary, this throws `KeyNotFoundException`. Prefer `TryGetValue` and handle missing key. After `Remove`, if the list is empty, removing the key keeps the map tidy and avoids stale empty lists.
- **Pause (line 219):** Comment says "15(?)/>" – looks like a typo (e.g. "15 ms").

### Performance

- **ProcessInput lock contention:** For each due event you do `TryPeek` (lock) then `TryTake` (lock). That’s two lock acquisitions per event. Adding a single method that, under one lock, peeks and—if the item is due—dequeues and returns it (e.g. `TryTakeIfDue`) cuts lock traffic in half on the hot path.
- **Pause busy-spin:** When `timeout <= TimeResolution` (100 ms), the code busy-spins in `while (_pauseTimer.Elapsed < timeout)`. That burns CPU and can hurt power and other threads. For short waits, prefer `Thread.Sleep(1)` (or `Thread.Sleep(0)` to yield) instead of a tight loop. You trade a bit of timing resolution for much lower CPU use.
- **SimulationStep:** Time and lag math is fine. No extra allocations in the hot path.

### Other

- **Events with no sink:** In `ProcessInput`, if `TryGetValue(eventItem.Item1.EvtType, ...)` fails, the event is dropped (already dequeued). That may be intentional; if not, consider logging or re-enqueueing for unknown event types.
- **SchedulerEventQueue.EnqueueEvent:** Throws if `TryAdd` returns false, but `EventQueue.TryAdd` currently always returns true. Either document that enqueue never fails or simplify to `Add` and remove the throw.

---

## Core.cs

### Readability

- **Renderer:** `public IRenderer Renderer { get; set; }` is non-nullable but not set in the constructor, so the compiler warns (CS8618). Either make it `required`, init-only, or nullable so the contract is clear.
- **Entity removal loop:** The `while (i < _entities.Count)` with `RemoveAt(i)` is clear and avoids the LINQ allocation of the commented variant. Keeping the loop is good for readability and performance.

### Performance

- **IsParallelized:** `foreach (var sys in _systems.AsParallel())` uses the default degree of parallelism. If you need to cap concurrency (e.g. to avoid oversubscribing), use `.AsParallel().WithDegreeOfParallelism(...)`.
- **SpawnNewEntities:** `foreach (var c in _newComponents) _components.AddComponent(c)` does one storage lookup per component. If `MappedCollectionStorage` added an `AddComponents` path that batches by type, you could reduce lookups; for moderate numbers of new components this is acceptable.
- **Entity removal:** `RemoveAt(i)` in a `List<T>` is O(n) per call. If you frequently remove many entities in one tick, collecting indices and removing in reverse order (or swapping with last and shrinking) reduces moves. For typical low removal rates, the current approach is fine.

---

## EventQueue.cs

### Readability

- **TryAdd:** Always returns `true`. The Try-prefix suggests it can fail. Either document that it never fails or rename to `Add` and return void.
- **TryPeek:** Returns `TPriority?`; for `ulong` the nullable is a bit odd but consistent with the Try pattern.

### Performance

- **ProcessInput call pattern:** Scheduler calls `TryPeek` then `TryTake` in a loop, so two locks per event. A method that under one lock peeks and, if the priority is due, dequeues and returns the item (e.g. `TryTakeIf(Predicate<TPriority> isDue)`) would reduce lock acquisitions and contention.
- **GetSerializableElements:** Uses `UnorderedItems` + `Select` + `ToList`. Allocations are appropriate for a save path; no change needed for the hot loop.

---

## Storage.cs (brief)

- **GetAllForType:** Caching via `ToCachedEnumerable` is good; first enumeration materializes, later ones use the cache.
- **AddComponent:** Multiple `_componentStores[component.GetType()]` lookups; could cache `var type = component.GetType()` in a local to avoid repeated reflection (minor).
- **RemoveComponentsByEntity:** Clears the entire `_cachedGetForTypeQueries` cache. Only the affected component types need invalidation; clearing all is simpler and fine unless you have many types and very frequent entity removal.

---

## Profiler.cs (brief)

- **Initialize:** Does not set `_droppedFrames`; it relies on default 0. That’s correct.
- **ToString:** Commented block with min/max stats could be restored behind a verbosity flag if you want that data without cluttering the default string.

---

## Summary of applied fixes (in code)

1. **Scheduler:** Doc typo fixed; `ProcessInput` uses deconstruction; `AddEventSink` simplified; `RemoveEventSink` uses `TryGetValue` and removes the key when the list becomes empty; `Pause` uses `Thread.Sleep(1)` for short waits instead of busy-spin.
2. **EventQueue:** New `TryTakeIfDue(Func<TPriority, bool> isDue, out ...)` so Scheduler can process one due event under a single lock; `ProcessInput` uses it.
3. **Core:** No change in this pass (Renderer nullability can be handled separately).

These keep behavior the same while improving readability and reducing lock contention and CPU waste in the scheduler loop.
