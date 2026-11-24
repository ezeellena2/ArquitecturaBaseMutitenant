using System.Collections.Concurrent;
using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Revisión adversarial de la B3 (ADR 0063) por las rutas y la transacción reales:
/// <list type="bullet">
/// <item>un código que no sale (cuenta bloqueada por intentos, sin el método o con la invitación de operador vencida)
/// tiene que responder lo mismo que uno que sale mientras los lugares de los códigos están llenos; si no, el tope global
/// dice qué cuentas están bloqueadas, que es lo que <c>EmailQuotaGuard</c> quiere evitar con el tope de la dirección;</item>
/// <item>el aviso del tope en el log va después del commit (AGENTS.md, regla 2): una reserva que se deshace no llegó al
/// tope, y avisa solo el pedido que de verdad lo cruza.</item>
/// </list>
/// </summary>
public sealed class EmailQuotaAdversarialTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Full = 1_000_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_the_code_places_full_a_locked_out_account_answers_the_same_429_as_an_active_one()
    {
        using var activeClient = Client();
        var active = await AccountJourney.RegisterAsync(factory, activeClient, Ct);
        using var lockedClient = Client();
        var locked = await AccountJourney.RegisterAsync(factory, lockedClient, Ct);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            // Los códigos del alta son de hace un momento: se corren, así el pedido no espera por su destino.
            await services.GetRequiredService<ApplicationDbContext>().LoginCodes
                .Where(code => code.Destination == active.Email.Value || code.Destination == locked.Email.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.CreatedAtUtc, code => code.CreatedAtUtc.AddHours(-1)), Ct);
            // La cuenta quedó bloqueada por intentos fallidos: el código se emite y no sale.
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var user = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == locked.UserId, ct);
                user.LockoutEnabled = true;
                user.LockoutEnd = services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        await FillAsync(MessageQuotaKey.Global);
        try
        {
            using (var activeAnswer = await PlatformJourney.PostAsync(Client(), "/api/auth/login-code",
                       new { email = active.Email.Value }, Ct))
                Assert.Equal(HttpStatusCode.TooManyRequests, activeAnswer.StatusCode);

            using var lockedAnswer = await PlatformJourney.PostAsync(Client(), "/api/auth/login-code",
                new { email = locked.Email.Value }, Ct);
            // Si respondiera 202, quien llena los lugares de los códigos sabe qué cuentas están bloqueadas.
            Assert.True(lockedAnswer.StatusCode == HttpStatusCode.TooManyRequests,
                "La cuenta bloqueada respondió " + (int)lockedAnswer.StatusCode + " y la activa 429: "
                + await lockedAnswer.Content.ReadAsStringAsync(Ct));
        }
        finally
        {
            await ClearAsync(MessageQuotaKey.Global);
        }
    }

    [Fact]
    public async Task A_reservation_that_rolls_back_does_not_warn_that_the_limit_was_reached_and_one_that_commits_does()
    {
        var logs = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Email:Quotas:DailyLimit", "5");
            builder.UseSetting("Email:Quotas:DailyInvitationReserve", "0");
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        await host.Services.SeedDatabaseAsync(Ct);
        await FillAsync(MessageQuotaKey.Global, 4);
        try
        {
            // Una invitación toma el último lugar y su caso falla después (por ejemplo, el 403 de la 0061): se deshace.
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var guard = services.GetRequiredService<EmailQuotaGuard>();
                var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    Assert.True((await guard.ReserveInvitationAsync(Guid.NewGuid(), Guid.NewGuid(), ct)).IsSuccess);
                    return Result.Failure(MemberErrors.InvalidTransition);
                }, CommitPolicy.OnSuccess, Ct);
                Assert.True(result.IsFailure);
            }
            Assert.Equal(4, await CountAsync(MessageQuotaKey.Global));

            // Nada llegó al tope: no hay aviso de que llegó.
            Assert.DoesNotContain(logs.Entries, IsQuotaWarning);

            // La que toma el último lugar y se guarda avisa, recién con el commit.
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var guard = services.GetRequiredService<EmailQuotaGuard>();
                var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    var reserved = await guard.ReserveInvitationAsync(Guid.NewGuid(), Guid.NewGuid(), ct);
                    Assert.DoesNotContain(logs.Entries, IsQuotaWarning);
                    return reserved;
                }, CommitPolicy.OnSuccess, Ct);
                Assert.True(result.IsSuccess);
            }
            Assert.Equal(5, await CountAsync(MessageQuotaKey.Global));
            Assert.Single(logs.Entries, IsQuotaWarning);
        }
        finally
        {
            await ClearAsync(MessageQuotaKey.Global);
        }
    }

    private static bool IsQuotaWarning((string Category, LogLevel Level) entry) =>
        entry.Category == typeof(EmailQuotaGuard).FullName && entry.Level == LogLevel.Warning;

    // El contador en la hora actual: libera lugar recién cuando esa hora sale de la ventana.
    private async Task FillAsync(MessageQuotaKey key, int count = Full)
    {
        var hour = MessageQuotaWindow.HourOf(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        await using var scope = factory.Services.CreateAsyncScope();
        var scopeName = key.Scope.ToString();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync($"""
            INSERT INTO identity."MessageQuotaBuckets" ("Scope", "KeyHash", "HourUtc", "Count")
            VALUES ({scopeName}, {key.KeyHash}, {hour}, {count})
            ON CONFLICT ("Scope", "KeyHash", "HourUtc") DO UPDATE SET "Count" = {count}
            """, Ct);
    }

    private async Task ClearAsync(MessageQuotaKey key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var scopeName = key.Scope.ToString();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(
            $"""DELETE FROM identity."MessageQuotaBuckets" WHERE "Scope" = {scopeName} AND "KeyHash" = {key.KeyHash}""", Ct);
    }

    private async Task<int> CountAsync(MessageQuotaKey key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MessageQuotaBuckets.AsNoTracking()
            .Where(bucket => bucket.Scope == key.Scope && bucket.KeyHash == key.KeyHash).SumAsync(bucket => bucket.Count, Ct);
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue((category, logLevel));
        }
    }
}
