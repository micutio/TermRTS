using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class World : TermRTS.IWorld
{
    public Vector2 Size;

    public World()
    {
        Size = new Vector2(Console.WindowWidth, Console.WindowHeight);
    }

    public void ApplyChange() { }

}

