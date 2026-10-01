using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Comprueba configuración segura de SMTP y entrega a pickup. Exige las credenciales y TLS correspondientes
/// y verifica el correo generado.
/// </summary>
public sealed class EmailDeliveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Smtp_requires_a_password_only_when_the_delivery_is_smtp()
    {
        var smtp = new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            UserName = "sender@example.test",
            FromAddress = "sender@example.test",
            FromName = "Test",
        };

        var required = new SmtpOptionsValidator(Options.Create(new EmailOptions { Delivery = EmailDelivery.Smtp }));
        var pickup = new SmtpOptionsValidator(Options.Create(new EmailOptions { Delivery = EmailDelivery.PickupDirectory }));

        var validation = required.Validate(null, smtp);
        Assert.False(validation.Succeeded);
        Assert.Contains("Email:Smtp:Password", string.Join(" ", validation.Failures ?? []));
        Assert.True(pickup.Validate(null, smtp).Skipped);
    }

    [Theory]
    [InlineData(SecureSocketOptions.None)]
    [InlineData(SecureSocketOptions.Auto)]
    [InlineData(SecureSocketOptions.StartTlsWhenAvailable)]
    public void Smtp_rejects_security_without_mandatory_tls(SecureSocketOptions security)
    {
        var validator = new SmtpOptionsValidator(
            Options.Create(new EmailOptions { Delivery = EmailDelivery.Smtp }));
        var options = new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            UserName = "sender@example.test",
            Password = "test-only-password",
            FromAddress = "sender@example.test",
            FromName = "Test",
            Security = security,
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Email:Smtp:Security", string.Join(" ", result.Failures ?? []));
    }

    [Theory]
    [InlineData(SecureSocketOptions.StartTls)]
    [InlineData(SecureSocketOptions.SslOnConnect)]
    public void Smtp_accepts_mandatory_tls(SecureSocketOptions security)
    {
        var validator = new SmtpOptionsValidator(
            Options.Create(new EmailOptions { Delivery = EmailDelivery.Smtp }));
        var options = new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            UserName = "sender@example.test",
            Password = "test-only-password",
            FromAddress = "sender@example.test",
            FromName = "Test",
            Security = security,
        };

        Assert.True(validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public async Task Pickup_writes_a_readable_eml_with_subject_and_bodies()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mt-email-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var pickup = new PickupDirectoryEmailTransport(
                Options.Create(new EmailOptions { Delivery = EmailDelivery.PickupDirectory, PickupDirectory = "pickup" }),
                Options.Create(new SmtpOptions { FromAddress = "sender@example.test", FromName = "Test" }),
                new StubHostEnvironment(directory),
                NullLogger<PickupDirectoryEmailTransport>.Instance);

            await pickup.SendAsync(new EmailMessage("recipient@example.test", "Your code", "<b>123456</b>",
                "Code 123456"), Ct);

            var path = Assert.Single(Directory.GetFiles(Path.Combine(directory, "pickup"), "*.eml"));
            await using var stream = File.OpenRead(path);
            var message = await MimeMessage.LoadAsync(stream, Ct);
            Assert.Equal("Your code", message.Subject);
            Assert.Equal("recipient@example.test", Assert.Single(message.To.Mailboxes).Address);
            Assert.Contains("123456", message.TextBody, StringComparison.Ordinal);
            Assert.Contains("123456", message.HtmlBody, StringComparison.Ordinal);
        }
        finally
        {
            var pickupDirectory = Path.Combine(directory, "pickup");
            if (Directory.Exists(pickupDirectory))
            {
                foreach (var path in Directory.GetFiles(pickupDirectory, "*.eml"))
                {
                    File.Delete(path);
                }
                Directory.Delete(pickupDirectory);
            }
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task Email_channel_reads_the_outbox_payload_and_uses_the_transport_once()
    {
        var transport = new CapturingEmailTransport();
        var sender = new EmailChannelSender(transport);
        var message = new EmailMessage("recipient@example.test", "Subject", "<b>Body</b>", "Body");

        await sender.SendAsync(JsonSerializer.Serialize(message), Ct);

        Assert.Equal("email", sender.Key);
        Assert.Equal(message, Assert.Single(transport.Messages));
    }

    [Fact]
    public void Registration_uses_pickup_in_tests_without_smtp_secrets()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder().Build();
        var environment = new StubHostEnvironment(Path.GetTempPath());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);

        services.AddEmail(configuration, environment);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<PickupDirectoryEmailTransport>(provider.GetRequiredService<IEmailTransport>());
        Assert.IsType<EmailChannelSender>(Assert.Single(provider.GetServices<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging.IChannelSender>()));
    }

    private sealed class CapturingEmailTransport : IEmailTransport
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
