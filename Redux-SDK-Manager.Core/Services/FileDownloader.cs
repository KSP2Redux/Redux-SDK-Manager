using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Redux_SDK_Manager.Services;

/// <summary>Downloads a file's raw bytes over HTTP.</summary>
public interface IFileDownloader
{
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class HttpFileDownloader : IFileDownloader
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
        => _http.GetByteArrayAsync(url, cancellationToken);
}
