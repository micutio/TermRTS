using System.Numerics;

namespace TermRTS.Examples.Greenery.WorldGen;

public class ElevationParameters
{
    public int MaxElevation { get; set; } = 9;
    public int LandElevationThreshold { get; set; } = 4;
    public int HighMountainThreshold { get; set; } = 7;
    public int SnowThreshold { get; set; } = 8;
    public int HighSeaThreshold { get; set; } = 1;
    public int OceanThreshold { get; set; } = 2;
    public int ShelfThreshold { get; set; } = 3;
    public int ShallowsThreshold { get; set; } = 4;
    public float ElevationAmplitudeModifier { get; set; } = 15f;
}

public class CoastalParameters
{
    public float MaxCoastalSlope { get; set; } = 5.0f;
}

public class VolcanicParameters
{
    /// <summary>
    /// The amount of volcanic soil eroded. Smaller value -> more resistance.
    /// </summary>
    public float VolcanicResistance { get; set; } = 0.05f;
    public float HotspotMinStrength { get; set; } = 0.3f;
    public float LavaHotspotThreshold { get; set; } = 0.6f;
    public float CraterElevationThreshold { get; set; } = 6;
    public float CinderElevationThreshold { get; set; } = 5;
    public float CalderaElevationThreshold { get; set; } = 7;
    public float ShieldVolcanoThreshold { get; set; } = 5;
    public int MinIslandChains { get; set; } = 12;
    public int MaxIslandChains { get; set; } = 85;
    public int MinChainLength { get; set; } = 3;
    public int MaxChainLength { get; set; } = 8;
    public int ChainSpacing { get; set; } = 9;
    public int MinHotspotRadius { get; set; } = 8;
    public int MaxHotspotRadius { get; set; } = 24;
    public float MinHotspotStrength { get; set; } = 3.4f;
    public float MaxHotspotStrength { get; set; } = 9.9f;
}

public class ErosionParameters
{
    public int ErosionIterations { get; set; } = 10;
    public float HydraulicErosionRate { get; set; } = 0.041f;
    public float SedimentCapacity { get; set; } = 0.041f;
    public float DepositionRate { get; set; } = 0.041f;
    public float EvaporationRate { get; set; } = 0.041f;
    public float RainRate { get; set; } = 0.7f;
    public float ThermalErosionRate { get; set; } = 0.05f;
    public float TalusAngle { get; set; } = 0.4f;
    public float MinSlope { get; set; } = 0.01f;
    public float Gravity { get; set; } = 9.81f;
    public float WaterViscosity { get; set; } = 0.001f;
}

public class ClimateParameters
{
    public float BaseTempMax { get; set; } = 35.0f;
    public float BaseTempMin { get; set; } = -40.0f;
    public float AridityConstant { get; set; } = 0.05f;
    public float BaseTemperatureAmplitude { get; set; } = 10.0f;
    public float LatitudeAmplitudeModifier { get; set; } = 20.0f;
}

public class RiverParameters
{
    public float RiverFormationThreshold { get; set; } = 0.01f;
    public float RiverCarveScale { get; set; } = 0.01f;
    public float RiverMaxCarveDepth { get; set; } = 3.0f;
    public int RiverCarveMinElevation { get; set; } = 4;
    public int RainfallWaterDistanceRadius { get; set; } = 2;
    public float RainfallOceanBase { get; set; } = 2f;
    public float RainfallLandBase { get; set; } = 1f;
    public float RainfallWaterDistancePenalty { get; set; } = 0.007f;
    public float RainfallMinValue { get; set; } = 0.4f;
    public float RainfallElevationDecay { get; set; } = 0.01f;
    public float RainfallMinModifier { get; set; } = 0.2f;
}