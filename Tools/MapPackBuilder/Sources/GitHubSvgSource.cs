using System.Text.Json;
using System.Text.RegularExpressions;

namespace MapPackBuilder.Sources;

internal sealed class GitHubSvgSource
{
    internal const string RepositoryName = "the-hideout/tarkov-dev-svg-maps";
    internal static readonly Uri HeadCommitUri =
        new($"https://api.github.com/repos/{RepositoryName}/commits/HEAD");

    private static readonly Regex CommitPattern = new("^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
    private readonly HttpClient _httpClient;

    public GitHubSvgSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SvgRepositorySnapshot> FetchAsync(
        IEnumerable<string> assetNames,
        CancellationToken cancellationToken = default)
    {
        var names = assetNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (names.Count == 0)
        {
            throw new ArgumentException("至少需要一个 SVG 资源。", nameof(assetNames));
        }

        foreach (var name in names)
        {
            if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) ||
                !name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"非法 SVG 资源名：{name}", nameof(assetNames));
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        var commitBytes = await FetchBytesAsync(HeadCommitUri, timeout.Token).ConfigureAwait(false);
        using var commitDocument = JsonDocument.Parse(commitBytes);
        var commitSha = commitDocument.RootElement.TryGetProperty("sha", out var shaNode)
            ? shaNode.GetString() ?? ""
            : "";
        if (!CommitPattern.IsMatch(commitSha))
        {
            throw new InvalidDataException("SVG 上游没有返回有效的 40 位提交编号。");
        }

        var tasks = names.ToDictionary(
            name => name,
            name => FetchBytesAsync(RawUri(commitSha, name), timeout.Token),
            StringComparer.Ordinal);
        var licenseTask = FetchBytesAsync(RawUri(commitSha, "LICENSE.md"), timeout.Token);
        await Task.WhenAll(tasks.Values.Append(licenseTask)).ConfigureAwait(false);

        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (name, task) in tasks)
        {
            var bytes = await task.ConfigureAwait(false);
            var prefix = System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 512)));
            if (!prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"上游资源不是有效 SVG：{name}");
            }

            assets[name] = bytes;
        }

        return new SvgRepositorySnapshot(
            commitSha,
            assets,
            await licenseTask.ConfigureAwait(false),
            DateTimeOffset.UtcNow);
    }

    internal static Uri RawUri(string commitSha, string fileName) =>
        new($"https://raw.githubusercontent.com/{RepositoryName}/{commitSha}/{fileName}");

    private async Task<byte[]> FetchBytesAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("TarkovMap-MapPackBuilder/1.0");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            throw new InvalidDataException($"上游返回空资源：{uri}");
        }

        return bytes;
    }
}

internal sealed record SvgRepositorySnapshot(
    string CommitSha,
    IReadOnlyDictionary<string, byte[]> Assets,
    byte[] License,
    DateTimeOffset RetrievedAt);
