using System.Collections.Concurrent;
using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// ADR 0052: los permisos efectivos se leen con consultas agrupadas. Cuántas consultas hace una carga en frío no depende
/// de cuántas empresas tiene la persona, y un permiso que no se proyecta desde una empresa nunca consulta empresas.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PermissionProjectionTests : IDisposable
{
    private const string TimeZone = "America/Argentina/Buenos_Aires";
    private readonly CommandLog log = new();
    private readonly WebApplicationFactory<Program> host;

    public PermissionProjectionTests(ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<ApplicationDbContext>(options => options.AddInterceptors(new CommandRecorder(log)));
            // Sin los workers periódicos de este host: sus consultas no se mezclan con las que se cuentan.
            foreach (var worker in services.Where(service => service.ServiceType == typeof(IHostedService)
                         && service.ImplementationType?.IsSubclassOf(typeof(PeriodicWorker)) == true).ToArray())
                services.Remove(worker);
        }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => host.Dispose();

    [Fact]
    public async Task A_cold_read_makes_the_same_queries_with_one_company_or_five()
    {
        var one = await ColdOrganizationQueriesAsync(companies: 1);
        var five = await ColdOrganizationQueriesAsync(companies: 5);

        Assert.NotEmpty(one);
        Assert.Equal(one.Count(ReadsPermissions), five.Count(ReadsPermissions));
    }

    [Fact]
    public async Task Only_a_projected_permission_reads_the_companies()
    {
        var (tenantId, memberUserId) = await NewOrganizationAsync(companies: 3);
        await using var scope = BusinessScope(tenantId);
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        log.Clear();
        Assert.False(await permissions.HasPermissionAsync(memberUserId, Permissions.Roles.Read, Ct));
        var organizationOnly = log.Take();
        Assert.NotEmpty(organizationOnly);
        Assert.DoesNotContain(organizationOnly, ReadsCompanies);

        Assert.True(await permissions.HasPermissionAsync(memberUserId, Permissions.Assistant.Use, Ct));
        Assert.Contains(log.Take(), ReadsCompanies);
    }

    private async Task<string[]> ColdOrganizationQueriesAsync(int companies)
    {
        var (tenantId, memberUserId) = await NewOrganizationAsync(companies);
        await using var scope = BusinessScope(tenantId);
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        log.Clear();
        var granted = await permissions.GetOrganizationPermissionsAsync(memberUserId, Ct);

        // El permiso llega sólo desde los roles de empresa: la proyección leyó las empresas.
        Assert.Contains(Permissions.Assistant.Use, granted);
        return log.Take();
    }

    private AsyncServiceScope BusinessScope(Guid tenantId)
    {
        var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        return scope;
    }

    // Una organización con su dueña y una persona más, miembro activa de N empresas con el rol de sistema de empresa,
    // el único que aporta un permiso proyectable (assistant.use).
    private async Task<(Guid TenantId, Guid MemberUserId)> NewOrganizationAsync(int companies)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var work = services.GetRequiredService<IUnitOfWork>();
        Guid ownerId = Guid.Empty, memberUserId = Guid.Empty;
        await work.ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            ownerId = (await users.CreateAsync(null, "es-AR", TimeZone, ct)).Id;
            memberUserId = (await users.CreateAsync(null, "es-AR", TimeZone, ct)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var tenant = Tenant.CreateBusiness("Proyección " + Guid.NewGuid().ToString("N")[..8], requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await work.ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", TimeZone, "ARS"), [ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var created = await work.ExecuteInTransactionAsync(async ct =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var member = Member.Invite(memberUserId);
            Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            context.Members.Add(member);
            var role = await context.Roles.SingleAsync(row => row.Name == SystemRoles.CompanyAdmin, ct);
            for (var index = 0; index < companies; index++)
            {
                var company = Company.Create("Empresa " + Guid.NewGuid().ToString("N")[..8], null, TimeZone).Value;
                context.Companies.Add(company);
                context.CompanyMemberships.Add(CompanyMembership.Create(company.Id, member.Id).Value);
                context.RoleAssignments.Add(RoleAssignment.Assign(member.Id, role.Id, role.Scope, role.CompanyId,
                    company.Id).Value);
            }

            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        return (tenant.Id, memberUserId);
    }

    private static bool ReadsPermissions(string command) =>
        ReadsCompanies(command) || command.Contains("\"CacheTagVersions\"", StringComparison.Ordinal)
        || command.Contains("\"Roles\"", StringComparison.Ordinal) || command.Contains("\"RoleAssignments\"", StringComparison.Ordinal)
        || command.Contains("\"Members\"", StringComparison.Ordinal);

    private static bool ReadsCompanies(string command) =>
        command.Contains("\"Companies\"", StringComparison.Ordinal)
        || command.Contains("\"CompanyMemberships\"", StringComparison.Ordinal);

    private sealed class CommandLog
    {
        private readonly ConcurrentQueue<string> _commands = new();

        internal void Add(string command) => _commands.Enqueue(command);

        internal void Clear() => _commands.Clear();

        internal string[] Take()
        {
            var commands = _commands.ToArray();
            _commands.Clear();
            return commands;
        }
    }

    private sealed class CommandRecorder(CommandLog log) : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            log.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
