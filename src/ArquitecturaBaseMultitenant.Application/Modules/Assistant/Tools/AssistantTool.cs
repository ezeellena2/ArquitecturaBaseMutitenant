using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

internal abstract class AssistantTool(AssistantAccessPolicy policy) : IAssistantTool
{
    private static readonly JsonSerializerOptions SchemaOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    public abstract string Key { get; }
    public abstract Access? Access { get; }
    public abstract IReadOnlyList<string> Permissions { get; }
    public string DescriptionKey => "Tool." + Key;
    protected AssistantAccessPolicy AccessPolicy { get; } = policy;
    protected abstract IReadOnlyList<AssistantToolParameter> Parameters { get; }
    public JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        additionalProperties = false,
        properties = Parameters.ToDictionary(p => p.Name, p => new
        {
            type = p.Type == "integer" ? "integer" : "string",
            format = p.Type == "uuid" ? "uuid" : p.Type == "utc" ? "date-time" : null,
            minimum = p.Type == "integer" ? 1 : (int?)null,
            maximum = p.Name == "pageSize" ? 20 : p.Type == "integer" ? 1_000_000 : (int?)null,
            maxLength = p.Type == "integer" ? (int?)null : p.Name == "cursor" ? 2048 : 100,
            anyOf = p.Name == "pageSize" ? new[] { new { @const = 10 }, new { @const = 20 } } : null,
            @enum = p.Values,
        }),
        required = Parameters.Where(p => p.Required).Select(p => p.Name).ToArray(),
    }, SchemaOptions);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        AccessPolicy.CanExecuteAsync(Access, Permissions, null, cancellationToken);

    public async Task<Result<AssistantToolData>> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!AssistantToolArguments.IsValid(arguments, Parameters)) return AssistantErrors.QuickQueryNotAvailable;
        var parsed = new AssistantToolArguments(arguments);
        if (await AuthorizeAsync(parsed.Id("companyId"), cancellationToken) is { } denied) return denied;
        var result = await ExecuteCoreAsync(parsed, cancellationToken);
        if (result.IsFailure) return result;
        // A grant or legal version may change while the core reader is running.
        return await AuthorizeAsync(parsed.Id("companyId"), cancellationToken) is { } revoked ? revoked : result;
    }

    private async Task<Error?> AuthorizeAsync(Guid? companyId, CancellationToken ct)
    {
        if (!await AccessPolicy.CanExecuteAsync(Access, Permissions, null, ct)) return AssistantErrors.QuickQueryNotAvailable;
        if (companyId is { } company && !await AccessPolicy.CanExecuteAsync(Access, Permissions, company, ct))
            return Domain.Tenancy.CompanyErrors.NotFound;
        return null;
    }

    protected abstract Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments arguments, CancellationToken ct);
    protected static AssistantToolParameter Page => new("page", "integer", true);
    protected static AssistantToolParameter PageSize => new("pageSize", "integer", true);
}
