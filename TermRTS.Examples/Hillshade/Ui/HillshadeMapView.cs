using System.Numerics;
using ConsoleRenderer;
using TermRTS.Io;
using TermRTS.Shared.Ui;
using TermRTS.Shared.World;
using TermRTS.Storage;
using TermRTS.Ui;

namespace TermRTS.Examples.Hillshade.Ui;

/// <summary>
///     Map view that renders terrain with hillshade and raycast shadows driven by a day/night cycle.
///     Shares viewport and panning with <see cref="ViewportMapViewBase" />.
/// </summary>
public class HillshadeMapView : ViewportMapViewBase
{
    #region Constants

    private const int MaxShadowRaySteps = 40;
    private const float Ambient = 0.25f;
    private const float ShadowIntensityFactor = 0.5f;
    private const float NightAmbient = 0.04f;

    /// <summary>Sun at zenith (90°) at noon; altitude in radians.</summary>
    private const float SunZenithAltitudeRad = (float)(Math.PI / 2.0);

    /// <summary>Sun position change threshold in radians; recompute hillshade only when exceeded.</summary>
    private const float SunPositionChangeThresholdRad = (float)(5.0 * Math.PI / 180.0);

    /// <summary>Elevations 0 to WaterSurfaceElevation (inclusive) are water; rendered as flat water surface.</summary>
    private const int WaterSurfaceElevation = 3;

    // Not `const` so the raycast body stays reachable for the compiler when toggled to true.
    private static bool EnableShadowRaycast = false;

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

    private readonly (char c, TerminalColor fg, TerminalColor bg)[,] _cache;
    private float? _lastSunAzimuth;
    private float? _lastSunAltitude;

    #endregion

    #region Constructor

    public HillshadeMapView(ConsoleCanvas canvas, int worldWidth, int worldHeight) : base(canvas,
        worldWidth, worldHeight)
    {
        _cache = new (char, TerminalColor, TerminalColor)[worldWidth, worldHeight];
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
        var boundaryX = Math.Min(WorldWidth, viewportExtendInWorldX);
        var boundaryY = Math.Min(WorldHeight, viewportExtendInWorldY);

        for (var y = ViewportPositionInWorldY; y < boundaryY; y++)
        for (var x = ViewportPositionInWorldX; x < boundaryX; x++)
        {
            var (c, fg, bg) = _cache[x, y];
            Canvas.Set(
                X + WorldToViewportX(x) + SpaceForScaleLeftValue,
                Y + WorldToViewportY(y) + SpaceForScaleTop,
                c,
                fg,
                bg);
        }

        RenderCoordinates();
    }

    /// <summary>Hillshade uses block glyphs for the coordinate ruler (contrast with Greenery whitespace ticks).</summary>
    protected override void RenderCoordinates()
    {
        for (var x = 0; x < SpaceForScaleLeftValue; x++)
            Canvas.Set(X + x, Y, Cp437.BlockFull, DefaultBg);

        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            Canvas.Set(X + SpaceForScaleLeftValue + x, Y, Cp437.BlockFull, fg);
        }

        for (var x = 0; x <= ViewportWidth; x++)
        {
            var worldX = ViewportToWorldX(x);
            var isTick = worldX > 0 && worldX % 10 == 0;
            if (!isTick) continue;
            var spaceForLabel = Width - x - SpaceForScaleLeftValue;
            var tickLabel = Convert.ToString(worldX);
            if (tickLabel.Length > spaceForLabel) tickLabel = tickLabel[..spaceForLabel];
            Canvas.Text(X + SpaceForScaleLeftValue + x, Y, tickLabel, false, DefaultBg, DefaultFg);
        }

        for (var y = 0; y <= ViewportHeight; y++)
        for (var x = 0; x < SpaceForScaleLeftValue; x++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            var fg = isTick ? DefaultFg : DefaultBg;
            Canvas.Set(X + x, y + SpaceForScaleTop, Cp437.BlockFull, fg);
        }

        for (var y = 0; y <= ViewportHeight; y++)
        {
            var worldY = ViewportToWorldY(y);
            var isTick = worldY > 0 && worldY % 5 == 0;
            if (isTick)
                Canvas.Text(X, y + SpaceForScaleTop, Convert.ToString(worldY), false, DefaultBg,
                    DefaultFg);
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

        for (var y = 0; y < WorldHeight; y++)
        for (var x = 0; x < WorldWidth; x++)
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
                if (x > 0 && x < WorldWidth - 1)
                    nx = (world.Cells[x + 1, y] - world.Cells[x - 1, y]) * 0.5f;
                else if (x > 0)
                    nx = world.Cells[x, y] - world.Cells[x - 1, y];
                else if (x < WorldWidth - 1)
                    nx = world.Cells[x + 1, y] - world.Cells[x, y];

                if (y > 0 && y < WorldHeight - 1)
                    ny = (world.Cells[x, y + 1] - world.Cells[x, y - 1]) * 0.5f;
                else if (y > 0)
                    ny = world.Cells[x, y] - world.Cells[x, y - 1];
                else if (y < WorldHeight - 1)
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
                    intensity *= ShadowIntensityFactor;
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
        if (!EnableShadowRaycast) return false;

        var stepX = (float)Math.Cos(sunAzimuth);
        var stepY = (float)(-Math.Sin(sunAzimuth));
        var originElev = world.Cells[ox, oy];

        for (var n = 1; n <= MaxShadowRaySteps; n++)
        {
            var gx = (int)(ox + n * stepX + 0.5f);
            var gy = (int)(oy + n * stepY + 0.5f);
            if (gx < 0 || gx >= WorldWidth || gy < 0 || gy >= WorldHeight)
                break;
            if (world.Cells[gx, gy] > originElev)
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

    #endregion
}
