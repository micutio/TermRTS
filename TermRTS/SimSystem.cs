namespace TermRTS;

/// <summary>
///     A System defines one or multiple required components and processes <c>Entity</c>s which
///     provide all of these.
/// </summary>
public abstract class SimSystem
{
    // TODO: Investigate use of `in` keyword for storage.
    
    public abstract void ProcessComponents(ulong timeStepSize, in IStorage storage);
}