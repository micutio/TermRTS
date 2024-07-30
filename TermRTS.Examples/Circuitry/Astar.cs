using System.Numerics;

namespace TermRTS.Examples.Circuitry;

/// <summary>
///     Implementation of the A* path finding algorithm for 2d-grids.
/// </summary>
internal class AStar
{
    private readonly Dictionary<Vector2, Vector2> _cameFrom;
    private readonly Vector2 _goal;

    // Cheapest path from start to n, currently known, defaults to infinity
    private readonly Dictionary<Vector2, float> _gScore;

    // Heuristic function
    private readonly Func<Vector2, float> _h;

    // Track which elements are contained in the _openSet.
    private readonly HashSet<Vector2> _isInOpenSet;

    private readonly PriorityQueue<Vector2, float> _openSet;
    private readonly int _worldHeight;
    private readonly int _worldWidth;

    internal AStar(
        int worldWidth,
        int worldHeight,
        Vector2 start,
        Vector2 goal,
        Func<Vector2, float> h)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _goal = goal;
        _openSet = new PriorityQueue<Vector2, float>(
            Comparer<float>.Create((x, y) => Comparer<float>.Default.Compare(x, y)));
        _openSet.Enqueue(start, 0.0f);
        _isInOpenSet = new HashSet<Vector2> { start };
        _cameFrom = new Dictionary<Vector2, Vector2>();
        _gScore = new Dictionary<Vector2, float>
        {
            [start] = 0.0f
        };
        _h = h;
    }

    // TODO: Maybe conflate this function and the constructor
    internal IEnumerable<Vector2>? ComputePath()
    {
        while (_openSet.Count > 0)
        {
            var currentLoc = _openSet.Dequeue();
            _isInOpenSet.Remove(currentLoc);

            if (currentLoc.Equals(_goal)) return ReconstructPath(currentLoc);

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

                // Current best guess for how cheap a path from start to finish through n would be.
                // Defaults to infinity.
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

    private IEnumerable<Vector2> ReconstructPath(Vector2 endLocation)
    {
        var path = new List<Vector2> { endLocation };
        var current = endLocation;
        while (_cameFrom.ContainsKey(current))
        {
            current = _cameFrom[current];
            path.Add(current);
        }

        return path;
    }

    private static Vector2[] Neighborhood(Vector2 loc)
    {
        return
        [
            new Vector2(loc.X - 1, loc.Y),
            new Vector2(loc.X, loc.Y - 1),
            new Vector2(loc.X + 1, loc.Y),
            new Vector2(loc.X, loc.Y + 1)
        ];
    }

    private float Weight(Vector2 loc, Vector2 neighbor)
    {
        return Vector2.Distance(loc, neighbor);
    }
}