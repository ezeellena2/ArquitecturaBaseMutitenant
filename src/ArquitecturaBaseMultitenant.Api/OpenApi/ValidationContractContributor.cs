using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Publica las restricciones del validador real, sin ejecutar reglas ni consultar datos.</summary>
internal sealed class ValidationContractContributor(IServiceProvider services) : IHttpContractContributor
{
    public void Describe(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas) return;
        using var scope = services.CreateScope();
        var contracts = typeof(ValidationContractContributor).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("ArquitecturaBaseMultitenant.Api.Contracts.", StringComparison.Ordinal) == true);
        var models = typeof(Application.DependencyInjection).Assembly.GetTypes();
        foreach (var contract in contracts)
        {
            if (!schemas.TryGetValue(contract.Name, out var value) || value is not OpenApiSchema schema) continue;
            var model = models.FirstOrDefault(type => type.Name == contract.Name.Replace("HttpRequest", "Request", StringComparison.Ordinal))
                ?? models.FirstOrDefault(type => type.Name == contract.Name.Replace("HttpRequest", "Input", StringComparison.Ordinal));
            if (model is null) continue;
            var validator = scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(model)) as IValidator;
            if (validator is not null)
            {
                foreach (var rule in validator.CreateDescriptor().Rules)
                {
                    if (rule.PropertyName is null) continue;
                    var property = FindProperty(schema, rule.PropertyName, schemas);
                    if (property is null) continue;
                    foreach (var component in rule.Components)
                    {
                        if (component.Validator is ILengthValidator length)
                        {
                            property.MaxLength = length.Max;
                            if (length.Min > 0) property.MinLength = length.Min;
                        }
                        if (component.Validator is IBetweenValidator range)
                        {
                            property.Minimum = Convert.ToString(range.From, CultureInfo.InvariantCulture);
                            property.Maximum = Convert.ToString(range.To, CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            // Los correos tipados se validan al adaptar HTTP, antes del validador del modelo.
            foreach (var member in model.GetProperties().Where(property => property.PropertyType == typeof(Email)))
                if (FindProperty(schema, member.Name, schemas) is { } email) email.MaxLength = Email.MaxLength;
        }
        // El enum del filtro también alimenta las opciones generadas del cliente.
        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-organization-audit-categories"] = new JsonNodeExtension(
            JsonSerializer.SerializeToNode(OrganizationAuditCategories.EntityTypes.Keys)!);
        foreach (var operation in document.Paths.Where(path => path.Key.StartsWith("/api/organization/audit", StringComparison.Ordinal))
            .SelectMany(path => path.Value.Operations?.Values.AsEnumerable() ?? []))
            foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                if (string.Equals(parameter.Name, "category", StringComparison.OrdinalIgnoreCase))
                    parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String,
                        Enum = OrganizationAuditCategories.EntityTypes.Keys.Select(key => (JsonNode)JsonValue.Create(key)!).ToList() };
    }

    private static OpenApiSchema? FindProperty(OpenApiSchema schema, string path,
        IDictionary<string, IOpenApiSchema> schemas)
    {
        OpenApiSchema? current = schema;
        foreach (var part in path.Split('.'))
        {
            IOpenApiSchema? member = null;
            current?.Properties?.TryGetValue(JsonNamingPolicy.CamelCase.ConvertName(part), out member);
            current = Resolve(member, schemas);
        }
        return current;
    }

    private static OpenApiSchema? Resolve(IOpenApiSchema? value, IDictionary<string, IOpenApiSchema> schemas)
    {
        if (value is OpenApiSchemaReference reference)
            return schemas.TryGetValue(reference.Reference.Id!, out var referenced) ? referenced as OpenApiSchema : null;
        if (value is not OpenApiSchema schema) return null;
        // Los contratos nullable pueden ser oneOf(null, $ref): se sigue la forma del dato.
        if (schema.OneOf?.OfType<OpenApiSchemaReference>().FirstOrDefault() is { } nullableReference)
            return Resolve(nullableReference, schemas);
        return schema;
    }
}
