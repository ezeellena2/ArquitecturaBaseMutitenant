using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

public sealed record VerifyLoginCodeRequest(Email? Email, string? Code, string? ReturnUrl);
