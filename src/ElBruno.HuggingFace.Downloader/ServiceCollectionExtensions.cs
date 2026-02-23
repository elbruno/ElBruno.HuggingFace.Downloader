using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElBruno.HuggingFace;

/// <summary>
/// Extension methods for registering <see cref="HuggingFaceDownloader"/> with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HuggingFaceDownloader"/> as a singleton service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure <see cref="HuggingFaceDownloaderOptions"/>.</param>
    public static IServiceCollection AddHuggingFaceDownloader(
        this IServiceCollection services,
        Action<HuggingFaceDownloaderOptions>? configure = null)
    {
        var options = new HuggingFaceDownloaderOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<HuggingFaceDownloader>>();
            return new HuggingFaceDownloader(sp.GetRequiredService<HuggingFaceDownloaderOptions>(), logger);
        });

        return services;
    }
}
