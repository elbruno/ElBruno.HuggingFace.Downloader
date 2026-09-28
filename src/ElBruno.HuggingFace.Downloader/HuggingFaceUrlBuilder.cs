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

    /// <summary>
    /// Returns the Hugging Face Hub tree API URL used to list the files and directories in a repository.
    /// </summary>
    /// <param name="repoId">Repository ID (e.g., "sentence-transformers/all-MiniLM-L6-v2").</param>
    /// <param name="repoType">The kind of repository (model, dataset, or space).</param>
    /// <param name="revision">Branch, tag, or commit SHA. Defaults to "main".</param>
    /// <param name="path">Optional subdirectory path within the repository to list. Defaults to the repository root.</param>
    /// <param name="recursive">If <see langword="true"/>, requests the full tree recursively in a single call.</param>
    /// <param name="cursor">Pagination cursor returned by a previous call, if any.</param>
    public static string GetTreeUrl(
        string repoId,
        RepoType repoType = RepoType.Model,
        string revision = "main",
        string? path = null,
        bool recursive = true,
        string? cursor = null)
    {
        ValidateRepoId(repoId);
        ValidateRevision(revision);

        if (path is not null)
            ValidateFilePath(path);

        var repoTypeSegment = repoType switch
        {
            RepoType.Model => "models",
            RepoType.Dataset => "datasets",
            RepoType.Space => "spaces",
            _ => throw new ArgumentOutOfRangeException(nameof(repoType), repoType, "Unknown repository type.")
        };

        var url = $"{BaseUrl}/api/{repoTypeSegment}/{repoId}/tree/{revision}";
        if (!string.IsNullOrEmpty(path))
            url += $"/{path}";

        var query = new List<string>();
        if (recursive)
            query.Add("recursive=true");
        if (!string.IsNullOrEmpty(cursor))
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return url;
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

        if (revision.StartsWith('/') || revision.EndsWith('/'))
            throw new ArgumentException(
                $"Invalid revision '{revision}'. Revisions cannot start or end with '/'.",
                nameof(revision));

        // Prevent path traversal in revision while still allowing nested ref names such as refs/pr/42
        if (revision.Contains("..") || revision.Contains('\\'))
            throw new ArgumentException(
                $"Invalid revision '{revision}'. Path traversal is not allowed.",
                nameof(revision));
    }
}
