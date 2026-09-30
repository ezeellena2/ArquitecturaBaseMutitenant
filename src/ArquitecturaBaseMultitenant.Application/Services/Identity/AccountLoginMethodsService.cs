using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

internal sealed class AccountLoginMethodsService(ICurrentUser currentUser, ILoginMethodReader methods,
    ITenantReader tenants, LoginMethodAvailability availability, IGoogleAvailability google,
    IPlatformSettingsReader settings, TimeProvider timeProvider, ILogger<AccountLoginMethodsService> logger) : IAccountLoginMethodsService
{
    public Task<Result<AccountLoginMethodsResponse>> ListAsync(CancellationToken ct) =>
        OperationLog.RunAsync<AccountLoginMethodsResponse>(logger, timeProvider, "ListAccountLoginMethods", async () =>
        {
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            var all = await methods.ListByUserIdAsync(userId, ct);
            var available = new List<LoginMethodRow>();
            foreach (var method in all)
                if (await availability.IsAvailableAsync(userId, method, ct)) available.Add(method);
            var rows = new List<AccountLoginMethodResponse>();
            foreach (var method in all.OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id))
            {
                var remaining = available.Where(value => value.Id != method.Id).ToArray();
                var backup = remaining.Where(availability.CanReceiveCode).OrderByDescending(value => value.IsPrimary)
                    .ThenBy(value => value.ManagedByTenantId is not null).ThenBy(value => value.Id).FirstOrDefault();
                var isAvailable = available.Any(value => value.Id == method.Id);
                var canRemove = backup is not null && !(isAvailable && method.ManagedByTenantId is null
                    && remaining.All(value => value.ManagedByTenantId is not null));
                var organization = method.ManagedByTenantId is { } tenantId ? await tenants.FindByIdAsync(tenantId, ct) : null;
                rows.Add(new AccountLoginMethodResponse(method.Id, method.Type,
                    method.Type == LoginMethodType.Google ? method.ContactEmail?.Value : method.Value,
                    method.IsPrimary, method.VerifiedAtUtc is not null, method.ManagedByTenantId, organization?.Name,
                    canRemove, !method.IsPrimary && isAvailable && backup is not null,
                    backup is null ? null : Mask(backup)));
            }
            var platform = await settings.FindAsync(ct) ?? throw new InvalidOperationException("Platform settings have not been seeded.");
            return new AccountLoginMethodsResponse(rows, google.IsEnabled && !all.Any(method => method.Type == LoginMethodType.Google),
                !all.Any(method => method.VerifiedAtUtc is not null && method.ManagedByTenantId is null), platform.AccountDeletionGraceDays);
        });

    private static string Mask(LoginMethodRow method)
    {
        var contact = method.Type == LoginMethodType.Google ? method.ContactEmail!.Value : method.Value;
        var at = contact.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? contact[..1] + "***" + contact[at..] : "••••" + contact[^Math.Min(4, contact.Length)..];
    }
}
