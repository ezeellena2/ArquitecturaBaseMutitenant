using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Consulta registros TXT públicos mediante el endpoint DNS-over-HTTPS de Cloudflare.</summary>
internal sealed class CloudflareDnsTxtLookup(HttpClient client) : IDnsTxtLookup
{
    private static readonly Uri Endpoint = new("https://cloudflare-dns.com/dns-query");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<string>>> FindTxtRecordsAsync(string domain,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var uri = new UriBuilder(Endpoint)
        {
            Query = "name=" + Uri.EscapeDataString(domain) + "&type=TXT",
        }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/dns-json");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode) return TenantDomainErrors.DnsUnavailable;
            var payload = await response.Content.ReadFromJsonAsync<DnsJsonResponse>(JsonOptions, cancellationToken);
            if (payload is null || payload.Status is not (0 or 3)) return TenantDomainErrors.DnsUnavailable;
            return payload.Answer is null ? Array.Empty<string>()
                : payload.Answer.Where(answer => answer.Type == 16)
                    .Select(answer => answer.Data.Trim().Trim('"')).ToArray();
        }
        catch (HttpRequestException)
        {
            return TenantDomainErrors.DnsUnavailable;
        }
        catch (JsonException)
        {
            return TenantDomainErrors.DnsUnavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TenantDomainErrors.DnsUnavailable;
        }
    }

    private sealed record DnsJsonResponse(int Status, Answer[]? Answer);

    private sealed record Answer(int Type, string Data);
}
