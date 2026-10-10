using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Conexiones privadas explícitas a una única base efímera administrada por el runner.</summary>
public sealed record IsolatedDatabaseConnections(string Bootstrap, string Admin, string Runtime)
{
    public void Validate()
    {
        var bootstrap = new NpgsqlConnectionStringBuilder(Bootstrap);
        var admin = new NpgsqlConnectionStringBuilder(Admin);
        var runtime = new NpgsqlConnectionStringBuilder(Runtime);
        if (bootstrap.Host != admin.Host || admin.Host != runtime.Host
            || bootstrap.Port != admin.Port || admin.Port != runtime.Port
            || bootstrap.Database != "postgres" || admin.Database != "appdb" || runtime.Database != "appdb"
            || bootstrap.Username != "postgres" || admin.Username != "mt_owner" || runtime.Username != "mt_app"
            || string.IsNullOrWhiteSpace(bootstrap.Password) || string.IsNullOrWhiteSpace(admin.Password)
            || string.IsNullOrWhiteSpace(runtime.Password))
            throw new InvalidOperationException("Isolated connections must identify one PostgreSQL instance and its dedicated roles.");
    }

    public override string ToString() => nameof(IsolatedDatabaseConnections);
}
