using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>
/// Revisión de la base de la 12: cada comprobación del Asistente en Business (la puerta <c>assistant.use</c> y los permisos
/// de la herramienta) lee cada proyección de permisos una sola vez, con las mismas respuestas que las preguntas sueltas.
/// </summary>
public sealed class AssistantAccessPolicyTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid MemberCompany = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();
    private static readonly Guid InactiveCompany = Guid.CreateVersion7();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // La organización le da roles.read y company.members.read; la empresa donde es miembro, company.members.manage.
    [Theory]
    [InlineData(Permissions.Roles.Read, null, true)]
    [InlineData(Permissions.Audit.Read, null, false)]
    [InlineData(Permissions.Company.Members.Manage, null, true)]
    [InlineData(Permissions.Company.Members.Manage, "member", true)]
    [InlineData(Permissions.Company.Members.Manage, "other", false)]
    [InlineData(Permissions.Company.Members.Read, "other", true)]
    [InlineData(Permissions.Company.Members.Read, "inactive", false)]
    public async Task A_business_check_reads_each_permission_projection_once(string permission, string? company,
        bool allowed)
    {
        var cache = new CountingPermissionCache([Permissions.Assistant.Use, Permissions.Roles.Read, Permissions.Company.Members.Read],
            new Dictionary<Guid, IReadOnlyList<string>> { [MemberCompany] = [Permissions.Company.Members.Manage] });
        Guid? companyId = company switch
        {
            "member" => MemberCompany,
            "other" => OtherCompany,
            "inactive" => InactiveCompany,
            _ => null,
        };

        Assert.Equal(allowed, await Policy(cache).CanUseAsync(Access.Business, [permission], companyId, Ct));
        Assert.Equal((1, 1), (cache.OrganizationReads, cache.CompanyReads));
    }

    // assistant.use también llega desde una empresa (OrganizationProjectedFromCompany): la puerta lo ve en la misma lectura.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_gate_sees_a_use_projected_from_a_company_in_the_same_read(bool projected)
    {
        var cache = new CountingPermissionCache([Permissions.Roles.Read], new Dictionary<Guid, IReadOnlyList<string>>
        {
            [MemberCompany] = projected ? [Permissions.Assistant.Use] : [Permissions.Company.Members.Manage],
        });

        Assert.Equal(projected, await Policy(cache).CanUseAsync(Access.Business, [Permissions.Roles.Read], null, Ct));
        Assert.Equal((1, 1), (cache.OrganizationReads, cache.CompanyReads));
    }

    private static AssistantAccessPolicy Policy(CountingPermissionCache cache)
    {
        var tenant = new FakeTenantContext { TenantId = TenantId, TenantKind = TenantKind.Business };
        var permissions = new PermissionService(cache, tenant, new CompanyActivityReader(MemberCompany, OtherCompany),
            new FakeTimeProvider(), NullLogger<PermissionService>.Instance);
        var user = Stub.Of<ICurrentUser>(new()
        {
            ["get_UserId"] = _ => (Guid?)UserId,
            ["get_Access"] = _ => (Access?)Access.Business,
        });
        var session = new AssistantSessionPolicy(user, tenant,
            Stub.Of<IUserRepository>(new()
            {
                ["GetByIdAsync"] = _ => Task.FromResult<UserAccountRow?>(new(UserId, "Ana", "es-AR",
                    "America/Argentina/Buenos_Aires", UserStatus.Active, false, null, null)),
            }),
            Stub.Of<IUserTenantAccessReader>(new()
            {
                ["ListForUserAsync"] = _ => Task.FromResult<IReadOnlyList<UserTenantAccessRow>>(
                    [new(TenantId, TenantKind.Business, "Empresa A", "empresa-a", TenantStatus.Active, MemberStatus.Active, null)]),
            }),
            Stub.Of<ITenantReader>(new()
            {
                ["FindByIdAsync"] = _ => Task.FromResult<TenantRow?>(
                    new(TenantId, TenantKind.Business, TenantStatus.Active, "Empresa A")),
            }),
            Stub.Of<ILegalAcceptanceService>(new()
            {
                ["GetPendingAsync"] = _ => Task.FromResult(Result.Success<IReadOnlyList<PendingLegalDocumentResponse>>([])),
            }),
            Stub.Of<IUserStatusReader>(new() { ["GetStatusAsync"] = _ => Task.FromResult<UserStatus?>(UserStatus.Active) }));
        return new AssistantAccessPolicy(user, session,
            Stub.Of<IFeatureService>(new() { ["IsEnabledAsync"] = _ => Task.FromResult(true) }), permissions,
            Stub.Of<IPlatformPermissionService>(new()), Stub.Of<IAssistantNoticeReader>(new()));
    }

    /// <summary>Responde solo los miembros dados, por nombre; cualquier otro es una lectura que la prueba no espera.</summary>
    public class Stub : DispatchProxy
    {
        private Dictionary<string, Func<object?[], object?>> _answers = [];

        internal static T Of<T>(Dictionary<string, Func<object?[], object?>> answers) where T : class
        {
            var proxy = Create<T, Stub>();
            ((Stub)(object)proxy)._answers = answers;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod is not null && _answers.TryGetValue(targetMethod.Name, out var answer)
                ? answer(args ?? [])
                : throw new NotSupportedException(targetMethod?.Name);
    }
}
