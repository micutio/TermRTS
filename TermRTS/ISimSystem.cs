namespace TermRTS;

/// <summary>
///     A System defines one or multiple required components and processes <c>Entity</c>s which
///     provide all of these.
/// </summary>
public interface ISimSystem
{
    public abstract void ProcessComponents(ulong timeStepSizeMs, in IStorage storage);
}