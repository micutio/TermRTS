using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class EntityGenerator
{
    /// <summary>
    ///     Static method for starting a random circuit board creation using Builder Pattern.
    ///     The returned <see cref="EntityGenerator" /> instance comes with default values for all its
    ///     parameters, but can be customised using the builder pattern.
    /// </summary>
    /// <returns>New <see cref="EntityGenerator" /> instance with default values.</returns>
    internal static EntityGenerator RandomCircuitBoard()
    {
        return new EntityGenerator();
    }

    #region Private Fields

    // readonly utilities and internal state
    private readonly Dictionary<App.Chip, ISet<App.Chip>> _adjacency;
    private readonly List<EntityBase> _chipEntities;
    private readonly List<EntityBase> _busEntities;
    private readonly List<App.Chip> _generatedChipComponents;
    private readonly List<App.Bus> _generatedBusComponents;

    // world generation parameters
    private int _worldWidth;
    private int _worldHeight;
    private int _chipCount;
    private int _minChipSize;
    private int _maxChipSize;
    private int _minBusWidth;
    private int _maxBusWidth;

    // utilities and internal state
    // TODO: These fields can be readonly if we convert it to a factory
    private int? _rngSeed;
    private Random _rng;
    private byte[,] _occupation;

    #endregion

    #region Builder Pattern

    private EntityGenerator()
    {
        _worldWidth = 80;
        _worldHeight = 40;
        _chipCount = 5;
        _minChipSize = 3;
        _maxChipSize = 6;
        _minBusWidth = 1;
        _maxBusWidth = 1;

        _adjacency = new Dictionary<App.Chip, ISet<App.Chip>>();
        _generatedChipComponents = new List<App.Chip>();
        _generatedBusComponents = new List<App.Bus>();
        _chipEntities = new List<EntityBase>();
        _busEntities = new List<EntityBase>();
        _rng = _rngSeed != null ? new Random((int)_rngSeed) : new Random();
        _occupation = new byte[_worldWidth, _worldHeight];
    }

    internal EntityGenerator WithRandomSeed(int seed)
    {
        _rngSeed = seed;
        return this;
    }

    internal EntityGenerator WithWorldDimensions(int worldWidth, int worldHeight)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        return this;
    }

    internal EntityGenerator WithChipCount(int chipCount)
    {
        _chipCount = chipCount;
        return this;
    }

    internal EntityGenerator WithChipDimensions(int minChipSize, int maxChipSize)
    {
        _minChipSize = minChipSize;
        _maxChipSize = maxChipSize;
        return this;
    }

    internal EntityGenerator WithBusDimensions(int minBusWidth, int maxBusWidth)
    {
        _minBusWidth = minBusWidth;
        _maxBusWidth = maxBusWidth;
        return this;
    }

    internal void Build(out List<EntityBase> entities, out List<ComponentBase> components)
    {
        _rng = _rngSeed != null ? new Random((int)_rngSeed) : new Random();
        _occupation = new byte[_worldWidth, _worldHeight];

        GenerateChips();
        GenerateBuses();

        entities = [.._chipEntities];
        entities.AddRange(_busEntities);
        components = [.._generatedChipComponents];
        components.AddRange(_generatedBusComponents);
    }

    #endregion

    #region Private Methods

    private void GenerateChips()
    {
        _chipEntities.Clear();
        _generatedChipComponents.Clear();

        for (var chipIdx = 0; chipIdx < _chipCount; chipIdx += 1)
        {
            var w = _rng.Next(_minChipSize, _maxChipSize);
            var h = _rng.Next(_minChipSize, _maxChipSize);
            var x = _rng.Next(2, _worldWidth - w - 2);
            var y = _rng.Next(2, _worldHeight - h - 2);

            var tries = 0;
            var isInvalid = true;
            var chipEntity = new EntityBase();
            App.Chip? newChip = null;
            while (isInvalid && tries < 10)
            {
                newChip = new App.Chip(chipEntity.Id, new Vector2(x, y), new Vector2(x + w, y + h));
                isInvalid = _generatedChipComponents.Exists(c => newChip.IsIntersecting(c));
                tries += 1;
            }

            if (isInvalid || newChip == null)
                continue;

            _generatedChipComponents.Add(newChip);
            _chipEntities.Add(chipEntity);

            for (var j = (int)newChip.Position1.Y; j <= newChip.Position2.Y; j += 1)
                for (var i = (int)newChip.Position1.X; i <= newChip.Position2.X; i += 1)
                    Occupy(i, j);
        }

        // order chips by size, largest first
        _generatedChipComponents.Sort(Comparer<App.Chip>.Create((a, b) =>
        {
            // var aArea = Math.Abs(a.Position2.X - a.Position1.X) *
            //             Math.Abs(a.Position2.Y - a.Position2.Y);
            // var bArea = Math.Abs(b.Position2.X - b.Position1.X) *
            //             Math.Abs(b.Position2.Y - b.Position2.Y);
            // return aArea.CompareTo(bArea);
            var sideLengthA = Math.Max(Math.Abs(a.Position2.X - a.Position1.X),
                Math.Abs(a.Position2.Y - a.Position1.Y));
            var sideLengthB = Math.Max(Math.Abs(b.Position2.X - b.Position1.X),
                Math.Abs(b.Position2.Y - b.Position1.Y));
            return sideLengthA.CompareTo(sideLengthB);
        }));
        //_generatedChips.Reverse();
    }

    private void GenerateBuses()
    {
        var pairedChips = new PriorityQueue<(App.Chip, App.Chip), float>();
        // Connect each chip to the nearest unconnected one.
        foreach (var thisChip in _generatedChipComponents)
        {
            var availableChips =
                _generatedChipComponents
                    .FindAll(c =>
                    !thisChip.Equals(c) && !IsConnected(thisChip, c)).ToList();

            var nearestChip =
                availableChips.MinBy(c =>
                    thisChip.Equals(c)
                        ? float.PositiveInfinity
                        : Vector2.Distance(thisChip.Center(), c.Center()));
            if (nearestChip == null) continue;

            if (!_adjacency.TryGetValue(thisChip, out var connectedToThisChip))
                connectedToThisChip = new HashSet<App.Chip>();
            connectedToThisChip.Add(nearestChip);
            _adjacency[thisChip] = connectedToThisChip;
            if (!_adjacency.TryGetValue(nearestChip, out var connectedToNearestChip))
                connectedToNearestChip = new HashSet<App.Chip>();
            connectedToNearestChip.Add(thisChip);
            _adjacency[nearestChip] = connectedToNearestChip;

            // var dist = -1 * Vector2.Distance(thisChip.Center(), nearestChip.Center());
            var weight = Math.Min(
                Math.Abs(thisChip.Center().X - nearestChip.Center().X),
                Math.Abs(thisChip.Center().Y - nearestChip.Center().Y)
            );
            pairedChips.Enqueue((thisChip, nearestChip), weight);
        }

        while (pairedChips.Count > 0)
        {
            var (chipA, chipB) = pairedChips.Dequeue();
            ConnectChips(chipA, chipB);
        }
    }

    private bool IsConnected(App.Chip startChip, App.Chip goalChip)
    {
        var toExplore = new Queue<App.Chip>();
        var explored = new HashSet<App.Chip>();

        toExplore.Enqueue(startChip);
        while (toExplore.Count > 0)
        {
            var nextChip = toExplore.Dequeue();
            if (nextChip.Equals(goalChip)) return true;

            if (explored.Contains(nextChip)
                || !_adjacency.TryGetValue(nextChip, out var connectedChips)) continue;

            foreach (var c in connectedChips)
            {
                if (explored.Contains(c)) continue;

                toExplore.Enqueue(c);
            }

            explored.Add(nextChip);
        }

        return false;
    }

    private void ConnectChips(App.Chip thisChip, App.Chip otherChip)
    {
        // connectedChips.Add(nearestChip);
        var chipCenter = thisChip.Center();
        var nearestCenter = otherChip.Center();
        var vertical = chipCenter.Y - nearestCenter.Y;
        var horizontal = nearestCenter.X - chipCenter.X;

        // Rank chip walls by horizontal and vertical parameters and choose the best ones that
        // have space for wires.
        var startWalls = new PriorityQueue<ArraySegment<App.Cell>, float>(4);
        var endWalls = new PriorityQueue<ArraySegment<App.Cell>, float>(4);

        startWalls.Enqueue(thisChip.LowerWall(), vertical);
        startWalls.Enqueue(thisChip.UpperWall(), vertical * -1);
        startWalls.Enqueue(thisChip.LeftWall(), horizontal);
        startWalls.Enqueue(thisChip.RightWall(), horizontal * -1);

        endWalls.Enqueue(otherChip.LowerWall(), vertical * -1);
        endWalls.Enqueue(otherChip.UpperWall(), vertical);
        endWalls.Enqueue(otherChip.LeftWall(), horizontal * -1);
        endWalls.Enqueue(otherChip.RightWall(), horizontal);

        var width = _rng.Next(_minBusWidth, _maxBusWidth);
        var busStart = CreateBusTerminator(width, startWalls);
        var busEnd = CreateBusTerminator(width, endWalls);

        if (busStart.Count == 0 || busEnd.Count == 0) return;

        CreateBus(width, busStart, busEnd);
    }

    private void CreateBus(int width, IList<(int, int)> busStart, IList<(int, int)> busEnd)
    {
        var wires = new List<App.Wire>();
        for (var i = 0; i < width; i += 1)
        {
            var origin = new Vector2(busStart[i].Item1, busStart[i].Item2);
            var goal = new Vector2(busEnd[i].Item1, busEnd[i].Item2);
            var aStar = new AStar(
                _worldWidth,
                _worldHeight,
                origin,
                goal)
            {
                Heuristic = loc =>
                {
                    var isOccupied = IsOccupied((int)loc.X, (int)loc.Y);
                    var isEndPoint = loc.Equals(origin) || loc.Equals(goal);
                    var penalty = isOccupied && !isEndPoint ? float.PositiveInfinity : 0;
                    return Vector2.Distance(loc, goal) + penalty;
                }
            };

            aStar.Weight = (loc, neighbor) =>
            {
                // penalise occupied locations
                var isOccupied = IsOccupied((int)neighbor.X, (int)neighbor.Y);
                var isNeighborEndPoint = neighbor.Equals(origin) || neighbor.Equals(goal);
                var occupationPenalty = isOccupied && !isNeighborEndPoint
                    ? float.PositiveInfinity
                    : 0;
                // penalise turning corners
                var prev = aStar.CameFrom(loc);
                var isTurning = Math.Abs(prev.X - neighbor.X) > 0.5 &&
                                Math.Abs(prev.Y - neighbor.Y) > 0.5;
                var turningPenalty = isTurning ? 2.0f : 1.0f;
                // Create a weight that is biased towards cheaper cost in the middle of the path.
                // This causes the path finding to avoid making turns near the start and end
                // points, which in turn creates more space for the ending of other nearby paths.
                var avgMidPoint = Vector2.Distance(origin, goal) / 2.0f;
                var distToMid = Math.Abs(avgMidPoint - Vector2.Distance(origin, neighbor));
                var weight = distToMid * turningPenalty + occupationPenalty;
                return weight;
            };

            var path = aStar.ComputePath();

            if (path == null) continue;

            var wire =
                new App.Wire(path.ToList().ConvertAll(vec => ((int)vec.X, (int)vec.Y)));
            foreach (var (x, y, _) in wire.Outline) Occupy(x, y);
            wires.Add(wire);
        }

        //return wires.Count == 0 ? null : new App.Bus(wires);
        if (wires.Count > 0)
        {
            var busEntity = new EntityBase();
            var busComponent = new App.Bus(busEntity.Id, wires);
            _generatedBusComponents.Add(busComponent);
            _busEntities.Add(busEntity);
        }
    }

    private List<(int, int)> CreateBusTerminator(
        int width,
        in PriorityQueue<ArraySegment<App.Cell>, float> cellWalls)
    {
        // Idea:
        // - take a start wall from the queue
        // - use two indices, a and b, to find a fitting unoccupied gap in the wall
        // - set all cells of that gap to occupied and create a list of coordinates
        // - do the same for end walls
        // - initialise a new set of wires with the given coordinate sets
        // - create a bus from all the wires and return

        var isDone = false;
        var busEndCells = new List<(int, int)>();
        while (!isDone && cellWalls.Count > 0)
        {
            var cellWall = cellWalls.Dequeue();
            var wallLength = cellWall.Count;
            var a = 0;
            var b = 1;
            var foundSpace = false;
            while (!foundSpace)
            {
                // step 1: find a start point
                while (a < wallLength && IsConnected(cellWall[a].X, cellWall[a].Y))
                    a += 1;

                if (a == wallLength) break;

                // step 2: try set end point at <width>
                b = a + 1;
                while (b <= wallLength
                       && b < a + width
                       && !IsConnected(cellWall[b - 1].X, cellWall[b - 1].Y))
                    b += 1;

                if (b > wallLength) break; // reached the end of the wall

                if (b - a != width) // current gap in the wall is too small, try next one
                {
                    b += 1;
                    a = b;
                    continue;
                }

                // check whether we have enough space
                foundSpace = true;
            }

            if (!foundSpace) continue; // this wall did not have enough space, try the next one

            // Mark cells used for bus end as occupied.
            for (var i = a; i < b; i += 1)
            {
                var wallCell = cellWall[i];
                Occupy(wallCell.X, wallCell.Y);
                busEndCells.Add((wallCell.X, wallCell.Y));
                isDone = true;
            }
        }

        return busEndCells;
    }

    private void Occupy(int x, int y)
    {
        _occupation[x, y] += 1;
    }

    private bool IsOccupied(int x, int y)
    {
        return _occupation[x, y] > 0;
    }

    private bool IsConnected(int x, int y)
    {
        return _occupation[x, y] > 1;
    }

    #endregion
}