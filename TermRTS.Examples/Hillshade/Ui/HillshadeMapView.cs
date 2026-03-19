using System.Numerics;
using ConsoleRenderer;
using TermRTS.Examples.Greenery;
using TermRTS.Io;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Hillshade.Ui;

/// <summary>
///     Map view that renders terrain with hillshade and raycast shadows driven by a day/night cycle.
///     Reuses the same layout and viewport logic as Greenery's MapView (camera, coordinate scales).
/// </summary>
public class HillshadeMapView : KeyInputProcessorBase
{
    #region Constants

    private const int SpaceForScaleTop = 1;
    private const int SpaceForTextfieldBottom = 1;
    private const int MaxShadowRaySteps = 80;
    private const float Ambient = 0.25f;
    private const float NightAmbient = 0.04f;

    /// <summary>Sun at zenith (90°) at noon; altitude in radians.</summary>
    private const float SunZenithAltitudeRad = (float)(Math.PI / 2.0);

    /// <summary>Sun position change threshold in radians; recompute hillshade only when exceeded.</summary>
    private const float SunPositionChangeThresholdRad = (float)(5.0 * Math.PI / 180.0);

    /// <summary>Elevations 0 to WaterSurfaceElevation (inclusive) are water; rendered as flat water surface.</summary>
    private const int WaterSurfaceElevation = 3;

    // Color temperature: phase 0 = sunrise, 0.5 = noon, 1 = sunset; second half is "afternoon" toward night
    private const float WarmTintMaxStrength = 0.35f;
    private const float BlueTintMaxStrength = 0.4f;
    private static readonly TerminalColor WarmTint = new(255, 160, 80);
    private static readonly TerminalColor BlueTint = new(70, 90, 160);

    private static readonly TerminalColor DefaultBg = new(Console.BackgroundColor);
    private static readonly TerminalColor DefaultFg = new(Console.ForegroundColor);

    // Terrain glyphs (same as Greenery elevation/terrain)
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

    // Elevation base colors (same palette as Greenery: water to land)
    private static readonly TerminalColor[] ColorsElevation =
    [
        new(0, 0, 128), // DarkBlue
        new(0, 0, 255), // Blue
        new(0, 128, 128), // DarkCyan
        new(0, 255, 255), // Cyan
        new(255, 255, 0), // Yellow
        new(0, 128, 0), // DarkGreen
        new(0, 255, 0), // Green
        new(128, 128, 0), // DarkYellow
        new(128, 128, 128), // DarkGray
        new(192, 192, 192) // Gray
    ];

    #endregion

    #region Fields

    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly ConsoleCanvas _canvas;
    private readonly (char c, TerminalColor fg, TerminalColor bg)[,] _cache;
    private int _spaceForScaleLeft;
    private int _viewportPositionInWorldX;
    private int _viewportPositionInWorldY;
    private float? _lastSunAzimuth;
    private float? _lastSunAltitude;

    #endregion

    #region Constructor

    public HillshadeMapView(ConsoleCanvas canvas, int worldWidth, int worldHeight)
    {
        _canvas = canvas;
        _canvas.AutoResize = true;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _cache = new (char, TerminalColor, TerminalColor)[worldWidth, worldHeight];
        ViewportPositionInWorldX = 0;
        ViewportPositionInWorldY = 0;
        Console.CursorVisible = false;
    }

    #endregion

    #region Private Properties

    private int ViewportWidth => Width - _spaceForScaleLeft;
    private int ViewportHeight => Height - SpaceForScaleTop - SpaceForTextfieldBottom;

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

    #region UiElementBase

