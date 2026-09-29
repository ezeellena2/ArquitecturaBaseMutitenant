using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

internal sealed class WidgetQueryFixture : IAsyncDisposable
{
    public static readonly SortMap<Widget> SortableFields = new()
    {
        ["name"] = widget => widget.Name,
        ["createdAtUtc"] = widget => widget.CreatedAtUtc,
    };

    private readonly NpgsqlConnection _connection;

    private WidgetQueryFixture(NpgsqlConnection connection, IsolationApplicationDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public IsolationApplicationDbContext Context { get; }

    public static async Task<WidgetQueryFixture> CreateAsync(
        ApiFactory factory, Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, cancellationToken);
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql(connection)
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        return new WidgetQueryFixture(connection,
            new IsolationApplicationDbContext(options, new QueryTenantContext(tenantId)));
    }

    public async Task AddAsync(Guid tenantId, Guid id, string name, DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."Widgets" ("TenantId", "Id", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES (@tenant_id, @id, @name, @created_at_utc, false)
            """, _connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("created_at_utc", createdAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public IQueryable<Widget> Rows => Context.Set<Widget>().AsNoTracking();

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class QueryTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => tenantId;
    }
}
