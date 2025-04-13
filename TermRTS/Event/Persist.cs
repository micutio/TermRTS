namespace TermRTS.Event;

public enum PersistenceOption
{
    Load,
    Save
}

public readonly record struct Persist(PersistenceOption Option, string JsonFilePath)
{
}