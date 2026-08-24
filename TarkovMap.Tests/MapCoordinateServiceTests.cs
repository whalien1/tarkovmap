using TarkovMap.Models;
using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

/// <summary>World ↔ Image 坐标换算（线性映射 + reverseCoordinate 交换）回归测试。</summary>
public class MapCoordinateServiceTests
{
    private static readonly WorldBounds Bounds = new()
    {
        X0 = -500, X1 = 500, Z0 = -500, Z1 = 500, ReverseCoordinate = false
    };

    [Fact]
    public void Center_WorldToImage_IsImageCenter()
    {
        var p = MapCoordinateService.WorldToImage(Bounds, 1000, 1000, 0, 0);
        Assert.Equal(500, p.X, 4);
        Assert.Equal(500, p.Y, 4);
    }

    [Fact]
    public void CornerMin_WorldToImage_IsOrigin()
    {
        var p = MapCoordinateService.WorldToImage(Bounds, 1000, 1000, -500, -500);
        Assert.Equal(0, p.X, 4);
        Assert.Equal(0, p.Y, 4);
    }

    [Fact]
    public void CornerMax_WorldToImage_IsBottomRight()
    {
        var p = MapCoordinateService.WorldToImage(Bounds, 1000, 1000, 500, 500);
        Assert.Equal(1000, p.X, 4);
        Assert.Equal(1000, p.Y, 4);
    }

    [Fact]
    public void ReverseCoordinate_SwapsAxes()
    {
        var b = new WorldBounds { X0 = -500, X1 = 500, Z0 = -500, Z1 = 500, ReverseCoordinate = true };
        // x=500 → nx=1 → 交换后 nz=1 → imageY=1000；z=0 → nz=0.5 → 交换后 nx=0.5 → imageX=500
        var p = MapCoordinateService.WorldToImage(b, 1000, 1000, 500, 0);
        Assert.Equal(500, p.X, 4);
        Assert.Equal(1000, p.Y, 4);
    }

    [Fact]
    public void KnownPosition_WorldImageRoundTrips()
    {
        // 街区验证点 X=-239.36, Z=109.80（bounds 取自 streets-of-tarkov 数据）
        var b = new WorldBounds { X0 = 323, X1 = -280, Z0 = -298, Z1 = 530, ReverseCoordinate = false };
        var p = MapCoordinateService.WorldToImage(b, 2048, 2048, -239.36, 109.80);
        var (x, z) = MapCoordinateService.ImageToWorld(b, 2048, 2048, p.X, p.Y);
        Assert.Equal(-239.36, x, 3);
        Assert.Equal(109.80, z, 3);
    }
}