    public override void UpdateFromComponents(
        in IReadonlyStorage componentStorage,
        double timeStepSizeMs,
        double howFarIntoNextFramePercent)
    {
        if (!componentStorage.TryGetSingleForType<WorldComponent>(out var world) || world == null)
            return;
        if (!componentStorage.TryGetSingleForType<TimeOfDayComponent>(out var timeOfDay) ||
            timeOfDay == null)
            return;

        ComputeSunPosition(timeOfDay.TimeMs, timeOfDay.DayLengthMs, out var sunAzimuth,
            out var sunAltitude);

        if (!ShouldRecomputeHillshade(sunAzimuth, sunAltitude))
            return;

        UpdateHillshadeAndShadows(world, sunAzimuth, sunAltitude);
        IsRequireReRender = true;
        _lastSunAzimuth = sunAzimuth;
        _lastSunAltitude = sunAltitude;
    }

    private static float AngularDifferenceRad(float a, float b)
    {
        var d = Math.Abs(a - b);
        return d > Math.PI ? (float)(2.0 * Math.PI - d) : d;
    }

    private bool ShouldRecomputeHillshade(float sunAzimuth, float sunAltitude)
    {
        if (_lastSunAzimuth is null || _lastSunAltitude is null)
            return true;
        var deltaAzimuth = AngularDifferenceRad(sunAzimuth, _lastSunAzimuth.Value);
        var deltaAltitude = Math.Abs(sunAltitude - _lastSunAltitude.Value);
        return deltaAzimuth >= SunPositionChangeThresholdRad ||
               deltaAltitude >= SunPositionChangeThresholdRad;
    }

