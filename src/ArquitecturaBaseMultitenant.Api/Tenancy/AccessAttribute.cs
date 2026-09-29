using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Declara los accesos válidos para un controller o una acción.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AccessAttribute : TypeFilterAttribute
{
    public AccessAttribute(params Access[] allowed) : base(typeof(AccessFilter))
    {
        ArgumentNullException.ThrowIfNull(allowed);
        if (allowed.Length == 0 || allowed.Any(access => !Enum.IsDefined(access)))
        {
            throw new ArgumentException("Declare at least one valid access.", nameof(allowed));
        }

        Allowed = [.. allowed];
        Arguments = [Allowed];
    }

    public IReadOnlyList<Access> Allowed { get; }
}
