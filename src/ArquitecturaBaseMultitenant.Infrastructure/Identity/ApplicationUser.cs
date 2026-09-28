using Microsoft.AspNetCore.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Shell CLR para el contexto único; su modelo y tabla nacen en E3.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
