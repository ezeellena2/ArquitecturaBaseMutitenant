using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Alta propia por canal registrado; teléfono escrito se normaliza en el servicio, no es prueba de posesión.</summary>
public sealed record AddLoginMethodRequest(Email? Email, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(AddLoginMethodRequest);
}
