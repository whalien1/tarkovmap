using System.Globalization;
using System.Text.RegularExpressions;
using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// 截图文件名 → PlayerLocation。纯函数，无 WinForms 依赖。
/// 格式（游戏自带截图功能生成，已用真实目录验证）：
///   YYYY-MM-DD[HH-mm]_X, Y, Z_qx, qy, qz, qw_FOV (序号).png
/// 无坐标的截图（结算/仓库画面）不匹配此格式，TryParse 返回 false，静默跳过。
/// </summary>
public static class ScreenshotLocationParser
{
    private static readonly Regex NameRegex = new(
        @"^(?<date>\d{4}-\d{2}-\d{2})\[(?<hour>\d{2})-(?<minute>\d{2})\]_" +
        @"(?<x>-?\d+(?:\.\d+)?),\s*(?<y>-?\d+(?:\.\d+)?),\s*(?<z>-?\d+(?:\.\d+)?)_" +
        @"(?<qx>-?\d+(?:\.\d+)?),\s*(?<qy>-?\d+(?:\.\d+)?),\s*" +
        @"(?<qz>-?\d+(?:\.\d+)?),\s*(?<qw>-?\d+(?:\.\d+)?)_" +
        @"(?<fov>-?\d+(?:\.\d+)?)(?:\s*\((?<index>\d+)\))?\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string fileName, out PlayerLocation location)
    {
        location = new PlayerLocation();
        var match = NameRegex.Match(Path.GetFileName(fileName));
        if (!match.Success)
        {
            return false;
        }

        try
        {
            var qx = Read(match, "qx");
            var qy = Read(match, "qy");
            var qz = Read(match, "qz");
            var qw = Read(match, "qw");

            location = new PlayerLocation
            {
                X = Read(match, "x"),
                Y = Read(match, "y"),
                Z = Read(match, "z"),
                Rotation = new QuaternionData { X = qx, Y = qy, Z = qz, W = qw },
                YawDegrees = PlayerDirectionService.QuaternionToYawDegrees(qx, qy, qz, qw),
                FileName = Path.GetFileName(fileName)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double Read(Match m, string group) =>
        double.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);
}
