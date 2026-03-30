using System.Numerics;
using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Io;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Greenery.Ui;

public enum MapRenderMode
{
    ElevationColor,
    ElevationMonochrome,
    HeatMapColor,
    HeatMapMonochrome,
    ContourColor,
    ContourMonochrome,
    TerrainColor,
    TerrainMonochrome,
    ReliefColor,
    ReliefMonochrome,
    SurfaceFeatures,
    Rivers,
    Temperature,
    Humidity,
    Biomes,
    TemperatureAmplitude
}

public class MapView : KeyInputProcessorBase, IEventSink
{
    #region Fields

    #region Color Constants

    private static readonly ConsoleColor DefaultBg = Console.BackgroundColor;
    private static readonly ConsoleColor DefaultFg = Console.ForegroundColor;

    private static readonly char[] MarkersElevation =
        ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    private static readonly char[] MarkersTerrain =
    [
        Cp437.Tilde,
        Cp437.Tilde,
        Cp437.Approximation,
        Cp437.Approximation,
        Cp437.SparseShade,
        Cp437.BoxDoubleUpHorizontal,
        Cp437.BoxUpHorizontal,
        Cp437.Intersection,
        Cp437.Caret,
        Cp437.TriangleUp
    ];

    private static readonly char[] MarkersHeatmapColor =
    [
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade,
        Cp437.DenseShade
    ];

    private static readonly char[] MarkersHeatmapMonochrome =
    [
        Cp437.BlockFull,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.BlockFull
    ];

    private static readonly char[] MarkersSurfaceFeatures =
    [
        Cp437.WhiteSpace, // None
        Cp437.Tilde, // River
        Cp437.MediumShade, // Glacier
        Cp437.BlockFull, // Lava
        Cp437.TriangleUp, // Mountain
        Cp437.Solar, // Snow
        Cp437.Dot, // Beach
        Cp437.BoxDoubleUpHorizontal, // Cliff
        Cp437.Pipe, // Fjord
        Cp437.BulletHollow, // Crater
        Cp437.Percent, // Ash
        Cp437.Asterisk, // Cinder
        Cp437.Plus, // Caldera
        Cp437.Rectangle, // Shield
        Cp437.Intersection // Stratovolcano
    ];

    private static readonly char[] MarkersBiome =
    [
        Cp437.Tilde, // Ocean
        't', // Tundra
        'T', // Taiga
        'F', // TemperateForest
        'g', // Grassland
        'd', // Desert
        'J', // TropicalForest
        's', // Savanna
        'I' // IceCap
    ];

    private static readonly char[] MarkersScalar =
    [
        Cp437.WhiteSpace,
        Cp437.Dot,
        Cp437.Comma,
        Cp437.SparseShade,
        Cp437.MediumShade,
        Cp437.DenseShade,
        Cp437.BlockFull,
        Cp437.Plus,
        Cp437.Hash,
        Cp437.TriangleUp
    ];

    private static readonly (ConsoleColor, ConsoleColor)[] ColorsElevation =
    [
        (ConsoleColor.DarkBlue, DefaultBg),
        (ConsoleColor.Blue, DefaultBg),
        (ConsoleColor.DarkCyan, DefaultBg),
        (ConsoleColor.Cyan, DefaultBg),
        (ConsoleColor.Yellow, DefaultBg),
        (ConsoleColor.DarkGreen, DefaultBg),
        (ConsoleColor.Green, DefaultBg),
        (ConsoleColor.DarkYellow, DefaultBg),
        (ConsoleColor.DarkGray, DefaultBg),
        (ConsoleColor.Gray, DefaultBg)
    ];

