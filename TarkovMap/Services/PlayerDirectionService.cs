namespace TarkovMap.Services;

/// <summary>
/// 四元数 → 朝向角（Yaw，0-360 度）。
/// 公式来自参考项目 ScreenshotCoordinateParser（已在同类工具中实战验证），
/// 最终方向用工厂本地模式的定向校准截图锁定。
/// </summary>
public static class PlayerDirectionService
{
    public static double QuaternionToYawDegrees(double qx, double qy, double qz, double qw)
    {
        var sinyCosp = 2 * (qw * qy + qx * qz);
        var cosyCosp = 1 - 2 * (qy * qy + qz * qz);
        var yaw = Math.Atan2(sinyCosp, cosyCosp) * 180 / Math.PI;
        return (yaw + 360) % 360;
    }
}
