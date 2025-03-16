namespace TermRTS;

/// <summary>
///     A System defines one or multiple required components and processes <c>Entity</c>s which
///     provide all of these.
///     NOTE: Ideally a System should NEVER carry internal state, because:
///       - all data and state is supposed to be kept within COMPONENTS
///       - internal state makes systems much harder to serialize
/// </summary>
public interface ISimSystem
{
    public abstract void ProcessComponents(ulong timeStepSizeMs, in IStorage storage);
}