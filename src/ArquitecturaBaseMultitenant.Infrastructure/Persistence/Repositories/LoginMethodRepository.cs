using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara los métodos de ingreso de una cuenta para altas y cambios transaccionales. Incluye el lock de cuenta y el retiro inmediato de la marca principal antes de elegir otra.</summary>
internal sealed class LoginMethodRepository(ApplicationDbContext context) : ILoginMethodRepository
{
    public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Account(userId)], cancellationToken);

    public Task<LoginMethod?> GetByIdForUserAsync(Guid userId, Guid methodId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LoginMethods.SingleOrDefaultAsync(
            method => method.Id == methodId && method.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<LoginMethod>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return await context.LoginMethods.Where(method => method.UserId == userId)
            .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task ClearPrimaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        // El índice parcial es inmediato: retirar primero la marca evita depender
        // del orden en que EF emita los UPDATE de dos métodos en SaveChanges.
        await context.LoginMethods.Where(method => method.UserId == userId && method.IsPrimary)
            .ExecuteUpdateAsync(setters => setters.SetProperty(method => method.IsPrimary, false), cancellationToken);
        foreach (var entry in context.ChangeTracker.Entries<LoginMethod>()
                     .Where(entry => entry.Entity.UserId == userId))
            entry.Entity.ClearPrimary();
    }

    public Task<LoginMethod?> GetByIdAsync(Guid methodId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LoginMethods.SingleOrDefaultAsync(method => method.Id == methodId, cancellationToken);
    }

    public async Task<IReadOnlyList<LoginMethod>> ListVerifiedByEmailDomainAsync(OrganizationEmailDomain domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        context.RequireTransaction();
        var pattern = $"%@{domain.Value}";
        var methodIds = await context.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM identity."LoginMethods"
                WHERE "VerifiedAtUtc" IS NOT NULL
                  AND (("Type" = 'Email' AND "Value" ILIKE {pattern})
                    OR ("Type" = 'Google' AND "ContactEmail" ILIKE {pattern}))
                """).ToArrayAsync(cancellationToken);
        if (methodIds.Length == 0) return [];
        return await context.LoginMethods.Where(method => methodIds.Contains(method.Id))
            .ToArrayAsync(cancellationToken);
    }

    public void Add(LoginMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        context.RequireTransaction();
        context.LoginMethods.Add(method);
    }

    public void Remove(LoginMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        context.RequireTransaction();
        context.LoginMethods.Remove(method);
    }
}
