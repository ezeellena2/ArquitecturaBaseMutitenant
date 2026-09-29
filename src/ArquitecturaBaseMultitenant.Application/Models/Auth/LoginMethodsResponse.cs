namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Canales activos aportados por el núcleo y los módulos registrados.</summary>
public sealed record LoginMethodsResponse(IReadOnlyList<LoginChannelAvailability> Channels);

public sealed record LoginChannelAvailability(string Key, IReadOnlyList<string> Countries);
