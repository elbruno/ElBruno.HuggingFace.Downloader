using System.Net;
#if NETSTANDARD2_0
using System.Reflection;
#endif

namespace ElBruno.HuggingFace;

// The status code of a failed download. HttpRequestException has it from .NET 5. The netstandard2.0 build, which .NET Framework and
// .NET Core 3.1 to .NET 7 apps get, sets it through the .NET 5+ constructor when the runtime has one, and keeps it in Exception.Data.
internal static class HttpStatusCodes
{
#if NETSTANDARD2_0
    private static readonly ConstructorInfo? StatusCodeConstructor =
        typeof(HttpRequestException).GetConstructor([typeof(string), typeof(Exception), typeof(HttpStatusCode?)]);
#endif

    public static HttpRequestException Exception(string message, HttpStatusCode statusCode)
    {
#if NETSTANDARD2_0
        var exception = StatusCodeConstructor is null
            ? new HttpRequestException(message)
            : (HttpRequestException)StatusCodeConstructor.Invoke([message, null, (HttpStatusCode?)statusCode]);
        exception.Data[nameof(HttpStatusCode)] = statusCode;
        return exception;
#else
        return new HttpRequestException(message, inner: null, statusCode: statusCode);
#endif
    }

    public static HttpStatusCode? Of(HttpRequestException exception)
    {
#if NETSTANDARD2_0
        return exception.Data[nameof(HttpStatusCode)] as HttpStatusCode?;
#else
        return exception.StatusCode;
#endif
    }
}
