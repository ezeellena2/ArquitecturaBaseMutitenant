using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Único adaptador global de búsqueda por valor de método de ingreso.</summary>
internal sealed class UserLookup(ApplicationDbContext context) : IUserLookup
{
    public async Task<IReadOnlyList<Guid>> FindVerifiedUsersByEmailAsync(Email email, CancellationToken cancellationToken) =>
        await context.LoginMethods.AsNoTracking()
            .Where(method => method.VerifiedAtUtc != null &&
                (method.Type == LoginMethodType.Email && method.Value == email.Value
                    || method.Type == LoginMethodType.Google && method.ContactEmail == email))
            .Select(method => method.UserId).Distinct().ToArrayAsync(cancellationToken);

    public Task<Guid?> FindVerifiedUserIdAsync(
        LoginMethodType type, string value, CancellationToken cancellationToken) =>
        context.LoginMethods.AsNoTracking()
            .Where(method => method.Type == type && method.Value == value && method.VerifiedAtUtc != null)
            .Select(method => (Guid?)method.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<LoginMethodLookup?> FindMethodAsync(
        LoginMethodType type, string value, CancellationToken cancellationToken) =>
        context.LoginMethods.AsNoTracking()
            .Where(method => method.Type == type && method.Value == value)
            .Select(method => new LoginMethodLookup(method.Id, method.UserId, method.VerifiedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
}
