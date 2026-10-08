using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

public sealed class WhatsAppMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Delivery_callbacks_and_outbound_messages_cannot_be_claimed_as_inbound()
    {
        var message = WhatsAppMessage.Create("number", "wamid", "delivered", WhatsAppMessageDirection.Outbound,
            WhatsAppMessageKind.Delivery, WhatsAppMessageStatus.Delivered, Now, Now);
        Assert.False(message.TryLease(Guid.NewGuid(), Now, TimeSpan.FromMinutes(1)));
    }
    [Fact]
    public void Authentication_never_retains_content() =>
        Assert.Throws<ArgumentException>(() => WhatsAppMessage.Create("number", "wamid", "sent",
            WhatsAppMessageDirection.Outbound, WhatsAppMessageKind.Authentication, WhatsAppMessageStatus.Sent,
            Now, Now, encryptedText: "ciphertext"));
    [Fact]
    public void Expired_lease_can_be_reclaimed_but_old_worker_cannot_complete()
    {
        var message = WhatsAppMessage.Create("number", "wamid", "inbound", WhatsAppMessageDirection.Inbound,
            WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, Now, Now, encryptedText: "ciphertext");
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.True(message.TryLease(first, Now, TimeSpan.FromMinutes(1)));
        Assert.False(message.TryLease(second, Now, TimeSpan.FromMinutes(1)));
        Assert.True(message.TryLease(second, Now.AddMinutes(2), TimeSpan.FromMinutes(1)));
        Assert.False(message.Complete(first, Now.AddMinutes(2)));
        Assert.True(message.Complete(second, Now.AddMinutes(2)));
        Assert.False(message.TryLease(first, Now.AddMinutes(4), TimeSpan.FromMinutes(1)));
        Assert.Equal(2, message.Attempts);
        message.ClearText(); Assert.Null(message.EncryptedText);
    }
}
