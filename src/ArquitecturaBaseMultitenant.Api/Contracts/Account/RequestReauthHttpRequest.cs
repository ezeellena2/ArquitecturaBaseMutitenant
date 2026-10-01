using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe la acción sensible y el método objetivo de una prueba de titularidad. Los campos nullable
/// permiten que la validación informe entradas faltantes o inválidas.
/// </summary>
public sealed record RequestReauthHttpRequest(ReauthAction? Action, Guid? TargetMethodId);
