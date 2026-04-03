using System.Numerics;

namespace TermRTS.Algorithms;

/// <summary>
///     Implementation of the A* path finding algorithm for 2d-grids.
/// </summary>
public ref struct ChunkAStar<T> where T : allows ref struct
{
    #region Constructor

    public ChunkAStar(int worldWidth, int worldHeight, Vector2 start, Vector2 goal, T accessor)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _goal = goal;
        _openSet = new PriorityQueue<Vector2, float>(
            Comparer<float>.Create((x, y) => Comparer<float>.Default.Compare(x, y)));
        _openSet.Enqueue(start, 0.0f);
        _isInOpenSet = [start];
        _cameFrom = new Dictionary<Vector2, Vector2>();
        _gScore = new Dictionary<Vector2, float>
        {
            [start] = 0.0f
        };
        _accessor = accessor;

        // Default to euclidean distance to goal
        Heuristic = (v, _) => Vector2.Distance(v, goal);
        // Default the weight to a constant
        Weight = (_, _, _) => 1.0f;
    }

    #endregion

    #region Fields

    // Location of the goal.
    private readonly Vector2 _goal;

    // Mapping of locations to predecessor locations, for path reconstruction.
    private readonly Dictionary<Vector2, Vector2> _cameFrom;

    // Cheapest path from start to n, currently known, defaults to infinity.
    private readonly Dictionary<Vector2, float> _gScore;

    // Track which elements are contained in the _openSet.
    private readonly HashSet<Vector2> _isInOpenSet;

    private readonly PriorityQueue<Vector2, float> _openSet;
    private readonly int _worldHeight;
    private readonly int _worldWidth;
    private readonly T _accessor;

    #endregion

    #region Properties

    /// <summary>
    ///     Determine the weight of the edge between the two given points
    /// </summary>
    public Func<Vector2, Vector2, T, float> Weight { get; set; }

    /// <summary>
    ///     Heuristic function, estimates the cost to get from a given location to the goal.
    /// </summary>
    public Func<Vector2, T, float> Heuristic { get; set; }

    #endregion

    #region Public API

    public List<Vector2>? ComputePath()
    {
        while (_openSet.Count > 0)
        {
            var currentLoc = _openSet.Dequeue();
            _isInOpenSet.Remove(currentLoc);

            if (currentLoc.Equals(_goal)) return ReconstructPath(currentLoc);

            foreach (var neighbor in Neighborhood(currentLoc))
            {
                // Wrap X coordinate for cylindrical world, clamp Y coordinate
                var wrappedX = (neighbor.X % _worldWidth + _worldWidth) % _worldWidth;
                var clampedY = Math.Clamp(neighbor.Y, 0, _worldHeight - 1);
                var wrappedNeighbor = new Vector2(wrappedX, clampedY);

                // Ensure the neighbor is within the world bounds (Y is already clamped).
                if (wrappedNeighbor.Y < 0 || wrappedNeighbor.Y >= _worldHeight)
                    continue;

                // Tentative score is the distance from start to neighbor through current.
                var tentativeScore = _gScore[currentLoc] + Weight(currentLoc, wrappedNeighbor, _accessor);

                if (tentativeScore >= _gScore.GetValueOrDefault(wrappedNeighbor, float.PositiveInfinity))
                    continue;

                // This path to neighbor is better than any previous one. Record it!
                _cameFrom[wrappedNeighbor] = currentLoc;
                _gScore[wrappedNeighbor] = tentativeScore;

                if (_isInOpenSet.Contains(wrappedNeighbor))
                    continue;

                // Current best guess for how cheap a path from start to finish through n would be.
                // Defaults to infinity.
                var fScore = tentativeScore + Heuristic(wrappedNeighbor, _accessor);
                _openSet.Enqueue(wrappedNeighbor, fScore);
                _isInOpenSet.Add(wrappedNeighbor);
            }
        }

        // open set is empty, but goal was never reached
        return null;
    }

    public Vector2 CameFrom(Vector2 loc)
    {
        return _cameFrom.GetValueOrDefault(loc, loc);
    }

    #endregion

    #region Private Methods

    private List<Vector2> ReconstructPath(Vector2 endLocation)
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
            loc with { X = loc.X - 1 },
            loc with { Y = loc.Y - 1 },
            loc with { X = loc.X + 1 },
            loc with { Y = loc.Y + 1 }
        ];
    }

    #endregion
}