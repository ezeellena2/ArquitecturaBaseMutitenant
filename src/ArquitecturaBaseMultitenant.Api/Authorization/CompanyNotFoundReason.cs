using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>La empresa de la ruta no es de la organización activa, o no existe: se responde 404, nunca 403 (E9, CC2).</summary>
internal sealed class CompanyNotFoundReason(IAuthorizationHandler handler)
    : AuthorizationFailureReason(handler, "The company was not found.");
