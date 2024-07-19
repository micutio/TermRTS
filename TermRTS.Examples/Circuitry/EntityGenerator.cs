using System.Numerics;

namespace TermRTS.Examples.Circuitry;

internal class EntityGenerator
{
    /// <summary>
    /// Static method for building a pre-fab circuit board
    /// </summary>
    /// <returns>A small complete set of chips and wires</returns>
    internal static IReadOnlyList<EntityBase<App.CircuitComponentTypes>> BuildSmallCircuitBoard()
    {

        var chipEntity1 = new EntityBase<App.CircuitComponentTypes>();
        chipEntity1.AddComponent(
            App.CircuitComponentTypes.Chip,
            new App.Chip(new Vector2(10, 10), new Vector2(15, 15)));

        var chipEntity2 = new EntityBase<App.CircuitComponentTypes>();
        chipEntity2.AddComponent(
            App.CircuitComponentTypes.Chip,
            new App.Chip(new Vector2(20, 20), new Vector2(30, 30)));

        var busEntity1 = new EntityBase<App.CircuitComponentTypes>();
        busEntity1.AddComponent(
            App.CircuitComponentTypes.Bus,
            new App.Bus(
                new List<App.Wire>
                {
                    new App.Wire(
                        new List<(int, int)>
                        {
                            // from the lower chip, first go left
                            (20, 25), (19, 25), (18, 25), (17, 25), (16, 25), (15, 25), (14, 25), (13, 25),
                            // then go up
                            (13, 24), (13, 23), (13, 22), (13, 21), (13, 20), (13, 19), (13, 18), (13, 17),
                            // now go right
                            (14, 17), (15, 17), (16, 17), (17, 17), (18, 17),
                            // go up again
                            (18, 16), (18, 15), (18, 14),
                            // finally, go left and connect to the smaller chip
                            (17, 14), (16, 14), (15, 14)
                        }),
                    new App.Wire(
                        new List<(int, int)>
                        {
                            // from the lower chip, first go left
                            (20, 24), (19, 24), (18, 24), (17, 24), (16, 24), (15, 24), (14, 24),
                            // then go up
                            (14, 23), (14, 22), (14, 21), (14, 20), (14, 19), (14, 18),
                            // now go right
                            (15, 18), (16, 18), (17, 18), (18, 18), (19, 18),
                            // go up again
                            (19, 17), (19, 16), (19, 15), (19, 14), (19, 13),
                            // finally, go left and connect to the smaller chip
                            (18, 13), (17, 13), (16, 13), (15, 13)
                        }),
                    new App.Wire(
                        new List<(int, int)>
                        {
                            // from the lower chip, first go left
                            (20, 23), (19, 23), (18, 23), (17, 23), (16, 23), (15, 23),
                            // then go up
                            (15, 22), (15, 21), (15, 20), (15, 19),
                            // now go right
                            (16, 19), (17, 19), (18, 19), (19, 19), (20, 19),
                            // go up again
                            (20, 18), (20, 17), (20, 16), (20, 15), (20, 14), (20, 13), (20, 12),
                            // finally, go left and connect to the smaller chip
                            (19, 12), (18, 12), (17, 12), (16, 12), (15, 12)
                        })
                }));

        var busEntity2 = new EntityBase<App.CircuitComponentTypes>();
        busEntity2.AddComponent(
            App.CircuitComponentTypes.Bus,
            new App.Bus(
                new List<App.Wire>
                {
                    new App.Wire(new List<(int x, int y)>
                        {
                            // from the upper chip, first go up
                            (14, 10), (14, 9),
                            // then turn right
                            (15, 9), (16, 9), (17, 9), (18, 9), (19, 9), (20, 9), (21, 9),
                            (22, 9), (23, 9), (24, 9), (25, 9), (26, 9), (27, 9), (28, 9),
                            // then turn downwards
                            (28, 10), (28, 11), (28, 12), (28, 13), (28, 14), (28, 15),
                            (28, 16), (28, 17), (28, 18), (28, 19), (28, 20)
                        })
                }));

        var busEntity3 = new EntityBase<App.CircuitComponentTypes>();
        busEntity3.AddComponent(
            App.CircuitComponentTypes.Bus,
            new App.Bus(
                new List<App.Wire>
                {
                    new App.Wire(
                        new List<(int x, int y)>
                        {
                            (7, 7), (7, 8), (8, 8), (8, 7)
                        })
                }));

        return new List<EntityBase<App.CircuitComponentTypes>>
        {
            chipEntity1, chipEntity2, busEntity1, busEntity2, busEntity3
        };
    }

