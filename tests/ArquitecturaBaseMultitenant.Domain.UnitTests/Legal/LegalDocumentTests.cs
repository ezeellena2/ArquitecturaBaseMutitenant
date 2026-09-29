using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Legal;

public sealed class LegalDocumentTests
{
    private static readonly DateTime EffectiveAtUtc = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Document_has_a_version_and_separate_text_for_each_culture()
    {
        var document = LegalDocument.Create(LegalDocumentKind.Terms, 1, EffectiveAtUtc);
        var spanish = LegalDocumentContent.Create(document.Id, "es-AR", "Términos de uso");
        var english = LegalDocumentContent.Create(document.Id, "en-US", "Terms of use");

        Assert.Equal(LegalDocumentKind.Terms, document.Kind);
        Assert.Equal(1, document.Version);
        Assert.Equal(EffectiveAtUtc, document.EffectiveAtUtc);
        Assert.Equal(document.Id, spanish.LegalDocumentId);
        Assert.Equal("es-AR", spanish.Culture);
        Assert.Equal("Términos de uso", spanish.Text);
        Assert.Equal(document.Id, english.LegalDocumentId);
        Assert.Equal("en-US", english.Culture);
        Assert.Equal("Terms of use", english.Text);
        Assert.All(typeof(LegalDocument).GetProperties(), property => Assert.Null(property.GetSetMethod()));
    }

    [Fact]
    public void Acceptance_records_the_exact_document_version_and_instant()
    {
        var document = LegalDocument.Create(LegalDocumentKind.Privacy, 3, EffectiveAtUtc);
        var userId = Guid.CreateVersion7();
        var acceptedAtUtc = EffectiveAtUtc.AddMinutes(2);

        var acceptance = LegalAcceptance.Create(userId, document, acceptedAtUtc, "127.0.0.1", "test-agent");

        Assert.Equal(userId, acceptance.UserId);
        Assert.Equal(document.Id, acceptance.LegalDocumentId);
        Assert.Equal(3, acceptance.Version);
        Assert.Equal(acceptedAtUtc, acceptance.AcceptedAtUtc);
        Assert.Equal("127.0.0.1", acceptance.IpAddress);
        Assert.Equal("test-agent", acceptance.UserAgent);
        Assert.All(typeof(LegalAcceptance).GetProperties(), property => Assert.Null(property.GetSetMethod()));
    }

    [Fact]
    public void Legal_instants_must_be_utc()
    {
        var local = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => LegalDocument.Create(LegalDocumentKind.Terms, 1, local));
    }

    [Fact]
    public void Account_deletion_can_remove_request_metadata_without_erasing_acceptance_proof()
    {
        var document = LegalDocument.Create(LegalDocumentKind.Terms, 1, EffectiveAtUtc);
        var acceptance = LegalAcceptance.Create(Guid.CreateVersion7(), document, EffectiveAtUtc.AddMinutes(1),
            "127.0.0.1", "test-agent");

        acceptance.ClearPersonalMetadata();

        Assert.Null(acceptance.IpAddress);
        Assert.Null(acceptance.UserAgent);
        Assert.Equal(document.Id, acceptance.LegalDocumentId);
        Assert.Equal(1, acceptance.Version);
    }
}
