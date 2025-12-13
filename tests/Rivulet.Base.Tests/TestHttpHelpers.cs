namespace Rivulet.Base.Tests;

/// <summary>
/// Test helper for mocking HTTP message handlers.
/// </summary>
public class TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        handler(request, cancellationToken);
}

/// <summary>
/// Factory methods for creating test HTTP clients.
/// </summary>
public static class TestHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient with a custom message handler for testing.
    /// </summary>
    public static HttpClient CreateTestClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string? baseAddress = "http://test.local")
    {
        var messageHandler = new TestHttpMessageHandler(handler);
        var client = new HttpClient(messageHandler);

        if (baseAddress != null)
        {
            client.BaseAddress = new Uri(baseAddress);
        }

        return client;
    }
}