    /// <summary>
    /// Static method for starting a random circuit board creation using Builder Pattern.
    /// The returned <see cref="EntityGenerator"/> instance comes with default values for all its
    /// parameters, but can be customised using the builder pattern.
    /// </summary>
    /// <returns>New <see cref="EntityGenerator"/> instance with default values.</returns>
    internal static EntityGenerator RandomCircuitBoard()
    {
        return new EntityGenerator();
    }

    #region Private Fields

    private Random _rng;
    private int _worldWidth;
    private int _worldHeight;
    private int _chipCount;
    private int _minChipSize;
    private int _maxChipSize;
    private int _busCount;
    private int _minBusWidth;
    private int _maxBusWidth;

    #endregion

    #region Builder Pattern

    private EntityGenerator()
    {
        _rng = new Random();
        _worldWidth = 80;
        _worldHeight = 40;
        _chipCount = 5;
        _minChipSize = 3;
        _maxChipSize = 6;
        _busCount = 10;
        _minBusWidth = 1;
        _maxBusWidth = 3;
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

    internal EntityGenerator WithBusCount(int busCount)
    {
        _busCount = busCount;
        return this;
    }

    internal EntityGenerator WithBusDimensions(int minBusWidth, int maxBusWidth)
    {
        _minBusWidth = minBusWidth;
        _maxBusWidth = maxBusWidth;
        return this;
    }

    internal IReadOnlyList<EntityBase<App.CircuitComponentTypes>> Build()
    {
        // TODO: Implement building procedure
        //      - [x] Initialise grid with world dimensions to keep track of occupied cells
        //      - [ ] Create <chipCount> chips with limited number of retries in case of collisions
        //      - [ ] Each chip should hold references to all connected buses
        //      - [ ] Create buses
        //      - [ ] For each bus pick start and end chip, try not to leave unconnected chips
        //      - [ ] Use A* on occupation grid to generate path for each wire

        var isOccupied = new bool[_worldHeight, _worldWidth]; // occupation matrix: [rows, cols]

        var chips = new List<App.Chip>();
        for (var chipIdx = 0; chipIdx < _chipCount; chipIdx += 1)
        {
            var w = _rng.Next(_minChipSize, _maxChipSize);
            var h = _rng.Next(_minChipSize, _maxChipSize);
            var x = _rng.Next(2, _worldWidth - w - 2);
            var y = _rng.Next(2, _worldHeight - h - 2);

            var tries = 0;
            var isInvalid = true;
            App.Chip? newChip = null;
            while (isInvalid && tries < 10)
            {
                newChip = new App.Chip(new Vector2(x, y), new Vector2(x + w, y + h));
                isInvalid = chips.Exists(newChip.IsIntersecting);
                tries += 1;
            }

            if (isInvalid || newChip == null)
                continue;

            chips.Add(newChip);
            foreach ((int x, int y, char _) tuple in newChip.Outline)
            {
                isOccupied[tuple.y, tuple.x] = true;
            }
        }

        var entities = new List<EntityBase<App.CircuitComponentTypes>>();
        chips
            .ConvertAll(c => new EntityBase<App.CircuitComponentTypes>(App.CircuitComponentTypes.Chip, c))
            .ForEach(entities.Add);

        return entities;
    }

    #endregion
}
