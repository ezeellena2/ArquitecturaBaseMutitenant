using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

/// <summary>
/// ADR 0066 §2: un envío «Sin confirmar» lo revisa una persona, con quién y cuándo; la primera revisión queda. La baja de una
/// cuenta olvida a quien revisó como olvida a quien escribió.
/// </summary>
public sealed class WhatsAppChatMessageReviewTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 17, 35, 0, DateTimeKind.Utc);

    [Fact]
    public void The_first_review_stays_and_a_second_one_changes_nothing()
    {
        var message = Outbound(author: Guid.NewGuid());
        var ana = Guid.NewGuid();
        Assert.True(message.MarkReviewed(ana, "Ana Gómez", Now));
        Assert.False(message.MarkReviewed(Guid.NewGuid(), "Otra persona", Now.AddMinutes(5)));
        Assert.Equal((ana, "Ana Gómez", Now), (message.ReviewedByUserId!.Value, message.ReviewedByName, message.ReviewedAtUtc!.Value));
    }

    [Fact]
    public void Only_what_went_out_can_be_reviewed()
    {
        var note = PlatformWhatsAppChatMessage.Note(Guid.NewGuid(), Guid.NewGuid(), "Ana", "Nota", Now);
        var inbound = WhatsAppChatMessage.Inbound(Guid.NewGuid(), "wamid.in", "Hola", null, Now);
        Assert.Throws<InvalidOperationException>(() => note.MarkReviewed(Guid.NewGuid(), "Ana", Now));
        Assert.Throws<InvalidOperationException>(() => inbound.MarkReviewed(Guid.NewGuid(), "Ana", Now));
        Assert.Throws<ArgumentException>(() => Outbound(null).MarkReviewed(Guid.NewGuid(), "Ana", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => Outbound(null).MarkReviewed(Guid.Empty, "Ana", Now));
    }

    [Fact]
    public void A_long_reviewer_name_is_cut_to_the_column()
    {
        var message = Outbound(null);
        message.MarkReviewed(Guid.NewGuid(), new string('a', TextLimits.PersonDisplayName + 10), Now);
        Assert.Equal(TextLimits.PersonDisplayName, message.ReviewedByName!.Length);
    }

    [Fact]
    public void Deleting_an_account_forgets_only_what_that_account_wrote_or_reviewed()
    {
        var author = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var message = Outbound(author);
        message.MarkReviewed(reviewer, "Ana Gómez", Now);

        message.ForgetAuthor(reviewer);
        Assert.Equal((author, "Autor"), (message.AuthorUserId!.Value, message.AuthorName));
        Assert.Equal((null, null, Now), (message.ReviewedByUserId, message.ReviewedByName, message.ReviewedAtUtc));

        message.ForgetAuthor(author);
        Assert.Equal((null, null), (message.AuthorUserId, message.AuthorName));
    }

    private static WhatsAppChatMessage Outbound(Guid? author) =>
        WhatsAppChatMessage.Outbound(Guid.NewGuid(), WhatsAppChatMessageKind.Text, author, author is null ? null : "Autor", "Te ayudo",
            Guid.NewGuid(), null, Now);
}
