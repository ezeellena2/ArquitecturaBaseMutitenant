namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Marca navegaciones de protocolo externo que declaran sus respuestas a mano.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class OwnProtocolAttribute : Attribute;
