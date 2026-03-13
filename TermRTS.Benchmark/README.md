# TermRTS Benchmarks

BenchmarkDotNet benchmarks for simulation loop overhead and throughput.

## Run

From repo root:

```bash
dotnet run -c Release --project TermRTS.Benchmark
```

List benchmarks (no run):

```bash
dotnet run -c Release --project TermRTS.Benchmark -- --list flat
```

Run a single benchmark with a short job:

```bash
dotnet run -c Release --project TermRTS.Benchmark -- --filter "*SchedulerStepBaseline*" --job short
```

Results are written to `BenchmarkDotNet.Artifacts/` (gitignored).

## Benchmarks

- **SchedulerStepBaselineBenchmark** – Step overhead with no systems and no-op renderer.
- **SchedulerStepTickLoadBenchmark** – Step time with K BusySystems (0.1 ms each); `[Params(1, 4, 8, 16)]`.
- **CoreTickOnlyBenchmark** – `Core.Tick(16)` only (no scheduler/render); entities and no-op systems via `[Params]`.
- **SchedulerStepHeavyRenderBenchmark** – Step time when renderer sleeps 2/5/10 ms per frame.
- **StorageComparisonBenchmarks** – Compares `MappedCollectionStorage` vs `ContiguousStorage` for all public `IStorage` / `IReadonlyStorage` methods. Both storages are filled with the same data; `[Params(100, 1_000, 10_000)]` for component count. Covers: `GetAllForEntity`, `GetAllForType`, `GetListForType`, `GetSingleForType`, `TryGetSingleForType`, `GetAllForTypeAndEntity`, `GetSingleForTypeAndEntity`, `TryGetSingleForTypeAndEntity`, `SwapBuffers`, `AddComponent`, `RemoveComponentsByEntity`, `RemoveComponentsByType`, `RemoveComponentsByEntityAndType`.

### Running storage comparison only

```bash
dotnet run -c Release --project TermRTS.Benchmark -- --filter "*StorageComparison*"
```

Quick run (short job):

```bash
dotnet run -c Release --project TermRTS.Benchmark -- --filter "*StorageComparison*" --job short
```

## Extending

Add a new class with `[MemoryDiagnoser]` and `[Benchmark]` methods; it will be picked up by `BenchmarkSwitcher`.
