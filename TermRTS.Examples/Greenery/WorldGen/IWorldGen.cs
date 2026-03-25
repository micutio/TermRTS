namespace TermRTS.Examples.Greenery.WorldGen;

public interface IWorldGen
{
    WorldGenerationResult Generate(int worldWidth, int worldHeight, float landRatio);

    void Reset();
}