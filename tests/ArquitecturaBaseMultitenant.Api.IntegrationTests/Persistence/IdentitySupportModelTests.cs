using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba el modelo global de configuración, eventos, outbox y legales. Protege versiones únicas y la
/// separación de documentos y aceptaciones.
/// </summary>
public sealed class IdentitySupportModelTests
{
    [Fact]
    public void Global_settings_events_outbox_and_legal_documents_use_platform_schema()
    {
        using var context = CreateContext();

        AssertSchema<PlatformSettings>(context, Schemas.Platform);
        AssertSchema<SecurityEvent>(context, Schemas.Platform);
        AssertSchema<OutboxMessage>(context, Schemas.Platform);
        AssertSchema<LegalDocument>(context, Schemas.Platform);
        AssertSchema<LegalDocumentContent>(context, Schemas.Platform);
        AssertSchema<LegalAcceptance>(context, Schemas.Identity);
    }

    [Fact]
    public void Legal_versions_and_texts_have_unique_keys_and_acceptances_remain_separate()
    {
        using var context = CreateContext();
        var document = context.Model.FindEntityType(typeof(LegalDocument));
        var content = context.Model.FindEntityType(typeof(LegalDocumentContent));
        var acceptance = context.Model.FindEntityType(typeof(LegalAcceptance));

        Assert.NotNull(document);
        Assert.NotNull(content);
        Assert.NotNull(acceptance);
        Assert.Contains(document.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["Kind", "Version"]));
        Assert.Equal(["LegalDocumentId", "Culture"],
            content.FindPrimaryKey()?.Properties.Select(property => property.Name));
        Assert.Contains(acceptance.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["UserId", "LegalDocumentId"]));
    }

    [Fact]
    public void Outbox_has_a_due_index_and_all_global_entities_have_no_tenant_filter()
    {
        using var context = CreateContext();
        var outbox = context.Model.FindEntityType(typeof(OutboxMessage));

        Assert.NotNull(outbox);
        Assert.Contains(outbox.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["Status", "NextAttemptAtUtc", "Id"]));
        foreach (var entity in new[] { typeof(PlatformSettings), typeof(SecurityEvent), typeof(OutboxMessage),
                     typeof(LegalDocument), typeof(LegalDocumentContent), typeof(LegalAcceptance) })
        {
            Assert.Empty(context.Model.FindEntityType(entity)!.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public void Audit_and_legal_proofs_are_immutable_except_acceptance_personal_metadata()
    {
        using var context = CreateContext();

        Assert.Equal(PropertySaveBehavior.Throw,
            context.Model.FindEntityType(typeof(SecurityEvent))!
                .FindProperty(nameof(SecurityEvent.OccurredAtUtc))!.GetAfterSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Throw,
            context.Model.FindEntityType(typeof(LoginAudit))!
                .FindProperty(nameof(LoginAudit.FailureCode))!.GetAfterSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Throw,
            context.Model.FindEntityType(typeof(LegalAcceptance))!
                .FindProperty(nameof(LegalAcceptance.Version))!.GetAfterSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Save,
            context.Model.FindEntityType(typeof(LegalAcceptance))!
                .FindProperty(nameof(LegalAcceptance.IpAddress))!.GetAfterSaveBehavior());
    }

    private static void AssertSchema<TEntity>(DbContext context, string expectedSchema) where TEntity : class =>
        Assert.Equal(expectedSchema, context.Model.FindEntityType(typeof(TEntity))?.GetSchema());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_support_model_test").Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
