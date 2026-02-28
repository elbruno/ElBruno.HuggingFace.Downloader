namespace ElBruno.HuggingFace;

/// <summary>
/// Builds Hugging Face Hub URLs for file downloads.
/// </summary>
public static class HuggingFaceUrlBuilder
{
    private const string BaseUrl = "https://huggingface.co";

    /// <summary>
    /// Returns the URL to download a file from a Hugging Face repository.
    /// </summary>
    /// <param name="repoId">Repository ID (e.g., "sentence-transformers/all-MiniLM-L6-v2").</param>
    /// <param name="filePath">Path within the repository (e.g., "onnx/model.onnx").</param>
    /// <param name="revision">Branch, tag, or commit SHA. Defaults to "main".</param>
    public static string GetFileUrl(string repoId, string filePath, string revision = "main")
    {
        ValidateRepoId(repoId);
        ValidateFilePath(filePath);
        ValidateRevision(revision);

        return $"{BaseUrl}/{repoId}/resolve/{revision}/{filePath}";
    }

    private static void ValidateRepoId(string repoId)
    {
        if (string.IsNullOrWhiteSpace(repoId))
            throw new ArgumentException("Repository ID cannot be null or empty.", nameof(repoId));

        // Validate repo ID format: owner/repo
        if (!repoId.Contains('/') || repoId.StartsWith('/') || repoId.EndsWith('/'))
            throw new ArgumentException(
                $"Invalid repository ID format '{repoId}'. Expected format: 'owner/repo'.",
                nameof(repoId));

        // Prevent path traversal
        if (repoId.Contains("..") || repoId.Contains('\\'))
            throw new ArgumentException(
                $"Invalid repository ID '{repoId}'. Path traversal detected.",
                nameof(repoId));
    }

    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        // Prevent path traversal
        if (filePath.Contains("..") || filePath.Contains('\\') || filePath.StartsWith('/'))
            throw new ArgumentException(
                $"Invalid file path '{filePath}'. Path traversal or absolute paths are not allowed.",
                nameof(filePath));
    }

    private static void ValidateRevision(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Revision cannot be null or empty.", nameof(revision));

        // Prevent path traversal in revision
        if (revision.Contains("..") || revision.Contains('/') || revision.Contains('\\'))
            throw new ArgumentException(
                $"Invalid revision '{revision}'. Path separators are not allowed.",
                nameof(revision));
    }
}
