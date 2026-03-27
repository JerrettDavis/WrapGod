namespace VendorLib;

/// <summary>
/// A simulated third-party HTTP client that your application depends on.
/// In a real scenario this would come from an external NuGet package.
/// </summary>
public class HttpClient
{
    /// <summary>Timeout in seconds for HTTP requests.</summary>
    public int Timeout { get; set; } = 30;

    /// <summary>The base URL for requests.</summary>
    public string BaseUrl { get; set; } = "https://api.example.com";

    /// <summary>Send a GET request and return the response body.</summary>
    public string Get(string path)
    {
        return $"[GET {BaseUrl}/{path} timeout={Timeout}s]";
    }

    /// <summary>Send a POST request with a body and return the response.</summary>
    public string Post(string path, string body)
    {
        return $"[POST {BaseUrl}/{path} body={body} timeout={Timeout}s]";
    }

    /// <summary>Dispose underlying resources. Consumers should not need this on the wrapper.</summary>
    public void Dispose()
    {
        // no-op for demo
    }
}
