using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Messaging;

public sealed class ChannelContractsTests
{
    [Fact]
    public void Account_notices_are_a_closed_catalog_for_all_planned_account_events()
    {
        var notices = typeof(AccountNotice).Assembly.GetTypes()
            .Where(type => type.BaseType == typeof(AccountNotice))
            .ToArray();

        Assert.True(typeof(AccountNotice).IsAbstract);
        Assert.Equal(8, notices.Length);
        Assert.All(notices, type => Assert.True(type.IsSealed));
        Assert.Equal(["AccountDeleted", "DeletionCancelled", "DeletionRequested",
                "LoginMethodChanged", "RecoveryApproved", "RecoveryReceived",
                "RecoveryRejected", "ReviewLoginMethods"],
            notices.Select(type => type.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Channels_use_registered_string_keys_and_outbox_stays_a_port()
    {
        Assert.Equal(typeof(string), typeof(ILoginCodeChannel).GetProperty("Key")?.PropertyType);
        Assert.Equal(typeof(string), typeof(IInvitationChannel).GetProperty("Key")?.PropertyType);
        Assert.Equal(typeof(string), typeof(IAccountNoticeChannel).GetProperty("Key")?.PropertyType);
        Assert.True(typeof(IOutbox).IsInterface);
    }

    [Fact]
    public void Email_message_does_not_print_the_destination_or_the_body()
    {
        var message = new EmailMessage("secret@example.com", "Secret subject", "<b>Code 123456</b>", "Code 123456");

        Assert.DoesNotContain("secret@example.com", message.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("123456", message.ToString(), StringComparison.Ordinal);
    }
}
