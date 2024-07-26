namespace TermRTS.Examples.Circuitry;

internal readonly struct Location(int x, int y)
{
    internal int X { get; } = x;
    internal int Y { get; } = y;
}

/// <summary>
/// Implementation of the A* path finding algorithm for 2d-grids.
/// </summary>
internal class AStar
{
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly Location _goal;
    private readonly PriorityQueue<Location, float> _openSet;
    // Track which elements are contained in the _openSet.
    private readonly HashSet<Location> _isInOpenSet;
    private readonly Dictionary<Location, Location> _cameFrom;
    // Cheapest path from start to n, currently known, defaults to infinity
    private readonly Dictionary<Location, float> _gScore;
    // Heuristic function
    private readonly Func<Location, float> _h;
    // Current best guess for how cheap a path from start to finish through n would be.
    // Defaults to infinity.

    internal AStar(int worldWidth, int worldHeight, Location start, Location goal, Func<Location, float> h)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _goal = goal;
        _openSet = new PriorityQueue<Location, float>(
            Comparer<float>.Create((x, y) => Comparer<float>.Default.Compare(x, y)));
        _openSet.Enqueue(start, 0.0f);
        _isInOpenSet = new HashSet<Location> { start };
        _cameFrom = new Dictionary<Location, Location>();
        _gScore = new Dictionary<Location, float>
        {
            [start] = 0.0f
        };
        _h = h;
    }

    // TODO: Maybe conflate this function and the constructor
    internal IEnumerable<Location>? ComputePath()
    {
        while (_openSet.Count > 0)
        {
            var currentLoc = _openSet.Dequeue();
            _isInOpenSet.Remove(currentLoc);

            if (currentLoc.Equals(_goal))
            {
                return ReconstructPath(currentLoc);
            }

            foreach (var neighbor in Neighborhood(currentLoc))
            {
                // Ensure the neighbor is within the world bounds.
                if (neighbor.X < 0 || neighbor.X >= _worldWidth ||
                    neighbor.Y < 0 || neighbor.Y >= _worldHeight)
                    continue;

                // Tentative score is the distance from start to neighbor through current.
                var tentativeScore = _gScore[currentLoc] + Weight(currentLoc, neighbor);

                if (!(tentativeScore < _gScore.GetValueOrDefault(neighbor, float.PositiveInfinity)))
                    continue;

                // This path to neighbor is better than any previous one. Record it!
                _cameFrom[neighbor] = currentLoc;
                _gScore[neighbor] = tentativeScore;
                var fScore = tentativeScore + _h(neighbor);

                if (_isInOpenSet.Contains(neighbor))
                    continue;

                _openSet.Enqueue(neighbor, fScore);
                _isInOpenSet.Add(neighbor);
            }
        }
        // open set is empty, but goal was never reached
        return null;
    }

    internal IEnumerable<Location> ReconstructPath(Location endLocation)
    {
        var path = new List<Location> { endLocation };
        var current = endLocation;
        while (_cameFrom.ContainsKey(current))
        {
            current = _cameFrom[current];
            path.Add(current);
        }
        return path;
    }

    internal static Location[] Neighborhood(Location loc)
    {
        return
        [
            new Location(loc.X - 1, loc.Y),
            new Location(loc.X, loc.Y - 1),
            new Location(loc.X + 1, loc.Y),
            new Location(loc.X, loc.Y + 1)
        ];
    }

    internal float Weight(Location loc, Location neighbor)
    {
        return 1.0f;
    }
}