    private static readonly (ConsoleColor, ConsoleColor)[] ColorsElevationMonochrome =
    [
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg),
        (DefaultFg, DefaultBg)
    ];

    private static readonly (ConsoleColor, ConsoleColor)[] ColorsHeatmapMonochrome =
    [
        (ConsoleColor.Black, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.DarkGray, ConsoleColor.Black),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.Gray, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray),
        (ConsoleColor.White, ConsoleColor.DarkGray)
    ];

    #endregion

    #region Positioning Constants and Variables

    // World size
    private readonly int _worldWidth;
    private readonly int _worldHeight;

    // Offsets for the Map rendering, to accommodate left and top scales
    private const int SpaceForScaleTop = 1;
    private const int SpaceForTextfieldBottom = 1;
    private int _spaceForScaleLeft;

    private int _viewportPositionInWorldX;
    private int _viewportPositionInWorldY;

    #endregion

    // reference to canvas to render on
    private readonly ConsoleCanvas _canvas;

    // cached world and drone paths
    private readonly (byte, char)[,] _cachedWorld;
    private readonly bool[,] _cachedFov;
    private readonly Dictionary<int, Vector2> _cachedDronePositions;
    private readonly Dictionary<int, List<(int, int, char)>> _cachedDronePaths;

    // rendering options
    private MapRenderMode _mapRenderMode = MapRenderMode.ElevationColor;

    #endregion

    #region Constructor

    public MapView(ConsoleCanvas canvas, int worldWidth, int worldHeight)
    {
        _canvas = canvas;
        _canvas.AutoResize = true;
        // _canvas.Interlaced = true;

        _cachedWorld = new (byte, char)[worldWidth, worldHeight];
        _cachedFov = new bool[worldWidth, worldHeight];
        _cachedDronePaths = new Dictionary<int, List<(int, int, char)>>();
        _cachedDronePositions = new Dictionary<int, Vector2>();

        _worldWidth = worldWidth;
        _worldHeight = worldHeight;

        ViewportPositionInWorldX = 0;
        ViewportPositionInWorldY = 0;

        Console.CursorVisible = false;
    }

    #endregion

    #region Private Properties

    private MapRenderMode MapRenderMode
    {
        get => _mapRenderMode;
        set
        {
            _mapRenderMode = value;
            IsRequireReRender = true;
        }
    }

    private int ViewportWidth => Width - _spaceForScaleLeft;
    private int ViewportHeight => Height - SpaceForScaleTop - SpaceForTextfieldBottom;

    // Left top position of the camera within the world
    private int ViewportPositionInWorldX
    {
        get => _viewportPositionInWorldX;
        set
        {
            if (_viewportPositionInWorldX == value) return;

            _viewportPositionInWorldX = value;
            IsRequireReRender = true;
        }
    }

    private int ViewportPositionInWorldY
    {
        get => _viewportPositionInWorldY;
        set
        {
            if (_viewportPositionInWorldY == value) return;

            _viewportPositionInWorldY = value;
            UpdateSpaceForScaleLeft();
            IsRequireReRender = true;
        }
    }

    #endregion

    #region IUiElement Members

    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        if (!componentStorage.TryGetSingleForType<WorldComponent>(out var world) ||
            world == null) return;
        if (!componentStorage.TryGetSingleForType<FovComponent>(out var fov) || fov == null) return;

        switch (MapRenderMode)
        {
            case MapRenderMode.ElevationColor:
            case MapRenderMode.ElevationMonochrome:
                SetElevationVisual(world);
                break;
            case MapRenderMode.HeatMapColor:
                SetHeatmapColorVisual(world);
                break;
            case MapRenderMode.HeatMapMonochrome:
                SetHeatmapMonochromeVisual(world);
                break;
            case MapRenderMode.ContourColor:
            case MapRenderMode.ContourMonochrome:
                SetContourLines(world);
                break;
            case MapRenderMode.TerrainColor:
            case MapRenderMode.TerrainMonochrome:
                SetTerrainVisual(world);
                break;
            case MapRenderMode.ReliefColor:
            case MapRenderMode.ReliefMonochrome:
                SetReliefVisual(world);
                break;
            case MapRenderMode.SurfaceFeatures:
                SetSurfaceFeatureVisual(world);
                break;
            case MapRenderMode.Rivers:
                SetRiversVisual(world);
                break;
            case MapRenderMode.Temperature:
                SetTemperatureVisual(world);
                break;
            case MapRenderMode.Humidity:
                SetHumidityVisual(world);
                break;
            case MapRenderMode.Biomes:
                SetBiomesVisual(world);
                break;
            case MapRenderMode.TemperatureAmplitude:
                SetTemperatureAmplitudeVisual(world);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                if (_cachedFov[x, y] == fov.Cells[x, y]) continue;
                IsRequireReRender = true;
                _cachedFov[x, y] = fov.Cells[x, y];
            }

        foreach (var drone in componentStorage.GetAllForType<DroneComponent>())
        {
            if (drone.Path != null)
            {
                if (_cachedDronePaths.TryGetValue(drone.EntityId, out var path))
                {
                    if (path.Count != drone.CachedPathVisual.Count) IsRequireReRender = true;

                    path.Clear();
                    path.AddRange(drone.CachedPathVisual);
                }
                else
                {
                    _cachedDronePaths.Add(drone.EntityId, [.. drone.CachedPathVisual]);
                    IsRequireReRender = true;
                }
            }

            _cachedDronePositions[drone.EntityId] = drone.Position;
        }
    }

    public override void Render()
    {
        if (!IsRequireReRender) return;

        var viewportExtendInWorldY = ViewportPositionInWorldY + ViewportHeight;
        var boundaryY = Math.Min(_worldHeight, viewportExtendInWorldY);

        var colors = MapRenderMode switch
        {
            MapRenderMode.ElevationColor => ColorsElevation,
            MapRenderMode.HeatMapColor => ColorsElevation,
            MapRenderMode.ContourColor => ColorsElevation,
            MapRenderMode.TerrainColor => ColorsElevation,
            MapRenderMode.ReliefColor => ColorsElevation,
            MapRenderMode.SurfaceFeatures => ColorsElevation,
            MapRenderMode.Rivers => ColorsElevation,
            MapRenderMode.Temperature => ColorsElevation,
            MapRenderMode.Humidity => ColorsElevation,
            MapRenderMode.Biomes => ColorsElevation,
            MapRenderMode.TemperatureAmplitude => ColorsElevation,
            MapRenderMode.ElevationMonochrome => ColorsElevationMonochrome,
            MapRenderMode.HeatMapMonochrome => ColorsHeatmapMonochrome,
            MapRenderMode.ContourMonochrome => ColorsElevationMonochrome,
            MapRenderMode.TerrainMonochrome => ColorsElevationMonochrome,
            MapRenderMode.ReliefMonochrome => ColorsElevationMonochrome,
            _ => throw new ArgumentOutOfRangeException()
        };

        // Step 1: Render World
        for (var y = ViewportPositionInWorldY; y < boundaryY; y++)
            for (var x = 0; x < ViewportWidth; x++)
            {
                var worldX = ViewportToWorldX(x);
                var (elevation, c) = _cachedWorld[worldX, y];
                // Deactivate fov for debugging.
                // TODO: Reactivate.
                var isFov = true; // _cachedFov[worldX, y];
                var (colFg, colBg) = colors[elevation];
                _canvas.Set(
                    X + x + _spaceForScaleLeft,
                    Y + WorldToViewportY(y) + SpaceForScaleTop,
                    c,
                    isFov ? colFg : DefaultFg,
                    isFov ? colBg : DefaultBg);
            }

        // Step 2: Render drones and drone paths
        foreach (var path in _cachedDronePaths.Values)
            foreach (var (pathX, pathY, pathCol) in path)
                if (IsInCamera(pathX, pathY))
                    _canvas.Set(
                        X + WorldToViewportX(pathX) + _spaceForScaleLeft,
                        Y + WorldToViewportY(pathY) + SpaceForScaleTop,
                        pathCol,
                        ConsoleColor.Red,
                        DefaultBg);

        foreach (var pos in _cachedDronePositions.Values)
        {
            var droneX = Convert.ToInt32(pos.X);
            var droneY = Convert.ToInt32(pos.Y);
            if (IsInCamera(droneX, droneY))
                _canvas.Set(
                    X + WorldToViewportX(droneX) + _spaceForScaleLeft,
                    Y + WorldToViewportY(droneY) + SpaceForScaleTop,
                    '@',
                    DefaultBg,
                    ConsoleColor.Red);
        }

        // Step 3: Render Coordinate Scales at the top and left sides.
        RenderCoordinates();
        RenderOverlay();
    }

    protected override void OnXChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnYChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnWidthChanged()
    {
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnHeightChanged()
    {
        UpdateSpaceForScaleLeft();
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    #endregion

    #region IEventSink Members

    public void ProcessEvent(IEvent evt)
    {
        if (evt is Event<MapRenderMode>(var renderMode)) MapRenderMode = renderMode;
    }

    #endregion

    #region KeyInputProcessorBase Members

    public override void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCameraUp();
                return;
            case ConsoleKey.DownArrow:
                MoveCameraDown();
                return;
            case ConsoleKey.LeftArrow:
                MoveCameraLeft();
                return;
            case ConsoleKey.RightArrow:
                MoveCameraRight();
                return;
            case ConsoleKey.Q:
                MapRenderMode = MapRenderMode.ElevationColor;
                return;
            case ConsoleKey.W:
                MapRenderMode = MapRenderMode.SurfaceFeatures;
                return;
            case ConsoleKey.E:
                MapRenderMode = MapRenderMode.Temperature;
                return;
            case ConsoleKey.R:
                MapRenderMode = MapRenderMode.Humidity;
                return;
            case ConsoleKey.T:
                MapRenderMode = MapRenderMode.Biomes;
                return;
            case ConsoleKey.U:
                MapRenderMode = MapRenderMode.Rivers;
                return;
            case ConsoleKey.Y:
                MapRenderMode = MapRenderMode.TemperatureAmplitude;
                return;
            default:
                return;
        }
    }

    #endregion

    #region Private Members

    private void UpdateSpaceForScaleLeft()
    {
        _spaceForScaleLeft = (ViewportPositionInWorldY + ViewportHeight) switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1000 => 3,
            < 10000 => 4,
            _ => 5
        };
    }

    private void MoveCameraUp()
    {
        ViewportPositionInWorldY = ViewportHeight > _worldHeight
            ? 0
            : Math.Max(ViewportPositionInWorldY - 1, 0);
    }

    private void MoveCameraDown()
    {
        var maxViewportY = ViewportPositionInWorldY + ViewportHeight - 1;
        var maxWorldY = _worldHeight - ViewportHeight - 1;
        var boundaryY = Math.Min(maxViewportY, maxWorldY);
        ViewportPositionInWorldY = ViewportHeight > _worldHeight
            ? 0
            : Math.Min(ViewportPositionInWorldY + 1, boundaryY);
    }

    private void MoveCameraLeft()
    {
        ViewportPositionInWorldX = ViewportWidth > _worldWidth
            ? 0
            : WrapWorldX(ViewportPositionInWorldX - 1);
    }

    private void MoveCameraRight()
    {
        ViewportPositionInWorldX = ViewportWidth > _worldWidth
            ? 0
            : WrapWorldX(ViewportPositionInWorldX + 1);
    }

    /// <summary>
    /// Determine whether a position is within the viewport boundaries.
    /// </summary>
    /// <param name="x">x-position relative to world coordinates</param>
    /// <param name="y">y-position relative to world coordinates</param>
    /// <returns><c>true</c> if it is within the viewport, <c>false</c> otherwise.</returns>
    private bool IsInCamera(float x, float y)
    {
        if (y < ViewportPositionInWorldY || y >= ViewportPositionInWorldY + ViewportHeight)
            return false;

        var worldX = Convert.ToInt32(x);
        var dx = (worldX - ViewportPositionInWorldX + _worldWidth) % _worldWidth;
        return dx >= 0 && dx < ViewportWidth;
    }

    /// <summary>
    /// Determine whether a position is within the world or not
    /// </summary>
    /// <param name="x">x-position relative to world coordinates</param>
    /// <param name="y">y-position relative to world coordinates</param>
    /// <returns></returns>
    private bool IsInBounds(float x, float y)
    {
        return x >= 0
               && x < _worldWidth
               && y >= 0
               && y < _worldHeight;
    }

    private int WorldToViewportX(float x)
    {
        var worldX = Convert.ToInt32(x);
        return (worldX - ViewportPositionInWorldX + _worldWidth) % _worldWidth;
    }

    private int WorldToViewportY(float y)
    {
        return Convert.ToInt32(y - ViewportPositionInWorldY);
    }

    private int ViewportToWorldX(int x)
    {
        return WrapWorldX(ViewportPositionInWorldX + x);
    }

    private int ViewportToWorldY(int y)
    {
        return ViewportPositionInWorldY + y;
    }

    private int WrapWorldX(int x)
    {
        return (x % _worldWidth + _worldWidth) % _worldWidth;
    }

    private void RenderCoordinates()
    {
        for (var x = 0; x < _spaceForScaleLeft; x++)
            _canvas.Set(X + x, Y, Cp437.BlockFull, DefaultBg);

        // Horizontal
        // tick marks
        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            _canvas.Set(X + _spaceForScaleLeft + x, Y, Cp437.BlockFull, fg);
        }

        // tick labels
        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;

            if (!isTick) continue;

            var spaceForLabel = Width - x - _spaceForScaleLeft;
            var tickLabel = Convert.ToString(worldX);
            if (tickLabel.Length > spaceForLabel) tickLabel = tickLabel[..spaceForLabel];
            _canvas.Text(X + _spaceForScaleLeft + x, Y, tickLabel, false, DefaultBg, DefaultFg);
        }

        // Vertical
        // tick marks
        for (var y = 0; y <= ViewportHeight; y++)
            for (var x = 0; x < _spaceForScaleLeft; x++)
            {
                var worldY = ViewportToWorldY(y);
                var isTick = worldY > 0 && worldY % 5 == 0;
                var fg = isTick ? DefaultFg : DefaultBg;
                _canvas.Set(X + x, y + SpaceForScaleTop, Cp437.BlockFull, fg);
            }

        // tick labels
        for (var y = 0; y <= ViewportHeight; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                _canvas.Text(X, y + SpaceForScaleTop, Convert.ToString(worldY), false, DefaultBg,
                    DefaultFg);
        }
    }

    private void RenderOverlay()
    {
        var maxWidth = Math.Max(0, Width - _spaceForScaleLeft);
        var keyMap =
            "Keys: Q=Elevation | W=Surface | U=Rivers | E=Temperature | R=Humidity | T=Biomes | Y=TempAmp";
        var legend = GetLegendText(MapRenderMode);

        // Don't show keymap for the time being.
        // _canvas.Text(X + _spaceForScaleLeft, Y + Height - 2, ClipToWidth(keyMap, maxWidth), false, DefaultBg, DefaultFg);
        _canvas.Text(X + _spaceForScaleLeft, Y + Height - 1, ClipToWidth(legend, maxWidth), false,
            DefaultBg, DefaultFg);
    }

    private static string ClipToWidth(string text, int maxWidth)
    {
        return text.Length <= maxWidth ? text : text[..maxWidth];
    }

    private static string GetLegendText(MapRenderMode mode)
    {
        return mode switch
        {
            MapRenderMode.SurfaceFeatures =>
                "Legend: ~ River | ^ Mountain | s Snow | . Beach | # Lava",
            MapRenderMode.Rivers => "Legend: ~ River",
            MapRenderMode.Biomes =>
                "Legend: ~ Ocean | t Tundra | T Taiga | F TemperateForest | g Grassland | d Desert | J TropicalForest | s Savanna | I IceCap",
            MapRenderMode.Temperature => "Legend: 0..9 = Temperature",
            MapRenderMode.Humidity => "Legend: 0..9 = Humidity",
            MapRenderMode.TemperatureAmplitude => "Legend: 0..9 = Temp Amplitude",
            MapRenderMode.ElevationColor or MapRenderMode.ElevationMonochrome =>
                "Legend: 0..9 = Elevation",
            MapRenderMode.HeatMapColor or MapRenderMode.HeatMapMonochrome =>
                "Legend: shade = Heatmap",
            MapRenderMode.ContourColor or MapRenderMode.ContourMonochrome =>
                "Legend: contour marks elevation changes",
            MapRenderMode.TerrainColor or MapRenderMode.TerrainMonochrome =>
                "Legend: terrain character map",
            MapRenderMode.ReliefColor or MapRenderMode.ReliefMonochrome =>
                "Legend: relief and cliffs",
            _ => "Legend: map visualization"
        };
    }

    private void SetRiversVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                _cachedWorld[x, y] = world.Rivers[x, y]
                    ? ((byte)1, Cp437.Tilde)
                    : ((byte)0, Cp437.WhiteSpace);
    }

    private void SetElevationVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                _cachedWorld[x, y] = (world.Cells[x, y], MarkersElevation[world.Cells[x, y]]);
    }

    private void SetHeatmapColorVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                _cachedWorld[x, y] = (world.Cells[x, y], MarkersHeatmapColor[world.Cells[x, y]]);
    }

    private void SetHeatmapMonochromeVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                _cachedWorld[x, y] = (world.Cells[x, y], MarkersHeatmapMonochrome[world.Cells[x, y]]);
    }

    private void SetTerrainVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
                _cachedWorld[x, y] = (world.Cells[x, y], MarkersTerrain[world.Cells[x, y]]);
    }

    private void SetReliefVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                char c;
                if (IsInBounds(x - 1, y) && world.Cells[x - 1, y] < world.Cells[x, y] &&
                    world.Cells[x, y] > 3)
                    c = Cp437.Slash;
                else if (IsInBounds(x + 1, y) && world.Cells[x + 1, y] < world.Cells[x, y] &&
                         world.Cells[x, y] > 3)
                    c = Cp437.BackSlash;
                else if (world.Cells[x, y] <= 3)
                    c = Cp437.Tilde;
                else
                    c = Cp437.Interpunct;

                _cachedWorld[x, y] = (world.Cells[x, y], c);
            }
    }

    private void SetSurfaceFeatureVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var feature = world.Surfaces[x, y];
                var index = Math.Min((int)feature, MarkersSurfaceFeatures.Length - 1);
                var charIndex = Math.Min(index, 9);
                _cachedWorld[x, y] = ((byte)charIndex, MarkersSurfaceFeatures[index]);
            }
    }

    private void SetTemperatureVisual(WorldComponent world)
    {
        var (min, max) = GetRange(world.Temperature);
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = GetScalarIndex(world.Temperature[x, y], min, max);
                _cachedWorld[x, y] = ((byte)index, MarkersScalar[index]);
            }
    }

    private void SetHumidityVisual(WorldComponent world)
    {
        var (min, max) = GetRange(world.Humidity);
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = GetScalarIndex(world.Humidity[x, y], min, max);
                _cachedWorld[x, y] = ((byte)index, MarkersScalar[index]);
            }
    }

    private void SetBiomesVisual(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var biome = world.Biomes[x, y];
                var index = Math.Min((int)biome, MarkersBiome.Length - 1);
                var charIndex = Math.Min(index, 9);
                _cachedWorld[x, y] = ((byte)charIndex, MarkersBiome[index]);
            }
    }

    private void SetTemperatureAmplitudeVisual(WorldComponent world)
    {
        var (min, max) = GetRange(world.TemperatureAmplitude);
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var index = GetScalarIndex(world.TemperatureAmplitude[x, y], min, max);
                _cachedWorld[x, y] = ((byte)index, MarkersScalar[index]);
            }
    }

    private static int GetScalarIndex(float value, float min, float max)
    {
        if (float.IsNaN(value)) return 0;
        if (max <= min) return 0;
        var normalized = (value - min) / (max - min);
        return Math.Clamp((int)MathF.Floor(normalized * 9.0f), 0, 9);
    }

    private static (float min, float max) GetRange(float[,] values)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var y = 0; y < values.GetLength(1); y++)
            for (var x = 0; x < values.GetLength(0); x++)
            {
                var value = values[x, y];
                if (float.IsNaN(value)) continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

        if (min == float.MaxValue || max == float.MinValue)
            return (0f, 1f);

        if (min == max)
            return (min - 1f, max + 1f);

        return (min, max);
    }

    private void SetContourLines(WorldComponent world)
    {
        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var cell = world.Cells[x, y];
                if (cell < 3)
                {
                    _cachedWorld[x, y] = (cell, Cp437.WhiteSpace);
                    continue;
                }

                var c = GetCharFromCliffs(cell, x, y, world);
                _cachedWorld[x, y] = (cell, c);
            }

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var cell = world.Cells[x, y];
                if (cell < 3) continue;

                if (_cachedWorld[x, y].Item2 == 'X') continue;
                var c = GetCliffAdjacentChar(cell, x, y, world);

                _cachedWorld[x, y].Item2 = c;
            }

        for (var y = 0; y < _worldHeight; y++)
            for (var x = 0; x < _worldWidth; x++)
            {
                var cell = world.Cells[x, y];
                if (cell < 3) continue;

                if (_cachedWorld[x, y].Item2 != 'X') continue;
                var c = GetCliffChar(cell, x, y, world);

                _cachedWorld[x, y].Item2 = c;
            }
    }

    private char GetCharFromCliffs(byte cell, int x, int y, WorldComponent world)
    {
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north < cell) return 'X';

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east < cell) return 'X';

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south < cell) return 'X';

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west < cell) return 'X';

        return Cp437.WhiteSpace;
    }

    private char GetCliffAdjacentChar(byte cell, int x, int y, WorldComponent world)
    {
        //return _visualByPosition[x, y].Item1;
        var b = 0b_0000_0000;
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north == cell && _cachedWorld[x, y - 1].Item2 == Cp437.UpperX) b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _cachedWorld[x + 1, y].Item2 == Cp437.UpperX) b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _cachedWorld[x, y + 1].Item2 == Cp437.UpperX) b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _cachedWorld[x - 1, y].Item2 == Cp437.UpperX) b |= 0b_0000_0001;

        return b switch
        {
            0 => Cp437.WhiteSpace, // 0000
            1 => Cp437.WhiteSpace, // 0001, west
            2 => Cp437.WhiteSpace, // 0010, south
            3 => Cp437.BoxDownLeft, // 0011, southwest
            4 => Cp437.WhiteSpace, // 0100, east
            5 => Cp437.BoxHorizontal, // 0101, east west
            6 => Cp437.BoxDownRight, // 0110, southeast
            7 => Cp437.BoxDownHorizontal, // 0111, east-south-west
            8 => Cp437.WhiteSpace, // 1000, north
            9 => Cp437.BoxUpLeft, // 1001, northwest
            10 => Cp437.BoxVertical, // 1010, north-south
            11 => Cp437.BoxVerticalLeft, // 1011, north-south-west
            12 => Cp437.BoxUpRight, // 1100, northeast
            13 => Cp437.BoxUpHorizontal, // 1101, north-east-west
            14 => Cp437.BoxVerticalRight, // 1110, north-south-east
            15 => Cp437.BoxVerticalHorizontal, // 1111, all
            _ => '?'
        };
    }

    private char GetCliffChar(byte cell, int x, int y, WorldComponent world)
    {
        //return _visualByPosition[x, y].Item1;
        var b = 0b_0000_0000;
        // north
        byte? north = IsInBounds(x, y - 1) ? world.Cells[x, y - 1] : null;
        if (north == cell && _cachedWorld[x, y - 1].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_1000;

        // east
        byte? east = IsInBounds(x + 1, y) ? world.Cells[x + 1, y] : null;
        if (east == cell && _cachedWorld[x + 1, y].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0100;

        // south
        byte? south = IsInBounds(x, y + 1) ? world.Cells[x, y + 1] : null;
        if (south == cell && _cachedWorld[x, y + 1].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0010;

        // west
        byte? west = IsInBounds(x - 1, y) ? world.Cells[x - 1, y] : null;
        if (west == cell && _cachedWorld[x - 1, y].Item2 != Cp437.WhiteSpace)
            b |= 0b_0000_0001;

        if (b == 0 && _cachedWorld[x, y].Item2 == Cp437.WhiteSpace) return Cp437.WhiteSpace;

        return b switch
        {
            0 => Cp437.BulletHollow, // 0000
            1 => Cp437.BoxHorizontal, // 0001, west
            2 => Cp437.BoxVertical, // 0010, south
            3 => Cp437.BoxDownLeft, // 0011, southwest
            4 => Cp437.BoxHorizontal, // 0100, east
            5 => Cp437.BoxHorizontal, // 0101, east west
            6 => Cp437.BoxDownRight, // 0110, southeast
            7 => Cp437.BoxDownHorizontal, // 0111, east-south-west
            8 => Cp437.BoxVertical, // 1000, north
            9 => Cp437.BoxUpLeft, // 1001, northwest
            10 => Cp437.BoxVertical, // 1010, north-south
            11 => Cp437.BoxVerticalLeft, // 1011, north-south-west
            12 => Cp437.BoxUpRight, // 1100, northeast
            13 => Cp437.BoxUpHorizontal, // 1101, north-east-west
            14 => Cp437.BoxVerticalRight, // 1110, north-south-east
            15 => Cp437.BoxVerticalHorizontal, // 1111, all
            _ => '?'
        };
    }

    #endregion
}