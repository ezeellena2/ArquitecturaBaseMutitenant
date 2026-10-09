using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

public sealed class PickupCodeReaderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("es-AR")]
    [InlineData("en-US")]
    public async Task Invitation_URL_digits_are_ignored_before_the_real_authentication_email(string culture)
    {
        using var client = factory.CreateClient();
        await factory.Services.SeedDatabaseAsync(Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var recipient = $"pickup-selection-{Guid.NewGuid():N}@example.test";
        const string expectedCode = "654321";
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var profile = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync(culture, Ct);
        var renderer = services.GetRequiredService<IEmailTemplateRenderer>();
        var invitation = await renderer.RenderInvitationAsync(recipient,
            new InvitationNotice("Organización", "Invitante", nowUtc.AddDays(1), "UTC",
                "https://example.test/invitacion#fixture123456token", false), profile, Ct);
        var authentication = renderer.RenderLoginCode(recipient, expectedCode, 10, profile);
        var protector = services.GetRequiredService<IPayloadProtector>();
        var first = OutboxMessage.Enqueue("email", protector.Protect(JsonSerializer.Serialize(invitation)), nowUtc.AddMinutes(-1));
        var second = OutboxMessage.Enqueue("email", protector.Protect(JsonSerializer.Serialize(authentication)), nowUtc);
        var database = services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            database.OutboxMessages.AddRange(first, second);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        // El primer despacho sólo deja el MIME de invitación; el lector debe esperar el correo de código.
        Assert.Equal(expectedCode, await PickupCodeReader.ReadAsync(factory.Services, recipient, Ct));
        Assert.All(await database.OutboxMessages.AsNoTracking().Where(row => row.Id == first.Id || row.Id == second.Id).ToArrayAsync(Ct),
            row => Assert.Equal(OutboxStatus.Sent, row.Status));
    }
}
