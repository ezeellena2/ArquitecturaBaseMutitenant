using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record AddLoginEmailRequest(Email? Email);