    public override void Render()
    {
        var viewportExtendInWorldX = ViewportPositionInWorldX + ViewportWidth;
        var viewportExtendInWorldY = ViewportPositionInWorldY + ViewportHeight;
        var boundaryX = Math.Min(_worldWidth, viewportExtendInWorldX);
        var boundaryY = Math.Min(_worldHeight, viewportExtendInWorldY);

        for (var y = ViewportPositionInWorldY; y < boundaryY; y++)
        for (var x = ViewportPositionInWorldX; x < boundaryX; x++)
        {
            var (c, fg, bg) = _cache[x, y];
            _canvas.Set(
                X + WorldToViewportX(x) + _spaceForScaleLeft,
                Y + WorldToViewportY(y) + SpaceForScaleTop,
                c,
                fg,
                bg);
        }

        RenderCoordinates();
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

    #region KeyInputProcessorBase

    public override void HandleKeyInput(in ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                MoveCameraUp();
                break;
            case ConsoleKey.DownArrow:
                MoveCameraDown();
                break;
            case ConsoleKey.LeftArrow:
                MoveCameraLeft();
                break;
            case ConsoleKey.RightArrow:
                MoveCameraRight();
                break;
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Compute sun position from time. Phase 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset, 1 = midnight.
    /// Sun is below horizon (altitude 0) from sunset to sunrise; zenith is 90° at noon.
    /// </summary>
    private static void ComputeSunPosition(ulong timeMs, ulong dayLengthMs, out float sunAzimuth,
        out float sunAltitude)
    {
        if (dayLengthMs == 0)
        {
            sunAzimuth = 0f;
            sunAltitude = (float)(Math.PI / 4);
            return;
        }

        var phase = (double)(timeMs % dayLengthMs) / dayLengthMs;
        var solarAngle = (phase - 0.25) * 2.0 * Math.PI;
        sunAzimuth = (float)solarAngle;
        var altitudeRaw = Math.Sin(solarAngle) * SunZenithAltitudeRad;
        sunAltitude = (float)Math.Max(0.0, altitudeRaw);
    }

    /// <summary>Sun path: right (east) → overhead (noon, center/top) → left (west). Map Y increases downward so "overhead" uses -Y.</summary>
    private void UpdateHillshadeAndShadows(WorldComponent world, float sunAzimuth,
        float sunAltitude)
    {
        var sunX = (float)(Math.Cos(sunAltitude) * Math.Cos(sunAzimuth));
        var sunY = (float)(-Math.Cos(sunAltitude) * Math.Sin(sunAzimuth));
        var sunZ = (float)Math.Sin(sunAltitude);
        var sunDir = new Vector3(sunX, sunY, sunZ);

        var phase = (sunAzimuth / (float)(2.0 * Math.PI)) + 0.25f;
        if (phase < 0f) phase += 1f;
        if (phase >= 1f) phase -= 1f;
        GetColorTemperatureStrength(phase, sunAltitude, out var warmStrength, out var blueStrength);

        var ambient = sunAltitude > 0f ? Ambient : NightAmbient;
        var waterSurfaceNormal = Vector3.UnitZ;
        var waterSurfaceIntensityBase =
            Math.Clamp(ambient + (1f - ambient) * Vector3.Dot(waterSurfaceNormal, sunDir), 0f, 1f);

        for (var y = 0; y < _worldHeight; y++)
        for (var x = 0; x < _worldWidth; x++)
        {
            var elevation = world.Cells[x, y];
            var isWater = elevation <= WaterSurfaceElevation;

            float intensity;
            if (isWater)
            {
                intensity = waterSurfaceIntensityBase;
            }
            else
            {
                var inShadow = IsInShadow(world, x, y, sunAzimuth);

                float nx = 0, ny = 0;
                if (x > 0 && x < _worldWidth - 1)
                    nx = (world.Cells[x + 1, y] - world.Cells[x - 1, y]) * 0.5f;
                else if (x > 0)
                    nx = world.Cells[x, y] - world.Cells[x - 1, y];
                else if (x < _worldWidth - 1)
                    nx = world.Cells[x + 1, y] - world.Cells[x, y];

                if (y > 0 && y < _worldHeight - 1)
                    ny = (world.Cells[x, y + 1] - world.Cells[x, y - 1]) * 0.5f;
                else if (y > 0)
                    ny = world.Cells[x, y] - world.Cells[x, y - 1];
                else if (y < _worldHeight - 1)
                    ny = world.Cells[x, y + 1] - world.Cells[x, y];

                var normal = new Vector3(-nx, -ny, 1f);
                var len = normal.Length();
                if (len < 1e-5f)
                    normal = Vector3.UnitZ;
                else
                    normal /= len;

                    var dot = Vector3.Dot(normal, sunDir);
                    intensity = Math.Clamp(ambient + (1f - ambient) * dot, 0f, 1f);
                if (inShadow)
                    intensity *= 0.35f;
            }

            var elevationClamped = Math.Clamp((int)elevation, 0, 9);
            var charIndex = Math.Clamp(elevationClamped, 0, MarkersTerrain.Length - 1);
            var c = MarkersTerrain[charIndex];
            var baseColor =
                ColorsElevation[Math.Clamp(elevationClamped, 0, ColorsElevation.Length - 1)];
            var fg = ScaleColor(baseColor, intensity);
            fg = ApplyColorTemperature(fg, warmStrength, blueStrength);
            _cache[x, y] = (c, fg, DefaultBg);
        }
    }

    /// <summary>Compute warm (sunrise/sunset) and blue (night) tint strength with smooth transitions. Phase 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.</summary>
    private static void GetColorTemperatureStrength(float phase, float sunAltitude,
        out float warmStrength, out float blueStrength)
    {
        var altitudeNorm = SunZenithAltitudeRad > 0 ? Math.Clamp(sunAltitude / SunZenithAltitudeRad, 0f, 1f) : 0f;
        var sunDown = 1f - altitudeNorm;

        var phaseRad = (float)(phase * 2.0 * Math.PI);
        var cosPhase = (float)Math.Cos(phaseRad);
        var sinPhase = (float)Math.Sin(phaseRad);

        // Blue: smooth peak at midnight (phase 0 and 1), zero at noon (phase 0.5). (1 + cos)/2
        var blueCurve = 0.5f * (1f + cosPhase);
        blueStrength = blueCurve * sunDown * BlueTintMaxStrength;

        // Warm: smooth peaks at sunrise/sunset (phase 0.25 and 0.75), zero at midnight and noon. sin²
        var warmCurve = sinPhase * sinPhase;
        warmStrength = warmCurve * sunDown * WarmTintMaxStrength;
    }

    private static TerminalColor ApplyColorTemperature(TerminalColor baseColor, float warmStrength,
        float blueStrength)
    {
        warmStrength = Math.Clamp(warmStrength, 0f, 1f);
        blueStrength = Math.Clamp(blueStrength, 0f, 1f);
        if (!baseColor.IsRgb)
            return baseColor;
        var r = baseColor.R;
        var g = baseColor.G;
        var b = baseColor.B;
        if (warmStrength > 0)
        {
            r = (byte)Math.Min(255, r + (int)((WarmTint.R - r) * warmStrength));
            g = (byte)Math.Min(255, g + (int)((WarmTint.G - g) * warmStrength));
            b = (byte)Math.Min(255, b + (int)((WarmTint.B - b) * warmStrength));
        }

        if (blueStrength > 0)
        {
            r = (byte)Math.Min(255, r + (int)((BlueTint.R - r) * blueStrength));
            g = (byte)Math.Min(255, g + (int)((BlueTint.G - g) * blueStrength));
            b = (byte)Math.Min(255, b + (int)((BlueTint.B - b) * blueStrength));
        }

        return new TerminalColor(r, g, b);
    }

    private bool IsInShadow(WorldComponent world, int ox, int oy, float sunAzimuth)
    {
        var stepX = (float)Math.Cos(sunAzimuth);
        var stepY = (float)(-Math.Sin(sunAzimuth));
        var originElev = world.Cells[ox, oy];

        for (var n = 1; n <= MaxShadowRaySteps; n++)
        {
            var gx = (int)(ox + n * stepX + 0.5f);
            var gy = (int)(oy + n * stepY + 0.5f);
            if (gx < 0 || gx >= _worldWidth || gy < 0 || gy >= _worldHeight)
                break;
            if (world.Cells[gx, gy] >= originElev)
                return true;
        }

        return false;
    }

    private static TerminalColor ScaleColor(TerminalColor baseColor, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        if (!baseColor.IsRgb)
            return baseColor;
        var r = (byte)(baseColor.R * factor);
        var g = (byte)(baseColor.G * factor);
        var b = (byte)(baseColor.B * factor);
        return new TerminalColor(r, g, b);
    }

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
            : Math.Max(ViewportPositionInWorldX - 1, 0);
    }

    private void MoveCameraRight()
    {
        var maxViewportX = ViewportPositionInWorldX + ViewportWidth - 1;
        var maxWorldX = _worldWidth - ViewportWidth - 1;
        var boundaryX = Math.Min(maxViewportX, maxWorldX);
        ViewportPositionInWorldX = ViewportWidth > _worldWidth
            ? 0
            : Math.Min(ViewportPositionInWorldX + 1, boundaryX);
    }

    private int WorldToViewportX(float x)
    {
        return (int)(x - ViewportPositionInWorldX);
    }

    private int WorldToViewportY(float y)
    {
        return (int)(y - ViewportPositionInWorldY);
    }

    private int ViewportToWorldX(int x)
    {
        return ViewportPositionInWorldX + x;
    }

    private int ViewportToWorldY(int y)
    {
        return ViewportPositionInWorldY + y;
    }

    private void RenderCoordinates()
    {
        for (var x = 0; x < _spaceForScaleLeft; x++)
            _canvas.Set(X + x, Y, Cp437.BlockFull, DefaultBg);

        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            _canvas.Set(X + _spaceForScaleLeft + x, Y, Cp437.BlockFull, fg);
        }

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

        for (var y = 0; y <= ViewportHeight; y++)
        for (var x = 0; x < _spaceForScaleLeft; x++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            _canvas.Set(X + x, y + SpaceForScaleTop, Cp437.BlockFull, fg);
        }

        for (var y = 0; y <= ViewportHeight; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                _canvas.Text(X, y + SpaceForScaleTop, Convert.ToString(worldY), false, DefaultBg,
                    DefaultFg);
        }
    }

    #endregion
}