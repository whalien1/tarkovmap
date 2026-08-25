using System.Text.Json;

namespace MapPackBuilder.Sources;

internal sealed class TarkovDevSource
{
    internal static readonly Uri MapsUri = new("https://json.tarkov.dev/pve/maps");
    internal static readonly Uri ChineseTranslationsUri = new("https://json.tarkov.dev/pve/maps_zh");

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _httpClient;

    public TarkovDevSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TarkovDevRawSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        var mapsTask = FetchJsonAsync(MapsUri, timeout.Token);
        var translationsTask = FetchJsonAsync(ChineseTranslationsUri, timeout.Token);
        await Task.WhenAll(mapsTask, translationsTask).ConfigureAwait(false);

        return new TarkovDevRawSnapshot(
            await mapsTask.ConfigureAwait(false),
            await translationsTask.ConfigureAwait(false),
            DateTimeOffset.UtcNow);
    }

    private async Task<byte[]> FetchJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            throw new InvalidDataException($"上游返回空数据：{uri}");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("data", out _))
            {
                throw new InvalidDataException($"上游 JSON 缺少 data：{uri}");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"上游返回无效 JSON：{uri}", ex);
        }

        return bytes;
    }
}

internal sealed record TarkovDevRawSnapshot(
    byte[] MapsJson,
    byte[] ChineseTranslationsJson,
    DateTimeOffset RetrievedAt);
