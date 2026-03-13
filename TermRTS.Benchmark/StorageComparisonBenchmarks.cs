using BenchmarkDotNet.Attributes;

namespace TermRTS.Benchmark;

/// <summary>
/// Compares MappedCollectionStorage vs ContiguousStorage for all public IStorage / IReadonlyStorage methods.
/// Both storages are populated with the same data (ComponentCount components, entity ids 0..ComponentCount-1).
/// </summary>
[MemoryDiagnoser]
public class StorageComparisonBenchmarks
{
    private MappedCollectionStorage _mapped = null!;
    private ContiguousStorage _contiguous = null!;

    [Params(100, 1_000, 10_000)]
    public int ComponentCount { get; set; }

    private const int EntityIdForByEntity = 0;
    private int _addEntityId => ComponentCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mapped = new MappedCollectionStorage();
        _contiguous = new ContiguousStorage();
        for (var i = 0; i < ComponentCount; i++)
        {
            var c = new BenchmarkComponent(i);
            _mapped.AddComponent(c);
            _contiguous.AddComponent(new BenchmarkComponent(i));
        }
    }

    // ---- Read: GetAllForEntity ----
    [Benchmark(Baseline = true)]
    public int Mapped_GetAllForEntity_Enumerate()
    {
        var n = 0;
        foreach (var _ in _mapped.GetAllForEntity(EntityIdForByEntity))
            n++;
        return n;
    }

    [Benchmark]
    public int Contiguous_GetAllForEntity_Enumerate()
    {
        var n = 0;
        foreach (var _ in _contiguous.GetAllForEntity(EntityIdForByEntity))
            n++;
        return n;
    }

    // ---- Read: GetAllForType ----
    [Benchmark]
    public int Mapped_GetAllForType_Enumerate()
    {
        var n = 0;
        foreach (var _ in _mapped.GetAllForType<BenchmarkComponent>())
            n++;
        return n;
    }

    [Benchmark]
    public int Contiguous_GetAllForType_Enumerate()
    {
        var n = 0;
        foreach (var _ in _contiguous.GetAllForType<BenchmarkComponent>())
            n++;
        return n;
    }

    // ---- Read: GetListForType ----
    [Benchmark]
    public int Mapped_GetListForType_Count()
    {
        return _mapped.GetListForType<BenchmarkComponent>().Count;
    }

    [Benchmark]
    public int Contiguous_GetListForType_Count()
    {
        return _contiguous.GetListForType<BenchmarkComponent>().Count;
    }

    // ---- Read: GetSingleForType ----
    [Benchmark]
    public ComponentBase? Mapped_GetSingleForType()
    {
        return _mapped.GetSingleForType<BenchmarkComponent>();
    }

    [Benchmark]
    public ComponentBase? Contiguous_GetSingleForType()
    {
        return _contiguous.GetSingleForType<BenchmarkComponent>();
    }

    // ---- Read: TryGetSingleForType ----
    [Benchmark]
    public bool Mapped_TryGetSingleForType()
    {
        return _mapped.TryGetSingleForType<BenchmarkComponent>(out _);
    }

    [Benchmark]
    public bool Contiguous_TryGetSingleForType()
    {
        return _contiguous.TryGetSingleForType<BenchmarkComponent>(out _);
    }

    // ---- Read: GetAllForTypeAndEntity ----
    [Benchmark]
    public int Mapped_GetAllForTypeAndEntity_Enumerate()
    {
        var n = 0;
        foreach (var _ in _mapped.GetAllForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity))
            n++;
        return n;
    }

    [Benchmark]
    public int Contiguous_GetAllForTypeAndEntity_Enumerate()
    {
        var n = 0;
        foreach (var _ in _contiguous.GetAllForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity))
            n++;
        return n;
    }

    // ---- Read: GetSingleForTypeAndEntity ----
    [Benchmark]
    public ComponentBase? Mapped_GetSingleForTypeAndEntity()
    {
        return _mapped.GetSingleForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity);
    }

    [Benchmark]
    public ComponentBase? Contiguous_GetSingleForTypeAndEntity()
    {
        return _contiguous.GetSingleForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity);
    }

    // ---- Read: TryGetSingleForTypeAndEntity ----
    [Benchmark]
    public bool Mapped_TryGetSingleForTypeAndEntity()
    {
        return _mapped.TryGetSingleForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity, out _);
    }

    [Benchmark]
    public bool Contiguous_TryGetSingleForTypeAndEntity()
    {
        return _contiguous.TryGetSingleForTypeAndEntity<BenchmarkComponent>(EntityIdForByEntity, out _);
    }

    // ---- Read: SwapBuffers ----
    [Benchmark]
    public void Mapped_SwapBuffers()
    {
        _mapped.SwapBuffers();
    }

    [Benchmark]
    public void Contiguous_SwapBuffers()
    {
        _contiguous.SwapBuffers();
    }

    // ---- Write: AddComponent (add one, cleanup removes it so state is unchanged for next iteration) ----
    [IterationCleanup(Target = nameof(Mapped_AddComponent))]
    public void Cleanup_Mapped_AddComponent()
    {
        _mapped.RemoveComponentsByEntity(_addEntityId);
    }

    [Benchmark]
    public void Mapped_AddComponent()
    {
        _mapped.AddComponent(new BenchmarkComponent(_addEntityId));
    }

    [IterationCleanup(Target = nameof(Contiguous_AddComponent))]
    public void Cleanup_Contiguous_AddComponent()
    {
        _contiguous.RemoveComponentsByEntity(_addEntityId);
    }

    [Benchmark]
    public void Contiguous_AddComponent()
    {
        _contiguous.AddComponent(new BenchmarkComponent(_addEntityId));
    }

    // ---- Write: RemoveComponentsByEntity (remove entity 0, setup restores it) ----
    [IterationSetup(Target = nameof(Mapped_RemoveComponentsByEntity))]
    public void Setup_Mapped_RemoveComponentsByEntity()
    {
        _mapped.AddComponent(new BenchmarkComponent(EntityIdForByEntity));
    }

    [Benchmark]
    public void Mapped_RemoveComponentsByEntity()
    {
        _mapped.RemoveComponentsByEntity(EntityIdForByEntity);
    }

    [IterationSetup(Target = nameof(Contiguous_RemoveComponentsByEntity))]
    public void Setup_Contiguous_RemoveComponentsByEntity()
    {
        _contiguous.AddComponent(new BenchmarkComponent(EntityIdForByEntity));
    }

    [Benchmark]
    public void Contiguous_RemoveComponentsByEntity()
    {
        _contiguous.RemoveComponentsByEntity(EntityIdForByEntity);
    }

    // ---- Write: RemoveComponentsByType (setup restores all components each iteration) ----
    [IterationSetup(Target = nameof(Mapped_RemoveComponentsByType))]
    public void Setup_Mapped_RemoveComponentsByType()
    {
        for (var i = 0; i < ComponentCount; i++)
            _mapped.AddComponent(new BenchmarkComponent(i));
    }

    [Benchmark]
    public void Mapped_RemoveComponentsByType()
    {
        _mapped.RemoveComponentsByType(typeof(BenchmarkComponent));
    }

    [IterationSetup(Target = nameof(Contiguous_RemoveComponentsByType))]
    public void Setup_Contiguous_RemoveComponentsByType()
    {
        for (var i = 0; i < ComponentCount; i++)
            _contiguous.AddComponent(new BenchmarkComponent(i));
    }

    [Benchmark]
    public void Contiguous_RemoveComponentsByType()
    {
        _contiguous.RemoveComponentsByType(typeof(BenchmarkComponent));
    }

    // ---- Write: RemoveComponentsByEntityAndType (remove entity 0's BenchmarkComponent, setup restores) ----
    [IterationSetup(Target = nameof(Mapped_RemoveComponentsByEntityAndType))]
    public void Setup_Mapped_RemoveComponentsByEntityAndType()
    {
        _mapped.AddComponent(new BenchmarkComponent(EntityIdForByEntity));
    }

    [Benchmark]
    public void Mapped_RemoveComponentsByEntityAndType()
    {
        _mapped.RemoveComponentsByEntityAndType(EntityIdForByEntity, typeof(BenchmarkComponent));
    }

    [IterationSetup(Target = nameof(Contiguous_RemoveComponentsByEntityAndType))]
    public void Setup_Contiguous_RemoveComponentsByEntityAndType()
    {
        _contiguous.AddComponent(new BenchmarkComponent(EntityIdForByEntity));
    }

    [Benchmark]
    public void Contiguous_RemoveComponentsByEntityAndType()
    {
        _contiguous.RemoveComponentsByEntityAndType(EntityIdForByEntity, typeof(BenchmarkComponent));
    }
}
