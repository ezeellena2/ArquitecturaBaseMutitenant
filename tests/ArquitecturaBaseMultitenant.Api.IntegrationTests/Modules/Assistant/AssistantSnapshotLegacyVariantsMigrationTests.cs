using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantSnapshotLegacyVariantsMigrationTests
{
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task All_ten_real_Quick_queries_upgrade_new_and_existing_conversations_and_Writer_Existing_replays_the_original()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var ct = deadline.Token;
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var ownerClient = AssistantTestData.Client(factory);
        using var targetClient = AssistantTestData.Client(factory);
        using var invitedClient = AssistantTestData.Client(factory);
        using var operatorClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, ct);
        var personalTenant = await AssistantTestData.ActiveTenantAsync(ownerClient);
        var target = await AccountJourney.RegisterAsync(factory, targetClient, ct);
        var invited = await AccountJourney.RegisterAsync(factory, invitedClient, ct);
        var operatorEmail = Email.Create("legacy-operator-" + Guid.CreateVersion7().ToString("N") + "@example.test").Value;
        AccountJourney platformOwner;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var user = await users.CreateAsync("Legacy Operator", "es-AR", "America/Argentina/Buenos_Aires", token);
                await users.SetPrimaryEmailAsync(user.Id, operatorEmail, token);
                var method = LoginMethod.CreateEmail(user.Id, operatorEmail);
                Assert.True(method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                Assert.True(method.MakePrimary().IsSuccess);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success(user.Id);
            }, CommitPolicy.OnSuccess, ct);
            platformOwner = new AccountJourney(created.Value, operatorEmail);
        }
        var businessTenant = await OrganizationJourney.StageAsync(factory, owner, "Legacy variant sources", ct);
        var targetMember = await OrganizationJourney.AddActiveMemberAsync(factory, businessTenant, target.UserId, ct);
        var linkedMember = Member.Invite(invited.UserId);
        var unlinkedMember = Member.Invite();
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var linkedInvitation = Invitation.Issue(linkedMember.Id, owner.UserId, invited.Email, "email", "linked-hash", nowUtc.AddDays(-2), TimeSpan.FromHours(1));
        var unlinkedInvitation = Invitation.Issue(unlinkedMember.Id, owner.UserId, Email.Create("unlinked-legacy@example.test").Value,
            "email", "unlinked-hash", nowUtc.AddDays(-2), TimeSpan.FromHours(1));
        var userAudit = AuditEntry.Create(businessTenant, AuditActorKind.User, target.UserId, AuditAction.Custom, "LegacyFixture", targetMember, "{}", nowUtc);
        var systemAudit = AuditEntry.Create(businessTenant, AuditActorKind.System, null, AuditAction.Custom, "LegacyFixture", targetMember, "{}", nowUtc);
        var platformApproval = SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationApproved, platformOwner.UserId,
            businessTenant, "Legacy platform label fixture", nowUtc);
        var securityActor = SecurityEvent.ForOrganizationMembershipEnded(target.UserId, businessTenant, invited.UserId, nowUtc);
        Assert.NotEqual(securityActor.ActorId, securityActor.TargetAccountId);
        var loginAudit = LoginAudit.Success(target.UserId, LoginAuditMethod.Code, nowUtc);
        var recovery = AccountRecoveryRequest.Submit(Guid.CreateVersion7(), LoginMethodType.Email, target.Email.Value,
            Email.Create("legacy-recovery@example.test").Value, target.UserId, nowUtc);
        var pendingOrganization = Tenant.CreateBusiness("Pending legacy organization", requiresApproval: true);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(businessTenant);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                await users.FillMissingProfileNamesAsync(owner.UserId, "Legacy", "Owner", token);
                await users.FillMissingProfileNamesAsync(target.UserId, "Legacy", "Subject", token);
                await users.FillMissingProfileNamesAsync(invited.UserId, "Legacy", "Invited", token);
                await users.FillMissingProfileNamesAsync(platformOwner.UserId, "Legacy", "Operator", token);
                services.GetRequiredService<ApplicationDbContext>().AddRange(linkedMember, unlinkedMember, linkedInvitation, unlinkedInvitation,
                    userAudit, systemAudit, platformApproval, securityActor, loginAudit, recovery, pendingOrganization);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct)).IsSuccess);
        }
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(ownerClient);
        var cases = new List<LegacyCase>();
        await CaptureVariantsAsync(ownerClient, "/api/me/assistant", Access.Consumer, personalTenant,
            [("my_organizations", "organizations"), ("my_signin", "account"), ("my_messages", "interactions")]);
        await AssistantTestData.EnableAsync(factory, businessTenant);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", ct, businessTenant);
        await AssistantExecutionActorTests.AcceptNoticeAsync(ownerClient, "/api/organization/assistant/notice");
        await CaptureVariantsAsync(ownerClient, "/api/organization/assistant", Access.Business, businessTenant,
            [("active_users", "people"), ("owners", "people"), ("expired_invitations", "invitations"), ("recent_changes", "audit")]);
        await PlatformOperators.GrantAsync(factory.Services, platformOwner.UserId, PlatformOperators.All, ct);
        using (var requested = await AccountJourney.PostAsync(operatorClient, "/api/auth/login-code", new { email = operatorEmail.Value }, ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, operatorEmail.Value, ct);
        using (var verified = await AccountJourney.PostAsync(operatorClient, "/api/auth/login-code/verify",
            new { email = operatorEmail.Value, code, returnUrl = "/connect/authorize?access=platform" }, ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", ct);
        using (var me = await operatorClient.GetFromJsonAsync<JsonDocument>("/api/me", ct))
        {
            var documents = me!.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray()
                .Select(row => new { id = row.GetProperty("id").GetGuid(), version = row.GetProperty("version").GetInt32() }).ToArray();
            using var accepted = await AccountJourney.PostAsync(operatorClient, "/api/legal/accept", new { documents }, ct);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        }
        await AssistantExecutionActorTests.AcceptNoticeAsync(operatorClient, "/api/platform/assistant/notice");
        await CaptureVariantsAsync(operatorClient, "/api/platform/assistant", Access.Platform, null,
            [("pending_organizations", "organizations"), ("pending_recoveries", "recoveries"), ("today_security", "security")]);
        // There are ten Quick keys with implemented results; assistant_usage is an honest 501 and produces no snapshot.
        Assert.Equal(20, cases.Count);
        Assert.Equal(10, cases.Select(value => value.QuickKey).Distinct().Count());
        Assert.Equal(10, cases.Count(value => value.Input.ConversationId is null));
        Assert.Equal(10, cases.Count(value => value.Input.ConversationId is not null));
        foreach (var value in cases.Where(value => value.QuickKey == "active_users"))
        {
            Assert.Contains(target.UserId, value.Subjects); Assert.Contains(owner.UserId, value.Subjects);
            AssertContainsSource(value, "People", targetMember);
        }
        foreach (var value in cases.Where(value => value.QuickKey == "owners")) Assert.Equal(new[] { owner.UserId }, value.Subjects);
        foreach (var value in cases.Where(value => value.QuickKey == "expired_invitations"))
        {
            Assert.Equal(new[] { invited.UserId }, value.Subjects);
            AssertContainsSource(value, "Invitations", linkedInvitation.Id); AssertContainsSource(value, "Invitations", unlinkedInvitation.Id);
        }
        foreach (var value in cases.Where(value => value.QuickKey == "recent_changes"))
        {
            Assert.Contains(target.UserId, value.Subjects); Assert.DoesNotContain(platformOwner.UserId, value.Subjects);
            AssertContainsSource(value, "Changes", userAudit.Id); AssertContainsSource(value, "Changes", systemAudit.Id);
            AssertContainsSource(value, "Changes", platformApproval.Id);
        }
        foreach (var value in cases.Where(value => value.QuickKey == "pending_recoveries"))
        { Assert.Equal(new[] { target.UserId }, value.Subjects); AssertContainsSource(value, "Recoveries", recovery.Id); }
        foreach (var value in cases.Where(value => value.QuickKey == "pending_organizations")) AssertContainsSource(value, "Organizations", pendingOrganization.Id);
        foreach (var value in cases.Where(value => value.QuickKey == "today_security"))
        {
            Assert.Contains(target.UserId, value.Subjects);
            AssertContainsSource(value, "Events", securityActor.Id); AssertContainsSource(value, "Events", loginAudit.Id);
        }
        foreach (var value in cases.Where(value => value.QuickKey is "my_organizations" or "pending_organizations" or "my_messages")) Assert.Empty(value.Subjects);
        foreach (var value in cases.Where(value => value.QuickKey == "my_signin")) Assert.Equal(new[] { owner.UserId }, value.Subjects);

        // Explicit typed legacy projections cover non-personal labels and ActorId versus TargetAccountId.
        var auditTemplate = cases.First(value => value.QuickKey == "recent_changes");
        var auditSnapshot = AssistantSnapshotBuilder.Restore(auditTemplate.SnapshotJson, 0);
        auditSnapshot = auditSnapshot with { Result = new AssistantQuickResultResponse("Original labels", [], new AssistantAuditQuickDataResponse(
            [new(userAudit.Id, "fixture", "Original actor", nowUtc), new(systemAudit.Id, "fixture", "System", nowUtc),
             new(platformApproval.Id, "fixture", "Platform", nowUtc)], null, false)) };
        var legacyAudit = AssistantDurableOperation.Create(owner.UserId, Access.Business, Guid.CreateVersion7(), "legacy-audit-hash",
            auditTemplate.Request.Route, auditSnapshot.Conversation.Id, "assistant.quick", 200, null,
            AssistantSnapshotBuilder.Serialize(auditSnapshot), "recent_changes", nowUtc);
        var securityTemplate = cases.First(value => value.QuickKey == "today_security");
        var systemSecurity = SecurityEvent.ForPlatformOperatorGrant("Legacy system actor fixture", nowUtc, invited.UserId);
        var securitySnapshot = AssistantSnapshotBuilder.Restore(securityTemplate.SnapshotJson, 0);
        securitySnapshot = securitySnapshot with { Result = new AssistantQuickResultResponse("Original security actors", [], new AssistantSecurityQuickDataResponse(
            [new(securityActor.Id, "fixture", "Original actor", nowUtc), new(loginAudit.Id, "fixture", "Original actor", nowUtc),
             new(systemSecurity.Id, "fixture", "System", nowUtc)], null, false)) };
        var legacySecurity = PlatformAssistantDurableOperation.Create(platformOwner.UserId, Guid.CreateVersion7(), "legacy-security-hash",
            securityTemplate.Request.Route, securitySnapshot.Conversation.Id, "assistant.quick", 200, null,
            AssistantSnapshotBuilder.Serialize(securitySnapshot), "today_security", nowUtc);
        var usageSnapshot = securitySnapshot with { Result = new AssistantQuickResultResponse("Unsupported historical usage", [], new AssistantUsageQuickDataResponse(
            new AssistantUsagePageResponse("2026-10", new Money(0m, CurrencyCode.Create("USD").Value), null, null, 0, 0, new([], 1, 20, 0), null, null))) };
        var unsupportedUsage = PlatformAssistantDurableOperation.Create(platformOwner.UserId, Guid.CreateVersion7(), "usage-hash", "/legacy/usage",
            securitySnapshot.Conversation.Id, "assistant.quick", 200, null, AssistantSnapshotBuilder.Serialize(usageSnapshot), "assistant_usage", nowUtc);
        var mismatchedVariant = PlatformAssistantDurableOperation.Create(platformOwner.UserId, Guid.CreateVersion7(), "mismatch-hash", "/legacy/mismatch",
            securitySnapshot.Conversation.Id, "assistant.quick", 200, null, AssistantSnapshotBuilder.Serialize(securitySnapshot), "pending_recoveries", nowUtc);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(businessTenant);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().AddRange(legacyAudit, legacySecurity, unsupportedUsage, mismatchedVariant, systemSecurity,
                    AssistantSnapshotRetentionIndex.Create(legacyAudit.Id, businessTenant, nowUtc),
                    AssistantSnapshotRetentionIndex.Create(legacySecurity.Id, null, nowUtc),
                    AssistantSnapshotRetentionIndex.Create(unsupportedUsage.Id, null, nowUtc),
                    AssistantSnapshotRetentionIndex.Create(mismatchedVariant.Id, null, nowUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct)).IsSuccess);
            var repository = services.GetRequiredService<IAssistantDurableRepository>();
            AddTypedCase(auditTemplate, (await repository.GetByIdAsync(owner.UserId, Access.Business, legacyAudit.Id, ct))!, legacyAudit.Key, [target.UserId]);
            AddTypedCase(securityTemplate, (await repository.GetByIdAsync(platformOwner.UserId, Access.Platform, legacySecurity.Id, ct))!, legacySecurity.Key, [target.UserId]);
        }
        var usageRequest = Guid.CreateVersion7();
        using (var usage = await AssistantDurableConversationTests.PostAsync(operatorClient, "/api/platform/assistant/quick/assistant_usage", "{}", usageRequest))
            Assert.Equal(HttpStatusCode.OK, usage.StatusCode);
        using (var read = factory.Services.CreateScope())
        {
            var currentUsage = await read.ServiceProvider.GetRequiredService<IAssistantDurableRepository>().GetByKeyAsync(platformOwner.UserId, Access.Platform, usageRequest, ct);
            Assert.NotNull(currentUsage);
            Assert.Equal("assistant.quick", currentUsage.Reference.Kind);
            Assert.Equal("assistant_usage", currentUsage.QuickKey);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(factory.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using (var migrationDb = new ApplicationDbContext(options.Options, new NoTenant()))
        {
            var migrator = migrationDb.GetService<IMigrator>();
            await migrator.MigrateAsync("20261010223305_AssistantEditableModelCatalog", ct);
            await migrator.MigrateAsync("20261011141438_AssistantSnapshotRetention", ct);
            Assert.False(migrationDb.Database.HasPendingModelChanges());
        }
        using (var read = factory.Services.CreateScope())
        {
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var original in new[] { unsupportedUsage, mismatchedVariant })
            {
                var purged = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == original.Id, ct);
                Assert.Null(purged.SnapshotJson); Assert.Null(purged.QuickKey); Assert.Empty(purged.SubjectUserIds);
                Assert.Equal(original.Version, purged.OriginalSnapshotVersion); Assert.NotEqual(original.Version, purged.Version);
                Assert.Equal(original.Key, purged.Key); Assert.Equal(original.BodyHash, purged.BodyHash); Assert.Equal(original.Route, purged.Route);
                Assert.Equal(original.Kind, purged.Kind); Assert.Equal(original.ConversationId, purged.ConversationId);
                Assert.False(await db.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == original.Id, ct));
            }
        }
        await using (var migrationDb = new ApplicationDbContext(options.Options, new NoTenant()))
            await migrationDb.GetService<IMigrator>().MigrateAsync(null, ct);
        foreach (var value in cases)
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var revalidated = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(value.Actor, ct);
            Assert.True(revalidated.IsSuccess, revalidated.Error.Code);
            using var actor = services.GetRequiredService<IExecutionActorScope>().Enter(revalidated.Value);
            var db = services.GetRequiredService<ApplicationDbContext>();
            var repository = services.GetRequiredService<IAssistantDurableRepository>();
            var upgraded = await repository.GetByKeyAsync(value.Actor.OwnerUserId, value.Actor.Access, value.Request.Key, ct);
            Assert.NotNull(upgraded);
            Assert.Equal(value.Version, upgraded.Version); Assert.Equal(value.SnapshotJson, upgraded.SnapshotJson);
            Assert.Equal(value.Subjects, upgraded.SubjectUserIds);
            var index = await db.Set<AssistantSnapshotRetentionIndex>().AsNoTracking().SingleAsync(row => row.OperationId == value.Id, ct);
            Assert.Equal(value.Actor.TenantId, index.ScopeTenantId); Assert.Equal(value.CreatedAtUtc.AddHours(24), index.ExpiresAtUtc);
            if (value.Actor.Access == Access.Platform)
            {
                var row = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == value.Id, ct);
                Assert.Equal(value.Version, row.OriginalSnapshotVersion); Assert.NotEqual(value.Version, row.Version);
            }
            else
            {
                var row = await db.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == value.Id, ct);
                Assert.Equal(value.Version, row.OriginalSnapshotVersion); Assert.NotEqual(value.Version, row.Version);
            }
            // Exercise the writer's Existing branch directly. HTTP durable preflight would short-circuit it.
            var context = new BoundRequest(value.Request);
            var builder = ActivatorUtilities.CreateInstance<AssistantQuickPreparationBuilder>(services, context);
            var prepared = await builder.PrepareAsync(value.QuickKey, value.Input, ct);
            Assert.True(prepared.IsSuccess, prepared.Error.Code); Assert.NotNull(prepared.Value.Existing); Assert.Null(prepared.Value.Composition);
            var writer = ActivatorUtilities.CreateInstance<AssistantConversationWriter>(services, context);
            var beforeMessages = value.Actor.Access == Access.Platform
                ? await db.Set<PlatformAssistantMessage>().CountAsync(row => row.ConversationId == upgraded.ConversationId, ct)
                : await db.Set<AssistantMessage>().CountAsync(row => row.ConversationId == upgraded.ConversationId, ct);
            var written = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                c => writer.QuickAsync(prepared.Value, c), CommitPolicy.OnSuccess, ct);
            Assert.True(written.IsSuccess, written.Error.Code); Assert.Null(written.Value.Row);
            Assert.Equal(upgraded.Reference, written.Value.Reference);
            Assert.Equal(value.RestoredSnapshot, AssistantSnapshotBuilder.Serialize(written.Value.AfterSave()));
            var afterMessages = value.Actor.Access == Access.Platform
                ? await db.Set<PlatformAssistantMessage>().CountAsync(row => row.ConversationId == upgraded.ConversationId, ct)
                : await db.Set<AssistantMessage>().CountAsync(row => row.ConversationId == upgraded.ConversationId, ct);
            Assert.Equal(beforeMessages, afterMessages);
            Assert.Equal(value.Version, (await repository.GetByIdAsync(value.Actor.OwnerUserId, value.Actor.Access, value.Id, ct))!.Version);
        }

        async Task CaptureVariantsAsync(HttpClient client, string prefix, Access access, Guid? tenantId, (string Key, string Variant)[] variants)
        {
            var actor = await AssistantExecutionActorTests.CaptureAsync(client);
            Assert.Equal(access, actor.Access); Assert.Equal(tenantId, actor.TenantId);
            foreach (var (key, variant) in variants)
            {
                await CaptureAsync(null, null);
                using var created = await AssistantDurableConversationTests.PostAsync(client, prefix + "/conversations", "{}", Guid.CreateVersion7());
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                using var creation = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
                await CaptureAsync(creation.RootElement.GetProperty("id").GetGuid(), creation.RootElement.GetProperty("version").GetUInt32());

                async Task CaptureAsync(Guid? conversationId, uint? version)
                {
                    var input = new AssistantQuickRequest(conversationId, version);
                    var requestBody = conversationId is null ? "{}" : JsonSerializer.Serialize(input, RequestJson);
                    var requestKey = Guid.CreateVersion7(); var route = prefix + "/quick/" + key;
                    using var response = await AssistantDurableConversationTests.PostAsync(client, route, requestBody, requestKey);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    using var read = factory.Services.CreateScope();
                    var services = read.ServiceProvider;
                    using var tenant = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
                    var row = await services.GetRequiredService<IAssistantDurableRepository>().GetByKeyAsync(actor.OwnerUserId, access, requestKey, ct);
                    Assert.NotNull(row); Assert.NotNull(row.SnapshotJson);
                    using var json = JsonDocument.Parse(row.SnapshotJson);
                    Assert.Equal(variant, json.RootElement.GetProperty("Result").GetProperty("Data").GetProperty("$type").GetString());
                    var subjects = key == "my_signin" ? new[] { owner.UserId } : row.SubjectUserIds.Order().ToArray();
                    cases.Add(new LegacyCase(row.Reference.Id, key, actor, input, new IdempotentRequest(requestKey, row.BodyHash, route, "https://localhost"),
                        row.Version, row.CreatedAtUtc, row.SnapshotJson, subjects, AssistantSnapshotBuilder.Serialize(AssistantSnapshotBuilder.Restore(row.SnapshotJson, row.Version))));
                }
            }
        }
        void AddTypedCase(LegacyCase template, AssistantDurableOperationRow row, Guid key, Guid[] subjects) =>
            cases.Add(new LegacyCase(row.Reference.Id, template.QuickKey, template.Actor, new AssistantQuickRequest(null, null),
                new IdempotentRequest(key, row.BodyHash, row.Route, "https://localhost"), row.Version, row.CreatedAtUtc, row.SnapshotJson!, subjects,
                AssistantSnapshotBuilder.Serialize(AssistantSnapshotBuilder.Restore(row.SnapshotJson!, row.Version))));
    }

    private static void AssertContainsSource(LegacyCase value, string array, Guid id)
    {
        using var json = JsonDocument.Parse(value.SnapshotJson);
        Assert.Contains(json.RootElement.GetProperty("Result").GetProperty("Data").GetProperty(array).EnumerateArray(),
            item => item.GetProperty("Id").GetGuid() == id);
    }
    private sealed record LegacyCase(Guid Id, string QuickKey, ExecutionActor Actor, AssistantQuickRequest Input, IdempotentRequest Request,
        uint Version, DateTime CreatedAtUtc, string SnapshotJson, Guid[] Subjects, string RestoredSnapshot);
    private sealed record BoundRequest(IdempotentRequest Request) : IIdempotentRequestContext;
    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
