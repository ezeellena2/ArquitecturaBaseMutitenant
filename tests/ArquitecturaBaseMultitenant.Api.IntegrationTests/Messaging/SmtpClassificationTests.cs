using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// N5 y N8: el transporte SMTP clasifica cada intento según dónde quedó la conversación, contra un servidor de guion. Antes de
/// que el servidor acepte al destinatario no salió nada y vuelve a pendiente; un 5xx al destinatario falla; después, una
/// respuesta que lo rechaza dice que no lo tomó y un corte o la espera vencida lo dejan «Sin confirmar». Un 535 deja el chequeo
/// <c>email</c> en Degraded hasta el próximo envío.
/// </summary>
public sealed class SmtpClassificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("421 4.7.0 Try again later", "235 2.7.0 Accepted", "250 2.1.0 OK", "250 2.1.5 OK", "250 2.0.0 OK", "NotSent", false)]
    [InlineData("220 localhost ESMTP", "535 5.7.8 Username and Password not accepted", "250 2.1.0 OK", "250 2.1.5 OK", "250 2.0.0 OK", "CredentialsRejected", false)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "550 5.4.5 Daily user sending limit exceeded", "250 2.1.5 OK", "250 2.0.0 OK", "NotSent", false)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "450 4.2.1 Mailbox busy", "250 2.0.0 OK", "NotSent", false)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "550 5.1.1 No such user", "250 2.0.0 OK", "RecipientRejected", false)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "250 2.1.5 OK", "451 4.3.0 Try again later", "NotSent", true)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "250 2.1.5 OK", "554 5.7.1 Message rejected", "Rejected", true)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "250 2.1.5 OK", null, "Uncertain", true)]
    [InlineData("220 localhost ESMTP", "235 2.7.0 Accepted", "250 2.1.0 OK", "250 2.1.5 OK", "250 2.0.0 OK queued", "Sent", true)]
    public async Task Each_smtp_answer_has_its_class(
        string greeting, string auth, string sender, string recipient, string? afterBody, string expected, bool body)
    {
        await using var server = new ScriptedSmtpServer(greeting, auth, recipient, afterBody, sender);
        using var transport = Transport(server.Port, new EmailHealth());

        Assert.Equal(Enum.Parse<EmailSendOutcome>(expected), await transport.SendAsync(Message(), Ct));
        // Lo que no llegó a aceptar al destinatario nunca mandó el cuerpo.
        Assert.Equal(body, server.ReceivedBody);
    }

    [Fact]
    public async Task The_wait_running_out_before_the_recipient_goes_back_to_pending()
    {
        await using var server = new ScriptedSmtpServer(greeting: null);
        using var transport = Transport(server.Port, new EmailHealth());
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        wait.CancelAfter(TimeSpan.FromSeconds(1));

        Assert.Equal(EmailSendOutcome.NotSent, await transport.SendAsync(Message(), wait.Token));
    }

    [Fact]
    public async Task The_wait_running_out_after_the_recipient_is_uncertain()
    {
        await using var server = new ScriptedSmtpServer(holdData: true);
        using var transport = Transport(server.Port, new EmailHealth());
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        wait.CancelAfter(TimeSpan.FromSeconds(1));

        Assert.Equal(EmailSendOutcome.Uncertain, await transport.SendAsync(Message(), wait.Token));
    }

    [Fact]
    public async Task A_rejected_password_degrades_the_check_until_the_next_send()
    {
        var health = new EmailHealth();
        var check = new EmailHealthCheck(health);
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new HealthCheckContext(), Ct)).Status);

        await using (var rejecting = new ScriptedSmtpServer(auth: "535 5.7.8 Username and Password not accepted"))
        using (var transport = Transport(rejecting.Port, health))
            Assert.Equal(EmailSendOutcome.CredentialsRejected, await transport.SendAsync(Message(), Ct));
        Assert.Equal(HealthStatus.Degraded, (await check.CheckHealthAsync(new HealthCheckContext(), Ct)).Status);

        // Un rechazo de un destinatario no dice nada de las credenciales.
        await using (var recipient = new ScriptedSmtpServer(recipient: "550 5.1.1 No such user"))
        using (var transport = Transport(recipient.Port, health))
            Assert.Equal(EmailSendOutcome.RecipientRejected, await transport.SendAsync(Message(), Ct));
        Assert.Equal(HealthStatus.Degraded, (await check.CheckHealthAsync(new HealthCheckContext(), Ct)).Status);

        await using (var accepting = new ScriptedSmtpServer())
        using (var transport = Transport(accepting.Port, health))
            Assert.Equal(EmailSendOutcome.Sent, await transport.SendAsync(Message(), Ct));
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new HealthCheckContext(), Ct)).Status);
    }

    [Fact]
    public async Task The_email_check_is_part_of_the_readiness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestingEnvironment(Path.GetTempPath());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddEmail(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == EmailHealthCheck.Name, Ct);

        Assert.Equal(HealthStatus.Healthy, Assert.Single(report.Entries).Value.Status);
        provider.GetRequiredService<EmailHealth>().ReportCredentialsRejected();
        report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(Ct);
        Assert.Equal(HealthStatus.Degraded, report.Entries[EmailHealthCheck.Name].Status);
    }

    [Theory]
    [InlineData("Sent", OutboxAttemptOutcome.Accepted, null)]
    [InlineData("NotSent", OutboxAttemptOutcome.Retry, EmailChannelSender.NotSentCode)]
    [InlineData("CredentialsRejected", OutboxAttemptOutcome.Retry, EmailChannelSender.CredentialsRejectedCode)]
    [InlineData("RecipientRejected", OutboxAttemptOutcome.Rejected, EmailChannelSender.RecipientRejectedCode)]
    [InlineData("Rejected", OutboxAttemptOutcome.Rejected, EmailChannelSender.RejectedCode)]
    [InlineData("Uncertain", OutboxAttemptOutcome.Uncertain, EmailChannelSender.UncertainCode)]
    public async Task The_email_channel_tells_the_queue_what_happened(string outcome, OutboxAttemptOutcome expected, string? code)
    {
        var sender = new EmailChannelSender(new FixedTransport(Enum.Parse<EmailSendOutcome>(outcome)));

        var receipt = await sender.SendWithReceiptAsync(JsonSerializer.Serialize(Message()), Ct);

        Assert.Equal((expected, code), (receipt.Outcome, receipt.SafeCode));
    }

    [Fact]
    public async Task An_unreadable_payload_fails_without_calling_the_server()
    {
        var transport = new FixedTransport(EmailSendOutcome.Sent);
        var receipt = await new EmailChannelSender(transport).SendWithReceiptAsync("not json", Ct);
        Assert.Equal((OutboxAttemptOutcome.Rejected, EmailChannelSender.RejectedCode), (receipt.Outcome, receipt.SafeCode));
        Assert.Equal(0, transport.Calls);
    }

    private static EmailMessage Message() => new("recipient@example.test", "Subject", "<b>Body</b>", "Body");

    private static SmtpEmailTransport Transport(int port, EmailHealth health) => new(Options.Create(new SmtpOptions
    {
        Host = "127.0.0.1",
        Port = port,
        Security = SecureSocketOptions.None,
        UserName = "sender@example.test",
        Password = "test-password",
        FromAddress = "sender@example.test",
        FromName = "Test",
    }), health, NullLogger<SmtpEmailTransport>.Instance);

    private sealed class TestingEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FixedTransport(EmailSendOutcome outcome) : IEmailTransport
    {
        public int Calls { get; private set; }

        public Task<EmailSendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(outcome);
        }
    }
}
