using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B7 (ADR 0066 §3): la migración pasa los motivos guardados de WhatsApp a los códigos nuevos, no toca los de otro canal y su
/// Down devuelve los cuatro que solo cambiaron de nombre.
/// </summary>
public sealed class WhatsAppUncertainMigrationTests
{
    [Fact]
    public async Task Stored_send_failures_move_to_the_new_codes_and_back()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();
        const string previous = "20261125004346_InvitationDeliveryOutbox";
        const string current = "20261125013446_WhatsAppUncertainReview";
        await migrator.MigrateAsync(previous, ct);

        var expected = new Dictionary<string, string>
        {
            ["WhatsApp.Send.RecipientNotAllowed"] = WhatsAppSendFailures.NotAllowed,
            ["WhatsApp.Send.WindowExpired"] = WhatsAppSendFailures.WindowClosed,
            ["WhatsApp.Send.Credentials"] = WhatsAppSendFailures.TokenInvalid,
            ["WhatsApp.Send.Permissions"] = WhatsAppSendFailures.PermissionMissing,
            ["WhatsApp.Send.Network"] = WhatsAppSendFailures.Unavailable,
            ["WhatsApp.Send.Timeout"] = WhatsAppSendFailures.Unavailable,
            ["WhatsApp.Send.InvalidResponse"] = WhatsAppSendFailures.Rejected,
            ["WhatsApp.Send.ChannelUnavailable"] = WhatsAppSendFailures.ReadOnly,
            [WhatsAppSendFailures.Undeliverable] = WhatsAppSendFailures.Undeliverable,
        };
        var nowUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
        var rows = new Dictionary<Guid, string>();
        foreach (var code in expected.Keys)
        {
            var id = Guid.CreateVersion7(); rows.Add(id, code);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "LastErrorCode")
                VALUES ({id}, 'whatsapp', '', 'Failed', 3, {nowUtc}, {code});
                """, ct);
        }
        // Otro canal con un código parecido no es de WhatsApp: no se toca.
        var email = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "LastErrorCode")
            VALUES ({email}, 'email', '', 'Failed', 3, {nowUtc}, 'WhatsApp.Send.Network');
            """, ct);

        await migrator.MigrateAsync(current, ct);
        var up = await CodesAsync(context, [.. rows.Keys, email], ct);
        Assert.All(rows, row => Assert.Equal(expected[row.Value], up[row.Key]));
        Assert.Equal("WhatsApp.Send.Network", up[email]);
        Assert.False(context.Database.HasPendingModelChanges());

        await migrator.MigrateAsync(previous, ct);
        var down = await CodesAsync(context, [.. rows.Keys], ct);
        string[] renamed = ["WhatsApp.Send.RecipientNotAllowed", "WhatsApp.Send.WindowExpired", "WhatsApp.Send.Credentials", "WhatsApp.Send.Permissions"];
        Assert.All(rows.Where(row => renamed.Contains(row.Value)), row => Assert.Equal(row.Value, down[row.Key]));
        Assert.All(rows.Where(row => !renamed.Contains(row.Value)), row => Assert.Equal(expected[row.Value], down[row.Key]));
        await migrator.MigrateAsync(cancellationToken: ct);
    }

    private static async Task<Dictionary<Guid, string?>> CodesAsync(ApplicationDbContext context, Guid[] ids, CancellationToken ct) =>
        await context.Database.SqlQuery<CodeRow>($"""
            SELECT "Id", "LastErrorCode" FROM platform."OutboxMessages" WHERE "Id" = ANY({ids})
            """).ToDictionaryAsync(row => row.Id, row => row.LastErrorCode, ct);

    private sealed record CodeRow(Guid Id, string? LastErrorCode);
}
