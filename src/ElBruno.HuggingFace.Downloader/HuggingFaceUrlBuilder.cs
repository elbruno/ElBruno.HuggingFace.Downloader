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
        return $"{BaseUrl}/{repoId}/resolve/{revision}/{filePath}";
    }
}
