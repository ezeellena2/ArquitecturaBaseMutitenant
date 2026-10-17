using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Control por stdin/stdout privado del runner, siempre sobre ApiFactory Testing y su base propia.</summary>
public sealed partial class WhatsAppBrowserControl
{
    public const string OperatorEmail = "e8-operator@example.test";
    public const string OperatorPhone = "+5491134567899";
    public const string AppSecret = "browser-fixture-secret";
    public const string NumberId = "browser-fixture-number";
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, ScenarioCursor> _scenarios = new(StringComparer.Ordinal);

    public WhatsAppBrowserControl(IServiceProvider services)
    {
        if (!services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
            throw new InvalidOperationException("Fixture requires Testing.");
        _services = services;
    }

    public async Task<object> ExecuteAsync(WhatsAppBrowserRequest request, HttpClient client, CancellationToken ct)
    {
        if (!request.IsValid()) throw new InvalidOperationException("Invalid fixture request.");
        var nowUtc = _services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (request.Kind == "begin")
        {
            Guid? ownerId = null;
            Guid[] existingMethods = [];
            if (request.Scenario is "backup-email-1440" or "backup-email-390")
            {
                await using var beginScope = _services.CreateAsyncScope();
                var beginDb = beginScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                ownerId = await PrimaryPhoneOwnerAsync(beginDb, request.Phone, ct)
                    ?? throw new InvalidOperationException("Fixture phone owner not found.");
                existingMethods = await beginDb.LoginMethods.AsNoTracking().Where(method => method.UserId == ownerId)
                    .Select(method => method.Id).ToArrayAsync(ct);
            }
            if (!_scenarios.TryAdd(request.Scenario, new ScenarioCursor(request.Phone, nowUtc, ownerId, existingMethods)))
                throw new InvalidOperationException("Fixture scenario already exists.");
            return new { ok = true };
        }
        if (!_scenarios.TryGetValue(request.Scenario, out var scenario) || scenario.Phone != request.Phone)
            throw new InvalidOperationException("Fixture scenario does not own this destination.");
        if (request.Kind.StartsWith("org", StringComparison.Ordinal))
            return await OrganizationAsync(request, scenario, client, nowUtc, ct);
        if (request.Kind == "inbound")
        {
            var payload = JsonSerializer.Serialize(new
            {
                @object = "whatsapp_business_account", entry = new[] { new { id = "fixture-waba", changes = new[] { new
                {
                    field = "messages", value = new { messaging_product = "whatsapp", metadata = new { phone_number_id = NumberId },
                        messages = new[] { new { from = request.Phone[1..], id = "wamid.fixture." + request.Scenario,
                            timestamp = new DateTimeOffset(nowUtc).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                            type = "text", text = new { body = "Hola" } } } }
                } } } }
            });
            using var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            message.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(payload))));
            using var response = await client.SendAsync(message, ct);
            return new { status = (int)response.StatusCode };
        }
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (request.Kind == "recoveryNotice")
            return await RecoveryNoticeAsync(services, db, request, scenario, ct);
        if (request.Kind == "methodEmailCode")
        {
            var ownerId = await PrimaryPhoneOwnerAsync(db, scenario.Phone, ct);
            if (ownerId is null || ownerId != scenario.OwnerId || scenario.ExistingMethods.Contains(request.MethodId!.Value)
                || !await db.LoginMethods.AsNoTracking().AnyAsync(method => method.Id == request.MethodId
                    && method.UserId == ownerId && method.Type == LoginMethodType.Email && method.Value == request.Email
                    && method.VerifiedAtUtc == null && method.ManagedByTenantId == null && method.InvitationOriginTenantId == null, ct)
                || !await db.LoginCodes.AsNoTracking().AnyAsync(code => code.RequestedByUserId == ownerId
                    && code.Destination == request.Email && code.Channel == "email" && code.Purpose == LoginCodePurpose.VerifyDestination
                    && code.CreatedAtUtc >= scenario.SinceUtc && code.SentAtUtc != null && code.ConsumedAtUtc == null
                    && code.InvalidatedAtUtc == null && code.ExpiresAtUtc > nowUtc, ct))
                throw new InvalidOperationException("Fixture method is not owned by this scenario.");
        }
        if (request.Kind == "expireTicket")
        {
            var hash = services.GetRequiredService<ISecureTokenGenerator>().Hash(request.Ticket!);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                var ids = db.LoginMethods.Where(method => method.Type == LoginMethodType.Phone && method.Value == scenario.Phone).Select(method => method.UserId);
                var count = await db.AccountDeletionCancelTickets.Where(ticket => ticket.TokenHash == hash
                        && ids.Contains(ticket.UserId) && ticket.IssuedAtUtc >= scenario.SinceUtc && ticket.ConsumedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(ticket => ticket.ExpiresAtUtc, nowUtc.AddSeconds(-1)), token);
                return count == 1 ? Result.Success() : Result.Failure(Error.Validation("Fixture.Ticket.NotOwned", "Fixture ticket is not owned by this scenario."));
            }, CommitPolicy.OnSuccess, ct);
            if (result.IsFailure) throw new InvalidOperationException("Fixture ticket is not owned by this scenario.");
            return new { ok = true };
        }
        if (request.Kind == "events")
        {
            var contacts = db.Set<WhatsAppContact>().Where(contact => contact.WaId == scenario.Phone).Select(contact => contact.Id);
            var events = await db.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.ContactId != null
                    && contacts.Contains(value.ContactId.Value) && value.ReceivedAtUtc >= scenario.SinceUtc)
                .OrderBy(value => value.ReceivedAtUtc).Select(value => new { kind = value.Kind.ToString(), direction = value.Direction.ToString(), status = value.Status.ToString(), processed = value.ProcessedAtUtc != null }).ToArrayAsync(ct);
            return new { events };
        }
        if (request.Kind == "bot")
        {
            var inbound = services.GetRequiredService<IWhatsAppInboundService>();
            for (var i = 0; i < 100; i++)
            {
                var claim = await inbound.ClaimAsync(ct);
                if (claim.IsFailure) throw new InvalidOperationException("Fixture claim failed.");
                if (claim.Value is null) break;
                if ((await inbound.ProcessAsync(claim.Value, ct)).IsFailure) throw new InvalidOperationException("Fixture bot failed.");
            }
        }
        var channel = request.Kind is "operatorCode" or "methodEmailCode" ? "email" : "whatsapp";
        var destination = request.Kind == "operatorCode" ? OperatorEmail : request.Kind == "methodEmailCode" ? request.Email : scenario.Phone;
        var protector = services.GetRequiredService<IPayloadProtector>();
        var rows = await db.OutboxMessages.AsNoTracking().Where(row => row.Channel == channel && row.EncryptedPayload != "" && row.CreatedAtUtc >= scenario.SinceUtc
                && (request.Kind != "methodEmailCode" || row.UserId == scenario.OwnerId))
            .OrderByDescending(row => row.CreatedAtUtc).ToArrayAsync(ct);
        foreach (var row in rows)
        {
            using var payload = JsonDocument.Parse(protector.Unprotect(row.EncryptedPayload));
            var value = payload.RootElement;
            if (value.GetProperty("To").GetString() != destination) continue;
            string? proof = null;
            if (request.Kind == "bot" && value.TryGetProperty("LinkUrl", out var url) && url.ValueKind == JsonValueKind.String) proof = url.GetString();
            if (request.Kind == "code" && value.TryGetProperty("TemplateName", out var template) && template.GetString() == "codigo_ingreso") proof = value.GetProperty("BodyParameters")[0].GetString();
            if (request.Kind is "operatorCode" or "methodEmailCode")
            {
                var match = Regex.Match(value.GetProperty("TextBody").GetString() ?? "", @"(?m)^(?<code>\d{6})\r?$", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (match.Success) proof = match.Groups["code"].Value;
            }
            if (proof is not null) return new PrivateProof(proof);
        }
        throw new InvalidOperationException("Fixture proof not found.");
    }

    private static Task<Guid?> PrimaryPhoneOwnerAsync(ApplicationDbContext db, string phone, CancellationToken ct) =>
        db.LoginMethods.AsNoTracking().Where(method => method.Type == LoginMethodType.Phone && method.Value == phone
            && method.IsPrimary && method.VerifiedAtUtc != null && method.ManagedByTenantId == null && method.InvitationOriginTenantId == null)
            .Select(method => (Guid?)method.UserId).SingleOrDefaultAsync(ct);

    private static async Task<object> RecoveryNoticeAsync(IServiceProvider services, ApplicationDbContext db,
        WhatsAppBrowserRequest request, ScenarioCursor scenario, CancellationToken ct)
    {
        var recovery = await db.AccountRecoveryRequests.AsNoTracking().SingleOrDefaultAsync(value => value.Id == request.RecoveryId
            && value.NewMethodType == LoginMethodType.Phone && value.NewMethodValue == scenario.Phone
            && value.CreatedAtUtc >= scenario.SinceUtc && value.Status == AccountRecoveryStatus.Pending && value.UserId != null, ct)
            ?? throw new InvalidOperationException("Fixture recovery is not owned by this scenario.");
        var beforeIds = await db.OutboxMessages.AsNoTracking().Select(row => row.Id).ToArrayAsync(ct);
        // El servicio real posee su UoW y procesa su tanda global en esta base aislada.
        var result = await services.GetRequiredService<IAccountRecoveryNoticeService>().NotifyAccountsAsync(ct);
        if (result.IsFailure) throw new InvalidOperationException("Fixture recovery notice failed.");
        var after = await db.AccountRecoveryRequests.AsNoTracking().SingleAsync(value => value.Id == recovery.Id, ct);
        var marked = await db.AccountRecoveryNotices.AsNoTracking().AnyAsync(value => value.Id == recovery.Id, ct);
        var queuedNotices = await db.OutboxMessages.AsNoTracking().CountAsync(row => !beforeIds.Contains(row.Id)
            && row.UserId == recovery.UserId && row.EncryptedPayload != "" && (row.Channel == "email" || row.Channel == "whatsapp"), ct);
        if (after.Version != recovery.Version || after.Status != AccountRecoveryStatus.Pending)
            throw new InvalidOperationException("Fixture recovery changed during notice.");
        return new { marked, queuedNotices, beforeVersion = recovery.Version, afterVersion = after.Version };
    }

    private sealed record ScenarioCursor(string Phone, DateTime SinceUtc, Guid? OwnerId, Guid[] ExistingMethods)
    {
        public override string ToString() => nameof(ScenarioCursor);
    }

    private sealed record PrivateProof([property: System.Text.Json.Serialization.JsonPropertyName("proof")] string Proof)
    {
        public override string ToString() => nameof(PrivateProof);
    }
}
