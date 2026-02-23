using ElBruno.HuggingFace;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHuggingFaceDownloader_Default_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddHuggingFaceDownloader();

        var provider = services.BuildServiceProvider();

        var downloader = provider.GetService<HuggingFaceDownloader>();
        Assert.NotNull(downloader);

        var options = provider.GetService<HuggingFaceDownloaderOptions>();
        Assert.NotNull(options);
    }

    [Fact]
    public void AddHuggingFaceDownloader_WithOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddHuggingFaceDownloader(options =>
        {
            options.AuthToken = "test-token";
            options.Timeout = TimeSpan.FromMinutes(60);
            options.ResolveFileSizesBeforeDownload = false;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<HuggingFaceDownloaderOptions>();

        Assert.Equal("test-token", options.AuthToken);
        Assert.Equal(TimeSpan.FromMinutes(60), options.Timeout);
        Assert.False(options.ResolveFileSizesBeforeDownload);
    }

    [Fact]
    public void AddHuggingFaceDownloader_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHuggingFaceDownloader();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHuggingFaceDownloader_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddHuggingFaceDownloader();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<HuggingFaceDownloader>();
        var second = provider.GetRequiredService<HuggingFaceDownloader>();

        Assert.Same(first, second);
    }
}
