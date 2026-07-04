namespace ElBruno.HuggingFace;

/// <summary>
/// Describes a single file entry in a model bundle manifest.
/// </summary>
public sealed record class ModelBundleFile
{
    /// <summary>
    /// Repository-relative file path (for example, <c>onnx/model.onnx</c>).
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Optional expected file size in bytes.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Optional expected SHA-256 hash encoded as lowercase hexadecimal.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Indicates whether this file must be present after bundle resolution.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Optional per-file revision override. When omitted, the manifest revision is used.
    /// </summary>
    public string? Revision { get; init; }
}
