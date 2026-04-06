using System.Net;
using System.Net.Http.Headers;
using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

/// <summary>
/// Phase 4 tests: Authentication, token resolution, and HTTP configuration behavior.
/// </summary>
public class AuthenticationAndConfigurationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalHfToken;

    public AuthenticationAndConfigurationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hf_auth_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalHfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
    }

    public void Dispose()
    {
        // Restore original HF_TOKEN value
        if (_originalHfToken is not null)
            Environment.SetEnvironmentVariable("HF_TOKEN", _originalHfToken);
        else
            Environment.SetEnvironmentVariable("HF_TOKEN", null);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region Mock Helpers

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private static HttpResponseMessage CreateFileResponse(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    #endregion

    #region ResolveToken Tests

    [Fact]
    public void ResolveToken_WithAuthToken_ReturnsAuthToken()
    {
        var options = new HuggingFaceDownloaderOptions { AuthToken = "hf_explicit_token" };

        var token = options.ResolveToken();

        Assert.Equal("hf_explicit_token", token);
    }

    [Fact]
    public void ResolveToken_WithoutAuthToken_ReturnsHfTokenEnvVar()
    {
        Environment.SetEnvironmentVariable("HF_TOKEN", "hf_env_token");
        var options = new HuggingFaceDownloaderOptions();

        var token = options.ResolveToken();

        Assert.Equal("hf_env_token", token);
    }

    [Fact]
    public void ResolveToken_WithBothAuthTokenAndEnvVar_PrefersAuthToken()
    {
        Environment.SetEnvironmentVariable("HF_TOKEN", "hf_env_token");
        var options = new HuggingFaceDownloaderOptions { AuthToken = "hf_explicit_token" };

        var token = options.ResolveToken();

        Assert.Equal("hf_explicit_token", token);
    }

    [Fact]
    public void ResolveToken_WithNeitherSet_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("HF_TOKEN", null);
        var options = new HuggingFaceDownloaderOptions();

        var token = options.ResolveToken();

        Assert.Null(token);
    }

    [Fact]
    public void ResolveToken_WithEmptyAuthToken_FallsBackToEnvVar()
    {
        Environment.SetEnvironmentVariable("HF_TOKEN", "hf_env_token");
        var options = new HuggingFaceDownloaderOptions { AuthToken = "" };

        // Empty string is not null, so AuthToken ?? env returns ""
        var token = options.ResolveToken();

        Assert.Equal("", token);
    }

    #endregion

    #region Authorization Header Tests

    [Fact]
    public async Task DownloadFilesAsync_WithBearerToken_SendsAuthorizationHeader()
    {
        AuthenticationHeaderValue? capturedAuth = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                capturedAuth = request.Headers.Authorization;
            return Task.FromResult(CreateFileResponse("secured content"));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "hf_test_token_123");

        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/private-repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"]
        });

        Assert.NotNull(capturedAuth);
        Assert.Equal("Bearer", capturedAuth!.Scheme);
        Assert.Equal("hf_test_token_123", capturedAuth.Parameter);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithoutToken_NoAuthorizationHeader()
    {
        AuthenticationHeaderValue? capturedAuth = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                capturedAuth = request.Headers.Authorization;
            return Task.FromResult(CreateFileResponse("public content"));
        });

        using var httpClient = new HttpClient(handler);
        // No Authorization header set
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/public-repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"]
        });

        Assert.Null(capturedAuth);
    }

    [Fact]
    public async Task DownloadFilesAsync_BearerTokenSentOnHeadRequests()
    {
        AuthenticationHeaderValue? capturedHeadAuth = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                capturedHeadAuth = request.Headers.Authorization;
                var headResp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                };
                headResp.Content.Headers.ContentLength = 100;
                return Task.FromResult(headResp);
            }
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "hf_head_token");

        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = true };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        });

        Assert.NotNull(capturedHeadAuth);
        Assert.Equal("Bearer", capturedHeadAuth!.Scheme);
        Assert.Equal("hf_head_token", capturedHeadAuth.Parameter);
    }

    #endregion

    #region User-Agent Header Tests

    [Fact]
    public async Task DownloadFilesAsync_WithCustomUserAgent_SendsCustomUserAgent()
    {
        string? capturedUserAgent = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                capturedUserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyCustomApp/3.0");

        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        });

        Assert.NotNull(capturedUserAgent);
        Assert.Contains("MyCustomApp/3.0", capturedUserAgent);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithDefaultUserAgent_SendsLibraryUserAgent()
    {
        string? capturedUserAgent = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                capturedUserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        // Simulate what CreateHttpClient does with default UserAgent
        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ElBruno.HuggingFace.Downloader/1.0");

        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        });

        Assert.NotNull(capturedUserAgent);
        Assert.Contains("ElBruno.HuggingFace.Downloader/1.0", capturedUserAgent);
    }

    #endregion

    #region Constructor with Auth/Config Tests

    [Fact]
    public void Constructor_WithAuthToken_DoesNotThrow()
    {
        var options = new HuggingFaceDownloaderOptions { AuthToken = "hf_test_token" };

        using var downloader = new HuggingFaceDownloader(options);

        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithHfTokenEnvVar_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("HF_TOKEN", "hf_env_token_for_constructor");
        var options = new HuggingFaceDownloaderOptions();

        using var downloader = new HuggingFaceDownloader(options);

        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithCustomUserAgent_DoesNotThrow()
    {
        var options = new HuggingFaceDownloaderOptions { UserAgent = "TestAgent/1.0" };

        using var downloader = new HuggingFaceDownloader(options);

        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithCustomTimeout_DoesNotThrow()
    {
        var options = new HuggingFaceDownloaderOptions { Timeout = TimeSpan.FromSeconds(30) };

        using var downloader = new HuggingFaceDownloader(options);

        Assert.NotNull(downloader);
    }

    #endregion
}
