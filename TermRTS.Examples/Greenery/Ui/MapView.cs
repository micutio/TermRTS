using System.Numerics;
using ConsoleRenderer;
using TermRTS.Event;
using TermRTS.Examples.Greenery.Ecs.Component;
using TermRTS.Examples.Greenery.WorldGen;
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
    SurfaceFeatures,
    Rivers,
    Temperature,
    Humidity,
    Biomes,
    TemperatureAmplitude
}

internal readonly struct CellVisual(char marker, ConsoleColor foreground, ConsoleColor background)
{
    internal char GetMarker()
    {
        return marker;
    }

    internal ConsoleColor GetForeground()
    {
        return foreground;
    }

    internal ConsoleColor GetBackground()
    {
        return background;
    }
}

public class MapView : KeyInputProcessorBase, IEventSink
{
    #region Fields

    #region Positioning Constants and Variables

    // World size
    // TODO: Remove world size fields
    private readonly int _worldWidth;
    private readonly int _worldHeight;

    // Offsets for the Map rendering, to accommodate left and top scales
    private const int SpaceForScaleTop = 1;
    private const int SpaceForTextfieldBottom = 1;
    private int _spaceForScaleLeft;

    #endregion

    // reference to canvas to render on
    private readonly ConsoleCanvas _canvas;

    // cached world and drone paths
    // TODO: Change to (TerminalColor, char)[] _cachedWorld;
    private readonly Dictionary<int, Vector2> _cachedDronePositions;
    private readonly Dictionary<int, List<(int, int, char)>> _cachedDronePaths;

    // rendering
    private MapRenderMode _mapRenderMode = MapRenderMode.ElevationColor;
    private readonly ElevationVisualizer _elevationColorVisualizer;
    private readonly ElevationVisualizer _elevationMonochromeVisualizer;
    private readonly ElevationHeatmapVisualizer _heatmapColorVisualizer;
    private readonly ElevationHeatmapVisualizer _heatmapMonochromeVisualizer;
    private readonly SurfaceFeatureVisualizer _surfaceFeatureVisualizer;
    private readonly TemperatureVisualizer _temperatureVisualizer;
    private readonly HumidityVisualizer _humidityVisualizer;
    private readonly TemperatureAmplitudeVisualizer _temperatureAmplitudeVisualizer;
    private readonly BiomeVisualizer _biomeVisualizer;
    private readonly RiverVisualizer _riverVisualizer;

    private readonly FovVisualizer _fovVisualizer;

    private CellVisual[] _cachedWorld;
    private bool[] _cachedFov;

    #endregion

    #region Constructor

    public MapView(ConsoleCanvas canvas, int worldWidth, int worldHeight)
    {
        _canvas = canvas;
        _canvas.AutoResize = true;
        // _canvas.Interlaced = true;

        _cachedWorld = new CellVisual[ViewportWidth * ViewportHeight];
        _cachedFov = new bool[ViewportWidth * ViewportHeight];
        _cachedDronePaths = new Dictionary<int, List<(int, int, char)>>();
        _cachedDronePositions = new Dictionary<int, Vector2>();

        _elevationColorVisualizer = new ElevationVisualizer(Visual.ColorsElevation);
        _elevationMonochromeVisualizer = new ElevationVisualizer(Visual.ColorsElevationMonochrome);
        _heatmapColorVisualizer = new ElevationHeatmapVisualizer(Visual.ColorsElevation);
        _heatmapMonochromeVisualizer =
            new ElevationHeatmapVisualizer(Visual.ColorsElevationMonochrome);

        _surfaceFeatureVisualizer = new SurfaceFeatureVisualizer();
        _temperatureVisualizer = new TemperatureVisualizer();
        _humidityVisualizer = new HumidityVisualizer();
        _temperatureAmplitudeVisualizer = new TemperatureAmplitudeVisualizer();
        _biomeVisualizer = new BiomeVisualizer();
        _riverVisualizer = new RiverVisualizer();

        _fovVisualizer = new FovVisualizer();

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
        get;
        set
        {
            if (field == value) return;

            field = value;
            IsRequireReRender = true;
        }
    }

    private int ViewportPositionInWorldY
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
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
        // TODO: Figure out which chunks we need for rendering the visible map.
        IWorldComponentVisualizer visualizer = _mapRenderMode switch
        {
            MapRenderMode.ElevationColor => _elevationColorVisualizer,
            MapRenderMode.ElevationMonochrome => _elevationMonochromeVisualizer,
            MapRenderMode.HeatMapColor => _heatmapColorVisualizer,
            MapRenderMode.HeatMapMonochrome => _heatmapMonochromeVisualizer,
            MapRenderMode.SurfaceFeatures => _surfaceFeatureVisualizer,
            MapRenderMode.Rivers => _riverVisualizer,
            MapRenderMode.Temperature => _temperatureVisualizer,
            MapRenderMode.Humidity => _humidityVisualizer,
            MapRenderMode.Biomes => _biomeVisualizer,
            MapRenderMode.TemperatureAmplitude => _temperatureAmplitudeVisualizer,
            _ => throw new ArgumentOutOfRangeException()
        };
        var viewX = ViewportPositionInWorldX;
        var viewY = ViewportPositionInWorldY;
        visualizer.SetVisuals(
            componentStorage,
            _cachedWorld,
            viewX,
            viewY,
            ViewportWidth,
            ViewportHeight);

        _fovVisualizer.CacheFov(
            componentStorage,
            _cachedFov,
            viewX,
            viewY,
            ViewportWidth,
            ViewportHeight);

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

