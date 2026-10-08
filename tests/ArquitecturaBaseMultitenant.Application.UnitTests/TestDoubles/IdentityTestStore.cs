using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

internal sealed class IdentityTestStore : IUserRepository, ILoginMethodRepository, IUserLookup,
    ITenantDomainRepository, IExternalLoginLock
{
    internal Dictionary<Guid, UserAccountRow> Users { get; } = [];
    internal List<LoginMethod> Methods { get; } = [];
    internal List<string> Locks { get; } = [];
    Task<Result> IUserRepository.RequestDeletionAsync(Guid userId, string reason, DateTime requestedAtUtc, int graceDays, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.RequestDeletionByOperatorAsync(Guid userId, Guid operatorId, string reason, DateTime requestedAtUtc, int graceDays, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.CancelDeletionAsync(Guid userId, DateTime cancelledAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.CompleteDeletionAsync(Guid userId, DateTime deletedAtUtc, string deletedDisplayName, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.SuspendAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result<UserStatus>> IUserRepository.ReactivateAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task IUserRepository.SetPlatformOperatorAsync(Guid userId, bool isOperator, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task IUserRepository.InvitePlatformOperatorAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken) { Users[userId] = Users[userId] with { IsPlatformOperator = true, OperatorInvitationExpiresAtUtc = expiresAtUtc }; return Task.CompletedTask; }
    Task<Result> IUserRepository.RenewOperatorInvitationAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.AcceptOperatorInvitationAsync(Guid userId, DateTime acceptedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Result> IUserRepository.DiscardOperatorInvitationAsync(Guid userId, DateTime discardedAtUtc, string deletedDisplayName, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<UserAccountRow> IUserRepository.CreateAsync(string? displayName, string culture, string timeZoneId, CancellationToken cancellationToken) { var row = new UserAccountRow(Guid.CreateVersion7(), displayName, culture, timeZoneId, UserStatus.Active, false, null, null); Users.Add(row.Id, row); return Task.FromResult(row); }
    Task<UserAccountRow?> IUserRepository.GetByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Users.GetValueOrDefault(userId));
    Task IUserRepository.SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task IUserRepository.SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber, CancellationToken cancellationToken) { Users[userId] = Users[userId] with { PrimaryEmail = email }; return Task.CompletedTask; }
    Task<Result<uint>> IUserRepository.UpdateProfileAsync(Guid userId, string? firstName, string? lastName, string culture, string timeZoneId, uint expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task IUserRepository.FillMissingProfileNamesAsync(Guid userId, string? firstName, string? lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task IUserRepository.RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task ILoginMethodRepository.LockUserAsync(Guid userId, CancellationToken cancellationToken) { Locks.Add("account"); return Task.CompletedTask; }
    Task<LoginMethodRow?> ILoginMethodRepository.GetIdentityHintAsync(Guid userId, Guid methodId, CancellationToken cancellationToken) => Task.FromResult(Methods.Where(method => method.UserId == userId && method.Id == methodId).Select(method => new LoginMethodRow(method.Id, method.Type, method.Value, method.IsPrimary, method.VerifiedAtUtc, method.ManagedByTenantId, method.ContactEmail, method.InvitationOriginTenantId)).SingleOrDefault());
    Task<LoginMethod?> ILoginMethodRepository.GetByIdForUserAsync(Guid userId, Guid methodId, CancellationToken cancellationToken) => Task.FromResult(Methods.SingleOrDefault(m => m.UserId == userId && m.Id == methodId));
    Task<IReadOnlyList<LoginMethod>> ILoginMethodRepository.ListByUserIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LoginMethod>>(Methods.Where(m => m.UserId == userId).ToArray());
    Task ILoginMethodRepository.ClearPrimaryAsync(Guid userId, CancellationToken cancellationToken) { foreach (var method in Methods.Where(m => m.UserId == userId)) method.ClearPrimary(); return Task.CompletedTask; }
    Task<LoginMethod?> ILoginMethodRepository.GetByIdAsync(Guid methodId, CancellationToken cancellationToken) => Task.FromResult(Methods.SingleOrDefault(m => m.Id == methodId));
    Task<IReadOnlyList<LoginMethod>> ILoginMethodRepository.ListVerifiedByEmailDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken) => throw new NotSupportedException();
    void ILoginMethodRepository.Add(LoginMethod method) { Methods.Add(method); }
    void ILoginMethodRepository.Remove(LoginMethod method) { Methods.Remove(method); }
    Task<IReadOnlyList<Guid>> IUserLookup.FindVerifiedUsersByEmailAsync(Email email, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Guid>>(Methods.Where(m => m.VerifiedAtUtc is not null && (m.Type == LoginMethodType.Email && m.Value == email.Value || m.ContactEmail == email)).Select(m => m.UserId).Distinct().ToArray());
    Task<Guid?> IUserLookup.FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<LoginMethodLookup?> IUserLookup.FindMethodAsync(LoginMethodType type, string value, CancellationToken cancellationToken) => Task.FromResult(Methods.Where(m => m.Type == type && m.Value == value).Select(m => new LoginMethodLookup(m.Id, m.UserId, m.VerifiedAtUtc)).FirstOrDefault());
    Task<string?> IUserLookup.FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task ITenantDomainRepository.LockDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<TenantDomain?> ITenantDomainRepository.GetByIdAsync(Guid domainId, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<TenantDomain?> ITenantDomainRepository.GetByIdAsync(Guid domainId, uint expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<TenantDomain?> ITenantDomainRepository.FindActiveByDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken) => throw new NotSupportedException();
    Task<Guid?> ITenantDomainRepository.FindVerifiedOrganizationIdAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
    void ITenantDomainRepository.Add(TenantDomain domain) => throw new NotSupportedException();
    void ITenantDomainRepository.MarkUpdated(TenantDomain domain) => throw new NotSupportedException();
    Task IExternalLoginLock.LockPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken) { Locks.Add("identity"); return Task.CompletedTask; }
    Task IExternalLoginLock.LockEmailAsync(Email email, CancellationToken cancellationToken) { Locks.Add("identity"); return Task.CompletedTask; }
    Task IExternalLoginLock.AcquireAsync(string providerSubject, Email? email, CancellationToken cancellationToken) => throw new NotSupportedException();
}
