using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElBruno.HuggingFace;

/// <summary>
/// Reads and writes model bundle manifest JSON files.
/// </summary>
public static class ModelBundleManifestJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Deserializes a <see cref="ModelBundleManifest"/> from JSON text.
    /// </summary>
    public static ModelBundleManifest Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<ModelBundleManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("The manifest JSON did not contain a bundle definition.");
    }

    /// <summary>
    /// Serializes a <see cref="ModelBundleManifest"/> to JSON text.
    /// </summary>
    public static string Serialize(ModelBundleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    /// <summary>
    /// Loads a <see cref="ModelBundleManifest"/> from a JSON file.
    /// </summary>
    public static async Task<ModelBundleManifest> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }

    /// <summary>
    /// Saves a <see cref="ModelBundleManifest"/> to a JSON file.
    /// </summary>
    public static async Task SaveAsync(ModelBundleManifest manifest, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, Serialize(manifest), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task SaveResolvedAsync(
        ResolvedModelBundleManifest manifest,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }
}
