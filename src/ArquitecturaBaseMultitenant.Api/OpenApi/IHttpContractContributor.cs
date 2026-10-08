using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Metadatos aportados por núcleo o módulos sin dependencias desde el exportador hacia un módulo.</summary>
public interface IHttpContractContributor
{
    void Describe(OpenApiDocument document);
}
