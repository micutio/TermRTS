using SimplexNoise;

namespace TermRTS.Examples.Greenery;

public interface IWorldGen
{
    public byte[,] Generate(int worldWidth, int worldHeight);
}

public class VoronoiWorld(int cellCount, int jiggle, int seed = 0) : IWorldGen
{
    private readonly Random _rng = new(seed);
    
    public byte[,] Generate(int worldWidth, int worldHeight)
    {
        Noise.Seed = seed;
        var cells = new int[worldWidth, worldHeight];
        
        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        var voronois = new int[cellCount];
        for (var i = 0; i < cellCount; i += 1) voronois[i] = _rng.Next(worldWidth * worldHeight);
        
        var landWaterMap = _rng.GetItems([3, 4], cellCount);
        var jiggleNoiseField = Noise.Calc2D(worldWidth, worldHeight, 0.1f);
        
        // step 2: associate each grid cell to one of the cell seeds
        // step 3: for each voronoi cell, determine whether it's going to be water or land
        
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var minDist = int.MaxValue;
            for (var i = 0; i < cellCount; i += 1)
            {
                var vX = voronois[i] % worldWidth;
                var vY = voronois[i] / worldWidth;
                var dist = (int)Math.Sqrt(Math.Pow(vX - x, 2) + Math.Pow(vY - y, 2));
                var jiggleVal = (int)(jiggleNoiseField[x, y] / 255 * jiggle);
                dist += jiggleVal;
                if (dist >= minDist) continue;
                
                minDist = dist;
                cells[x, y] = landWaterMap[i];
            }
        }
        
        // step 4: for each voronoi land cell, apply perlin or simplex noise to generate height
        Noise.Seed = seed;
        const float noiseScale = 0.1f;
        var noiseField = Noise.Calc2D(worldWidth, worldHeight, noiseScale);
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var factor = cells[x, y] == 4 ? 1 : -1;
            var noiseVal = (int)(noiseField[x, y] / 255.0f * 5.0f);
            cells[x, y] = Math.Clamp(cells[x, y] + noiseVal * factor, 0, 9);
        }
        // step 5: for each voronoi water cell, apply perlin or simplex noise to generate depth
        
        // optional: cluster multiple voronoi cells to generate convex land/water shapes
        // optional: apply more techniques from "around the world" to get more appealing shapes
        
        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            world[x, y] = Convert.ToByte(cells[x, y]);
        return world;
    }
}