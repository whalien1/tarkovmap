using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// World ↔ Image 坐标换算（无 WinForms 依赖，方便测试）。
/// 公式与参考项目 MapProjection 一致：
///   nx = (x - X0) / (X1 - X0)
///   nz = (z - Z0) / (Z1 - Z0)
///   reverseCoordinate 时交换 (nx, nz)
///   归一化坐标 (0..1) × 图片尺寸 = 图片像素坐标
/// </summary>
public static class MapCoordinateService
{
    public static PointF WorldToImage(WorldBounds bounds, int imageWidth, int imageHeight, double x, double z)
    {
        var nx = (x - bounds.X0) / (bounds.X1 - bounds.X0);
        var nz = (z - bounds.Z0) / (bounds.Z1 - bounds.Z0);
        if (bounds.ReverseCoordinate)
        {
            (nx, nz) = (nz, nx);
        }
        return new PointF((float)(nx * imageWidth), (float)(nz * imageHeight));
    }

    public static (double X, double Z) ImageToWorld(WorldBounds bounds, int imageWidth, int imageHeight, double imageX, double imageY)
    {
        var nx = imageX / imageWidth;
        var nz = imageY / imageHeight;
        if (bounds.ReverseCoordinate)
        {
            (nx, nz) = (nz, nx);
        }
        var x = bounds.X0 + nx * (bounds.X1 - bounds.X0);
        var z = bounds.Z0 + nz * (bounds.Z1 - bounds.Z0);
        return (x, z);
    }
}
