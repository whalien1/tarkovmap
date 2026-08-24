using System.Text.Json;
using System.Text.Json.Serialization;
using TarkovMap.Models;

namespace TarkovMap.Services;

/// <summary>
/// 配置读写：Config/config.json。不存在则创建默认值；损坏则回退默认值，不阻止启动。
/// 不保存玩家坐标、截图历史、战局信息。
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("lastMapId")]
    public string LastMapId { get; set; } = "";

    [JsonPropertyName("screenshotDirectory")]
    public string ScreenshotDirectory { get; set; } = "";

    [JsonPropertyName("topMost")]
    public bool TopMost { get; set; }

    [JsonPropertyName("windowWidth")]
    public int WindowWidth { get; set; } = 1280;

    [JsonPropertyName("windowHeight")]
    public int WindowHeight { get; set; } = 800;

    /// <summary>Marker 分类显示状态（key = MarkerType 枚举名）。</summary>
    [JsonPropertyName("markerVisibility")]
    public Dictionary<string, bool> MarkerVisibility { get; set; } = new();

    /// <summary>悬浮小地图设置。</summary>
    [JsonPropertyName("miniMap")]
    public MiniMapSettings MiniMap { get; set; } = new();
}

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _configFile;

    public AppConfig Config { get; private set; } = new();

    public ConfigService(string baseDirectory)
    {
        _configFile = Path.Combine(baseDirectory, "Config", "config.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                Config = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(_configFile), JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // 配置损坏：回退默认值，不阻止启动
            Config = new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFile)!);
            File.WriteAllText(_configFile, JsonSerializer.Serialize(Config, JsonOptions));
        }
        catch
        {
            // 配置保存失败不影响使用
        }
    }
}
