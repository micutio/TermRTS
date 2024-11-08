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
        
        // step 1: randomly sample <cellCount> coordinates of the grid as voronoi cell seeds
        var voronois = new int[cellCount];
        for (var i = 0; i < cellCount; i += 1) voronois[i] = _rng.Next(worldWidth * worldHeight);
        
        
        // step 2: for each voronoi cell, determine whether it's going to be water or land
        var landWaterMap = _rng.GetItems([3, 4], cellCount);
        
        // step 3: associate each grid cell to one of the cell seeds
        var cellHeights = new int[worldWidth, worldHeight];
        var heightFactors = new double[worldWidth, worldHeight];
        var jiggleNoiseField = Noise.Calc2D(worldWidth, worldHeight, 0.1f);
        
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var minDist = int.MaxValue;
            var minHeightFactor = double.MaxValue;
            var lastLandWaterValue = 0;
            for (var i = 0; i < cellCount; i += 1)
            {
                var thisWaterLandValue = landWaterMap[i];
                // Do not compare two voronoi cells of the same type to avoid visual 'seams'. 
                if (thisWaterLandValue == lastLandWaterValue) continue;
                lastLandWaterValue = thisWaterLandValue;
                
                var vX = voronois[i] % worldWidth;
                var vY = voronois[i] / worldWidth;
                var dist = Math.Sqrt(Math.Pow(vX - x, 2.0f) + Math.Pow(vY - y, 2.0f));
                var jiggleVal = jiggleNoiseField[x, y] / 255.0f * jiggle;
                dist += jiggleVal;
                
                // Gauge the distance to the shoreline by how close to equidistant we are between
                // two voronoi cell centres. Normalise and use this as multiplier for height.
                var heightFactor = Math.Min(9.0f, Math.Abs(dist - minDist) * 0.6) / 9.0f;
                minHeightFactor = Math.Min(minHeightFactor, heightFactor);
                
                if (dist > minDist) continue;
                
                minDist = (int)dist;
                cellHeights[x, y] = landWaterMap[i];
            }
            
            heightFactors[x, y] = minHeightFactor;
        }
        
        // step 4: for each voronoi land cell, apply perlin or simplex noise to generate height
        Noise.Seed = seed;
        var noiseField = Noise.Calc2D(worldWidth, worldHeight, 0.5f);
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
        {
            var baseHeight = cellHeights[x, y] == 4 ? 5.0f : -3.0f;
            var normalizedNoise = noiseField[x, y] / 255.0f;
            var heightVal = cellHeights[x, y] + normalizedNoise * baseHeight * heightFactors[x, y];
            
            // for debug only
            //cellHeights[x, y] = (int)(heightFactors[x, y] * 9.0f);
            cellHeights[x, y] = (int)Math.Clamp(heightVal, 0.0f, 9.0f);
        }
        // step 5: for each voronoi water cell, apply perlin or simplex noise to generate depth
        
        // optional: cluster multiple voronoi cells to generate convex land/water shapes
        // optional: apply more techniques from "around the world" to get more appealing shapes
        
        var world = new byte[worldWidth, worldHeight];
        for (var y = 0; y < worldHeight; y += 1)
        for (var x = 0; x < worldWidth; x += 1)
            world[x, y] = Convert.ToByte(cellHeights[x, y]);
        return world;
    }
}