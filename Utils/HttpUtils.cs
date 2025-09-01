using System.Net;
using System.Text.RegularExpressions;

namespace RayTracer.Utils;

/// <summary>
/// This class provides some utility methods to make HTTP client operations a bit easier.
/// </summary>
public static class HttpUtils
{
    private static readonly Regex HttpUrlStart = new (@"^https?://");
    private static readonly HttpClientHandler Handler = new ()
    { 
        AutomaticDecompression = DecompressionMethods.All 
    };

    private static readonly HttpClient HttpClient = new (Handler);

    /// <summary>
    /// This method will return whether or not the given string looks like an HTTP URL.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><code>true</code>, if the text begins with either <code>http</code> or
    /// <code>https</code> protocol followed by the <code>://</code> string or
    /// <code>false</code>, otherwise.</returns>
    public static bool LooksLikeUrl(string text)
    {
        return HttpUrlStart.IsMatch(text);
    }

    /// <summary>
    /// This method performs a simple <c>GET</c> request for the given URL and returns the
    /// resulting message.
    /// </summary>
    /// <param name="url">The URL to perform the <c>GET</c> on.</param>
    /// <returns>The response message for the call.</returns>
    public static HttpResponseMessage Get(string url)
    {
        Uri uri = new (url);
        HttpRequestMessage request = new (HttpMethod.Get, uri);

        request.Headers.Add("Host", uri.Host);
        request.Headers.Add("User-Agent", "RayTracer/1.0.1");
        request.Headers.Add("Accept", "*/*");

        return HttpClient.SendAsync(request)
            .GetAwaiter()
            .GetResult();
    }
}
