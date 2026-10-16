using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Escrituras del segundo factor de los operadores (E9) dentro del límite transaccional del caso de uso.</summary>
internal sealed class PlatformFactorRepository(ApplicationDbContext context) : IPlatformFactorRepository
{
    public Task LockAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Account(userId)], cancellationToken);

    public Task<PlatformAuthenticator?> GetAuthenticatorAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformAuthenticators.SingleOrDefaultAsync(authenticator => authenticator.UserId == userId,
            cancellationToken);
    }

    public void Add(PlatformAuthenticator authenticator)
    {
        context.RequireTransaction();
        context.PlatformAuthenticators.Add(authenticator);
    }

    public Task<PlatformRecoveryCode?> GetRecoveryCodeAsync(Guid userId, string codeHash, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformRecoveryCodes.SingleOrDefaultAsync(
            code => code.UserId == userId && code.CodeHash == codeHash, cancellationToken);
    }

    public void Add(IEnumerable<PlatformRecoveryCode> codes)
    {
        context.RequireTransaction();
        context.PlatformRecoveryCodes.AddRange(codes);
    }

    public Task<PlatformFactorChallenge?> GetChallengeForUpdateAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformFactorChallenges.FromSqlInterpolated(
                $"SELECT * FROM identity.\"PlatformFactorChallenges\" WHERE \"Id\" = {challengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(PlatformFactorChallenge challenge)
    {
        context.RequireTransaction();
        context.PlatformFactorChallenges.Add(challenge);
    }

    public Task<PlatformSession?> GetSessionAsync(Guid platformSessionId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformSessions.SingleOrDefaultAsync(session => session.Id == platformSessionId, cancellationToken);
    }

    public void Add(PlatformSession session)
    {
        context.RequireTransaction();
        context.PlatformSessions.Add(session);
    }

    public Task<PlatformFactorOperation?> GetOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformFactorOperations.SingleOrDefaultAsync(
            operation => operation.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public void Add(PlatformFactorOperation operation)
    {
        context.RequireTransaction();
        context.PlatformFactorOperations.Add(operation);
    }

    public async Task InvalidateChallengesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var open = await context.PlatformFactorChallenges
            .Where(challenge => challenge.UserId == userId && challenge.CompletedAtUtc == null && challenge.InvalidatedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var challenge in open) challenge.Invalidate(nowUtc);
    }

    public async Task RevokeSessionAsync(Guid userId, string sessionIdHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var session = await context.PlatformSessions.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId && candidate.SessionIdHash == sessionIdHash, cancellationToken);
        session?.Revoke(nowUtc);
    }

    public async Task RevokeSessionsAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var active = await context.PlatformSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in active) session.Revoke(nowUtc);
    }

    public async Task RemoveFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        context.PlatformRecoveryCodes.RemoveRange(await context.PlatformRecoveryCodes
            .Where(code => code.UserId == userId).ToListAsync(cancellationToken));
        if (await context.PlatformAuthenticators.SingleOrDefaultAsync(
                authenticator => authenticator.UserId == userId, cancellationToken) is { } authenticator)
            context.PlatformAuthenticators.Remove(authenticator);
    }
}
