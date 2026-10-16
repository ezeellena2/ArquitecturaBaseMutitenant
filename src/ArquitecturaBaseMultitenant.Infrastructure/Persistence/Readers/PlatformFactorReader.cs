using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Lecturas del segundo factor (E9) sin tracking, siempre de la base: nunca pasan por la caché.</summary>
internal sealed class PlatformFactorReader(ApplicationDbContext context) : IPlatformFactorReader
{
    public Task<PlatformFactorChallengeRow?> FindChallengeAsync(Guid challengeId, CancellationToken cancellationToken) =>
        context.PlatformFactorChallenges.AsNoTracking()
            .Where(challenge => challenge.Id == challengeId)
            .Select(challenge => new PlatformFactorChallengeRow(challenge.Id, challenge.UserId, challenge.BindingHash,
                challenge.SecurityStamp, challenge.ReturnUrl, challenge.Step, challenge.ProtectedSecret,
                challenge.ProtectedRecoveryCodes, challenge.ExpiresAtUtc, challenge.CompletedAtUtc, challenge.InvalidatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasAuthenticatorAsync(Guid userId, CancellationToken cancellationToken) =>
        context.PlatformAuthenticators.AsNoTracking().AnyAsync(authenticator => authenticator.UserId == userId, cancellationToken);

    public Task<PlatformSessionRow?> FindSessionAsync(Guid platformSessionId, CancellationToken cancellationToken) =>
        context.PlatformSessions.AsNoTracking()
            .Where(session => session.Id == platformSessionId)
            .Select(session => new PlatformSessionRow(session.Id, session.UserId, session.SecurityStamp,
                session.StartedAtUtc, session.RecentProofUntilUtc, session.RevokedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PlatformSessionRow?> FindSessionByHashAsync(Guid userId, string sessionIdHash,
        CancellationToken cancellationToken) =>
        context.PlatformSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.SessionIdHash == sessionIdHash)
            .Select(session => new PlatformSessionRow(session.Id, session.UserId, session.SecurityStamp,
                session.StartedAtUtc, session.RecentProofUntilUtc, session.RevokedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PlatformFactorOperationRow?> FindOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        context.PlatformFactorOperations.AsNoTracking()
            .Where(operation => operation.IdempotencyKey == idempotencyKey)
            .Select(operation => new PlatformFactorOperationRow(operation.Id, operation.IdempotencyKey, operation.Kind,
                operation.UserId, operation.ChallengeId, operation.PlatformSessionId, operation.BodyHash, operation.Route,
                operation.ReturnUrl, operation.ProtectedSessionId, operation.ShowsRecoveryCodes, operation.VerifiedAtUtc,
                operation.RecentUntilUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PlatformFactorOperationRow?> FindOperationByIdAsync(Guid operationId, CancellationToken cancellationToken) =>
        context.PlatformFactorOperations.AsNoTracking()
            .Where(operation => operation.Id == operationId)
            .Select(operation => new PlatformFactorOperationRow(operation.Id, operation.IdempotencyKey, operation.Kind,
                operation.UserId, operation.ChallengeId, operation.PlatformSessionId, operation.BodyHash, operation.Route,
                operation.ReturnUrl, operation.ProtectedSessionId, operation.ShowsRecoveryCodes, operation.VerifiedAtUtc,
                operation.RecentUntilUtc))
            .SingleOrDefaultAsync(cancellationToken);
}
