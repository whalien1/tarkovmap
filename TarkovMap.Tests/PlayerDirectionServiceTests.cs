using TarkovMap.Services;
using Xunit;

namespace TarkovMap.Tests;

/// <summary>
/// 四元数 → 朝向角（Yaw，0-360 度）的基础公式回归测试（已通过实测锁定，勿改）。
/// </summary>
public class PlayerDirectionServiceTests
{
    [Fact]
    public void IdentityQuaternion_IsZeroDegrees()
    {
        var yaw = PlayerDirectionService.QuaternionToYawDegrees(0, 0, 0, 1);
        Assert.Equal(0, yaw, 4);
    }

    [Fact]
    public void QuarterTurnLeft_Is90Degrees()
    {
        // 绕 Y 轴 +90°：q = (0, sin45°, 0, cos45°)
        var yaw = PlayerDirectionService.QuaternionToYawDegrees(0, 0.7071067812, 0, 0.7071067812);
        Assert.Equal(90, yaw, 3);
    }

    [Fact]
    public void NegativeYaw_WrapsTo360()
    {
        // 绕 Y 轴 -90°：结果应归一化为 270°
        var yaw = PlayerDirectionService.QuaternionToYawDegrees(0, -0.7071067812, 0, 0.7071067812);
        Assert.Equal(270, yaw, 3);
    }

    [Fact]
    public void Degrees_InRange()
    {
        // 任意四元数，结果都应落在 [0,360) 内
        var yaw = PlayerDirectionService.QuaternionToYawDegrees(0.1, 0.2, 0.3, 0.9);
        Assert.InRange(yaw, 0, 359.9999);
    }
}
