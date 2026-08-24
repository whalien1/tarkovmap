using TarkovMap.Models;
using Xunit;

namespace TarkovMap.Tests;

/// <summary>WorldBounds.Contains 边界判定回归测试。</summary>
public class WorldBoundsTests
{
    private static readonly WorldBounds Bounds = new() { X0 = -500, X1 = 500, Z0 = -500, Z1 = 500 };

    [Fact]
    public void Inside_ContainsTrue()
    {
        Assert.True(Bounds.Contains(0, 0));
        Assert.True(Bounds.Contains(499, -499));
    }

    [Fact]
    public void Outside_ContainsFalse()
    {
        Assert.False(Bounds.Contains(501, 0));
        Assert.False(Bounds.Contains(0, -501));
    }

    [Fact]
    public void Boundary_ContainsTrue()
    {
        // 临界值在边界上应算包含
        Assert.True(Bounds.Contains(-500, 0));
        Assert.True(Bounds.Contains(500, 500));
    }

    [Fact]
    public void ReversedBounds_StillValid()
    {
        // X0/X1 或 Z0/Z1 可能反向，Contains 不依赖顺序
        var b = new WorldBounds { X0 = 500, X1 = -500, Z0 = 500, Z1 = -500 };
        Assert.True(b.Contains(0, 0));
        Assert.False(b.Contains(600, 0));
    }
}