        // Step 1: Render World
        for (var y = 0; y < ViewportHeight; y++)
            for (var x = 0; x < ViewportWidth; x++)
            {
                var cellVisual = _cachedWorld[y * ViewportWidth + x];
                // Deactivate fov for debugging.
                // TODO: Reactivate.
                var isFov = _cachedFov[y * ViewportWidth + x];
                _canvas.Set(
                    X + x + _spaceForScaleLeft,
                    Y + y + SpaceForScaleTop,
                    cellVisual.GetMarker(),
                    isFov ? cellVisual.GetForeground() : Visual.DefaultFg,
                    isFov ? cellVisual.GetBackground() : Visual.DefaultBg);
            }

        // Step 2: Render drone paths and drones on top of them.
        foreach (var path in _cachedDronePaths.Values)
            foreach (var (pathX, pathY, pathCol) in path)
            {
                if (IsInCamera(pathX, pathY))
                {
                    _canvas.Set(
                        X + WorldToViewportX(pathX) + _spaceForScaleLeft,
                        Y + WorldToViewportY(pathY) + SpaceForScaleTop,
                        pathCol,
                        ConsoleColor.Red,
                        Visual.DefaultBg);
                }
            }

        foreach (var pos in _cachedDronePositions.Values)
        {
            var droneX = Convert.ToInt32(pos.X);
            var droneY = Convert.ToInt32(pos.Y);
            if (IsInCamera(droneX, droneY))
                _canvas.Set(
                    X + WorldToViewportX(droneX) + _spaceForScaleLeft,
                    Y + WorldToViewportY(droneY) + SpaceForScaleTop,
                    '@',
                    Visual.DefaultBg,
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
        var newSize = Math.Max(0, ViewportWidth * ViewportHeight);
        _cachedWorld = new CellVisual[newSize];
        _cachedFov = new bool[newSize];
        IsRequireReRender = true;
        IsRequireRootReRender = true;
    }

    protected override void OnHeightChanged()
    {
        UpdateSpaceForScaleLeft();
        var newSize = Math.Max(0, ViewportWidth * ViewportHeight);
        _cachedWorld = new CellVisual[newSize];
        _cachedFov = new bool[newSize];
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
            : WorldMath.WrapX(ViewportPositionInWorldX - 1);
    }

    private void MoveCameraRight()
    {
        ViewportPositionInWorldX = ViewportWidth > _worldWidth
            ? 0
            : WorldMath.WrapX(ViewportPositionInWorldX + 1);
    }

    /// <summary>
    /// Determine whether a position is within the viewport boundaries.
    /// </summary>
    /// <param name="worldX">x-position relative to world coordinates</param>
    /// <param name="worldY">y-position relative to world coordinates</param>
    /// <returns><c>true</c> if it is within the viewport, <c>false</c> otherwise.</returns>
    private bool IsInCamera(float worldX, float worldY)
    {
        if (worldY < ViewportPositionInWorldY ||
            worldY >= ViewportPositionInWorldY + ViewportHeight)
            return false;

        var intX = Convert.ToInt32(worldX);
        var dx = (intX - ViewportPositionInWorldX + _worldWidth) % _worldWidth;
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
        return WorldMath.WrapX(ViewportPositionInWorldX + x);
    }

    private int ViewportToWorldY(int y)
    {
        return ViewportPositionInWorldY + y;
    }

    private void RenderCoordinates()
    {
        for (var x = 0; x < _spaceForScaleLeft; x++)
            _canvas.Set(X + x, Y, Cp437.BlockFull, Visual.DefaultBg);

        // Horizontal
        // tick marks
        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var fg = isTick ? Visual.DefaultFg : Visual.DefaultBg;
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
            _canvas.Text(
                X + _spaceForScaleLeft + x,
                Y,
                tickLabel,
                false,
                Visual.DefaultBg,
                Visual.DefaultFg);
        }

        // Vertical
        // tick marks
        for (var y = 0; y <= ViewportHeight; y++)
            for (var x = 0; x < _spaceForScaleLeft; x++)
            {
                var worldY = ViewportToWorldY(y);
                var isTick = worldY > 0 && worldY % 5 == 0;
                var fg = isTick ? Visual.DefaultFg : Visual.DefaultBg;
                _canvas.Set(X + x, y + SpaceForScaleTop, Cp437.BlockFull, fg);
            }

        // tick labels
        for (var y = 0; y <= ViewportHeight; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                _canvas.Text(
                    X,
                    y + SpaceForScaleTop,
                    Convert.ToString(worldY),
                    false,
                    Visual.DefaultBg,
                    Visual.DefaultFg);
        }
    }

    private void RenderOverlay()
    {
        var maxWidth = Math.Max(0, Width - _spaceForScaleLeft);
        // var keyMap =
        //     "Keys: Q=Elevation | W=Surface | U=Rivers | E=Temperature | R=Humidity | T=Biomes | Y=TempAmp";
        var legend = GetLegendText(MapRenderMode);

        // Don't show keymap for the time being.
        // _canvas.Text(X + _spaceForScaleLeft, Y + Height - 2, ClipToWidth(keyMap, maxWidth), false, DefaultBg, DefaultFg);
        _canvas.Text(
            X + _spaceForScaleLeft,
            Y + Height - 1,
            ClipToWidth(legend, maxWidth),
            false,
            Visual.DefaultBg,
            Visual.DefaultFg);
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
            _ => "Legend: map visualization"
        };
    }

    #endregion
}