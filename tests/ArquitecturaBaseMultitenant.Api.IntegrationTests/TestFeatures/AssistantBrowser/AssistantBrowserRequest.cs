using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

public sealed record AssistantBrowserRequest(Guid Id, string Kind, JsonElement Args)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static AssistantBrowserRequest Parse(string json)
    {
        var request = JsonSerializer.Deserialize<AssistantBrowserRequest>(json, Json)
            ?? throw new InvalidOperationException("Private assistant command missing.");
        if (request.Id == Guid.Empty || request.Args.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Private assistant command binding invalid.");
        string[] allowed = request.Kind switch
        {
            "prepare" => ["email", "access", "tenantId"],
            "quotas" => ["email", "access", "tenantId", "remainingMessages", "monthlyFraction", "notify"],
            "clock" => ["email", "access", "tenantId", "retentionExpired"],
            "providerCounters" => [],
            _ => throw new InvalidOperationException("Unsupported private assistant command."),
        };
        if (request.Args.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Private assistant parameters invalid.");
        if (request.Kind == "providerCounters") return request;
        var email = Email.Create(request.Args.GetProperty("email").GetString());
        if (email.IsFailure || !email.Value.Value.EndsWith("@example.test", StringComparison.OrdinalIgnoreCase)
            || request.Args.GetProperty("access").GetString() is not ("business" or "consumer" or "platform"))
            throw new InvalidOperationException("Private assistant actor must be synthetic.");
        if (request.Args.TryGetProperty("tenantId", out var tenant) && (!tenant.TryGetGuid(out var id) || id == Guid.Empty))
            throw new InvalidOperationException("Private assistant scope invalid.");
        if (request.Kind == "quotas" && (request.Args.GetProperty("remainingMessages").GetInt32() is not (0 or 10 or 50)
            || request.Args.GetProperty("monthlyFraction").GetDecimal() is not (0m or .8m or 1m)))
            throw new InvalidOperationException("Private assistant quota scenario invalid.");
        if (request.Kind == "clock" && !request.Args.GetProperty("retentionExpired").GetBoolean())
            throw new InvalidOperationException("Private retention scenario invalid.");
        return request;
    }
}
