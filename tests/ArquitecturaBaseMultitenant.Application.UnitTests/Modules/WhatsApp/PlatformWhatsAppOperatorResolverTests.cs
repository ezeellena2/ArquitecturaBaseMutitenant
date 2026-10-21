using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class PlatformWhatsAppOperatorResolverTests
{
    [Theory]
    [InlineData(null, "gestor@example.test")]
    [InlineData("", "gestor@example.test")]
    [InlineData("   ", "gestor@example.test")]
    [InlineData("Lucía Fernández", "Lucía Fernández")]
    public async Task Operator_options_use_the_primary_email_when_the_name_is_missing(string? name, string expected)
    {
        var id = Guid.NewGuid();
        var reader = new Accounts(new PlatformAccountRow(id, name, Email.Create("gestor@example.test").Value, true,
            PlatformAccountState.Active, false, 0, null, null, null, null));
        var resolver = new PlatformWhatsAppOperatorResolver(reader, new LoginMethodAvailabilityPolicy([], new Google(), null!),
            null!, new FakeTimeProvider());

        var page = await resolver.ListOptionsAsync(new WhatsAppTeamMemberOptionListRequest { Page = 2, PageSize = 10, Search = "gestor" },
            TestContext.Current.CancellationToken);

        var option = Assert.Single(page.Items);
        Assert.Equal(id, option.Id);
        Assert.Equal(expected, option.Name);
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(11, page.TotalCount);
        Assert.Equal(PlatformAccountKind.Operator, reader.Request?.Kind);
        Assert.Equal(PlatformAccountState.Active, reader.Request?.State);
        Assert.Equal("gestor", reader.Request?.Search);
    }

    private sealed class Google : IGoogleAvailability
    {
        public bool IsEnabled => false;
    }

    private sealed class Accounts(PlatformAccountRow account) : IPlatformAccountReader
    {
        public ListPlatformAccountsRequest? Request { get; private set; }
        public Task<PagedResult<PlatformAccountRow>> ListAsync(ListPlatformAccountsRequest request,
            IReadOnlyCollection<LoginMethodType> signInTypes, DateTime nowUtc, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new PagedResult<PlatformAccountRow>([account], request.Page, request.PageSize, 11));
        }

        public Task<PlatformAccountFilterCounts> CountAsync(ListPlatformAccountsRequest request,
            IReadOnlyCollection<LoginMethodType> signInTypes, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();

        public Task<PlatformAccountDetailRow?> FindAsync(Guid accountId, IReadOnlyCollection<LoginMethodType> signInTypes,
            DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
    }
}
