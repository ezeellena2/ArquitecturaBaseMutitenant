namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Un tramo de la poda de OpenIddict (ADR 0062): cuántos tokens y cuántas autorizaciones podó, y desde qué fecha de
/// creación sigue el próximo tramo, o null si llegó al umbral final.
/// </summary>
public sealed record OpenIddictPruneTranche(long Tokens, long Authorizations, DateTime? NextFromUtc);
