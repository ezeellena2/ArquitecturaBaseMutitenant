using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Declara un error de la acción con application/problem+json en OpenAPI.</summary>
public sealed class ProducesProblemAttribute(int statusCode)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode, ProblemContentType)
{
    private const string ProblemContentType = "application/problem+json";
}
