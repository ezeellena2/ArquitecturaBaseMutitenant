using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorDisplayReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Only_operators_whose_deletion_has_not_begun_come_back_with_their_name_and_primary_email()
    {
        var named = Account("Lucía Operadora", isOperator: true);
        var unnamed = Account(null, isOperator: true);
        var person = Account("Persona Común", isOperator: false);
        var leaving = Account("Operadora Saliente", isOperator: true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ApplicationDbContext>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                context.Users.AddRange(named, unnamed, person, leaving);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            // La baja de «leaving» empezó: el proceso de baja ya tomó la cuenta.
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE identity."AspNetUsers" SET "DeletionLeaseId" = {Guid.CreateVersion7()},
                        "DeletionLeaseExpiresAtUtc" = {NowUtc.AddMinutes(5)} WHERE "Id" = {leaving.Id}
                    """, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        await using var check = factory.Services.CreateAsyncScope();
        var reader = check.ServiceProvider.GetRequiredService<IPlatformOperatorDisplayReader>();
        var rows = (await reader.ListAsync([named.Id, unnamed.Id, person.Id, leaving.Id, Guid.CreateVersion7()], Ct))
            .ToDictionary(row => row.UserId);

        // La persona que no es operadora nunca figura: el puerto no expone su correo.
        Assert.Equal(new[] { named.Id, unnamed.Id }.Order(), rows.Keys.Order());
        Assert.Equal("Lucía Operadora", rows[named.Id].DisplayName);
        Assert.Equal(named.Email, rows[named.Id].PrimaryEmail?.Value);
        Assert.Null(rows[unnamed.Id].DisplayName);
        Assert.Equal(unnamed.Email, rows[unnamed.Id].PrimaryEmail?.Value);
        Assert.Empty(await reader.ListAsync([], Ct));
    }

    private static ApplicationUser Account(string? displayName, bool isOperator)
    {
        var user = ApplicationUser.Create(displayName, "es-AR", "America/Argentina/Buenos_Aires", NowUtc).Value;
        user.Email = $"cuenta.{user.Id:N}@example.test";
        if (isOperator) user.GrantPlatformOperator();
        return user;
    }
}
