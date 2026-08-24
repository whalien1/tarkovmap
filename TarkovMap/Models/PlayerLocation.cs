namespace TarkovMap.Models;

public sealed class QuaternionData
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double W { get; init; }
}

/// <summary>玩家位置（仅内存状态，不持久化）。</summary>
public sealed class PlayerLocation
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public QuaternionData Rotation { get; init; } = new();
    public double YawDegrees { get; init; }
    public string FileName { get; init; } = "";
}
