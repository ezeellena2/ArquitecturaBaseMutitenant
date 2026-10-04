using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Convierte una cuenta de prueba en operadora con los permisos individuales elegidos.</summary>
internal static class PlatformOperators
{
    internal static readonly IReadOnlyCollection<string> All = [.. PlatformPermissions.All];

    /// <summary>Consulta y revisa recuperaciones, sin administrar organizaciones, cuentas ni configuración.</summary>
    internal static readonly IReadOnlyCollection<string> Reduced =
    [
        PlatformPermissions.Tenants.Read,
        PlatformPermissions.Accounts.Read,
        PlatformPermissions.Recoveries.Manage,
        PlatformPermissions.Audit.Read,
    ];

    internal static async Task GrantAsync(IServiceProvider provider, Guid userId,
        IReadOnlyCollection<string> permissions, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByIdAsync(userId.ToString("D"));
            Assert.NotNull(user);
            if (!user.IsPlatformOperator)
            {
                user.GrantPlatformOperator();
                Assert.True((await manager.UpdateAsync(user)).Succeeded);
            }

            var repository = services.GetRequiredService<IPlatformPermissionAssignmentRepository>();
            var existing = await repository.GetByUserIdAsync(userId, ct);
            if (existing is null) repository.Add(PlatformPermissionAssignment.Create(userId, permissions).Value);
            else Assert.True(existing.ChangePermissions(permissions).IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
    }
}
