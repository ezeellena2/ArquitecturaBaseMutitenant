using System.Net.Http.Json;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

internal static class PlatformJourney
{
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, cancellationToken);
    }
}
