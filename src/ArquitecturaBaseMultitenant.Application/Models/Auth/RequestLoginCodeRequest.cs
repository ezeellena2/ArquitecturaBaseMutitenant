using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

public sealed record RequestLoginCodeRequest(Email? Email);
