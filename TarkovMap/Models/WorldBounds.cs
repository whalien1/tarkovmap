namespace TarkovMap.Models;

/// <summary>
/// 世界坐标边界。两个对角点 (X0,Z0) → (X1,Z1)，
/// 对应图片归一化坐标 (0,0) → (1,1)（reverseCoordinate 时交换 X/Z）。
/// 数据来源：tarkov-dev 地图规范，与参考项目一致。
/// </summary>
public sealed class WorldBounds
{
    public double X0 { get; set; }
    public double Z0 { get; set; }
    public double X1 { get; set; }
    public double Z1 { get; set; }
    public bool ReverseCoordinate { get; set; }

    /// <summary>地图坐标旋转角（源数据 coordinateRotation，多数地图为 180）。用于玩家朝向箭头。</summary>
    public double CoordinateRotation { get; set; }

    public bool Contains(double x, double z)
    {
        var minX = Math.Min(X0, X1);
        var maxX = Math.Max(X0, X1);
        var minZ = Math.Min(Z0, Z1);
        var maxZ = Math.Max(Z0, Z1);
        return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
    }
}
