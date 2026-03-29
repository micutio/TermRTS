namespace TermRTS.Examples.Greenery.WorldGen;

public interface IWorldGen
{
    WorldGenerationResult Generate();

    void Reset();
}