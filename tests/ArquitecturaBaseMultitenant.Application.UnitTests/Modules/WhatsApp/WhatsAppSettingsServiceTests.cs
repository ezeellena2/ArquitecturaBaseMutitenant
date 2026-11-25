using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

/// <summary>
/// La configuración de WhatsApp (270 y 272): cada plazo con su mínimo y su máximo, el de la organización hasta el máximo que
/// fija la plataforma, los países sin repetir y habilitados, y el 409 de una versión vieja.
/// </summary>
public sealed class WhatsAppSettingsServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, true)]
    [InlineData(3650, true)]
    [InlineData(0, false)]
    [InlineData(3651, false)]
    public async Task Organization_retention_goes_from_one_to_the_global_maximum(int days, bool valid)
    {
        using var fixture = new ServiceFixture<WhatsAppSettingsService>(new UpdateWhatsAppOrganizationSettingsRequestValidator());
        var (service, repository, unitOfWork) = Organization(fixture, new Reader());

        var result = await service.UpdateAsync(new(days, 0), Ct);

        Assert.Equal(valid, result.IsSuccess);
        if (valid)
        {
            Assert.Equal(days, repository.Organization!.ChatRetentionDays);
            Assert.True(repository.OrganizationAdded);
            return;
        }
        Assert.Equal("chatRetentionDays", Assert.Single(Assert.IsType<ValidationError>(result.Error).Errors).Key);
        Assert.Equal(0, unitOfWork.Transactions);
    }

    [Fact]
    public async Task Organization_retention_cannot_pass_the_maximum_the_platform_sets()
    {
        using var fixture = new ServiceFixture<WhatsAppSettingsService>(new UpdateWhatsAppOrganizationSettingsRequestValidator());
        var reader = new Reader { Platform = new(["AR"], 90, 30, 4) };
        var (service, repository, unitOfWork) = Organization(fixture, reader);

        var over = await service.UpdateAsync(new(31, 0), Ct);
        var top = await service.UpdateAsync(new(30, 0), Ct);

        Assert.Equal("chatRetentionDays", Assert.Single(Assert.IsType<ValidationError>(over.Error).Errors).Key);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.True(top.IsSuccess);
        Assert.Equal(30, repository.Organization!.ChatRetentionDays);
    }

    [Fact]
    public async Task Organization_reads_its_own_retention_trimmed_to_the_platform_maximum()
    {
        using var fixture = new ServiceFixture<WhatsAppSettingsService>(new UpdateWhatsAppOrganizationSettingsRequestValidator());
        var reader = new Reader { Platform = new(["AR"], 90, 30, 4), Organization = new(120, 7) };
        var (service, _, _) = Organization(fixture, reader);

        var trimmed = await service.GetAsync(Ct);
        reader.Organization = null;
        reader.Platform = null;
        var defaults = await service.GetAsync(Ct);

        Assert.Equal((30, 30, 7u), (trimmed.Value.ChatRetentionDays, trimmed.Value.MaxOrganizationChatRetentionDays, trimmed.Value.Version));
        Assert.Equal((WhatsAppSettingsLimits.DefaultChatRetentionDays, WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays, 0u),
            (defaults.Value.ChatRetentionDays, defaults.Value.MaxOrganizationChatRetentionDays, defaults.Value.Version));
    }

    [Fact]
    public async Task Organization_with_an_old_version_is_a_concurrency_conflict()
    {
        using var fixture = new ServiceFixture<WhatsAppSettingsService>(new UpdateWhatsAppOrganizationSettingsRequestValidator());
        var (service, repository, _) = Organization(fixture, new Reader());
        repository.Conflict = true;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateAsync(new(30, 3), Ct));
        Assert.Equal(3u, repository.ExpectedVersion);
        Assert.Null(repository.Organization);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(3650, 3650, true)]
    [InlineData(0, 30, false)]
    [InlineData(3651, 30, false)]
    [InlineData(30, 0, false)]
    [InlineData(30, 3651, false)]
    public async Task Platform_retentions_go_from_one_to_ten_years(int chats, int organizationMaximum, bool valid)
    {
        using var fixture = new ServiceFixture<PlatformWhatsAppSettingsService>(new UpdatePlatformWhatsAppSettingsRequestValidator(new Countries()));
        var (service, repository, unitOfWork) = Platform(fixture, new Reader());

        var result = await service.UpdateAsync(new(["AR"], chats, organizationMaximum, 0), Ct);

        Assert.Equal(valid, result.IsSuccess);
        if (valid)
        {
            Assert.Equal((chats, organizationMaximum), (repository.Platform!.ChatRetentionDays, repository.Platform.MaxOrganizationChatRetentionDays));
            return;
        }
        var field = chats is < 1 or > 3650 ? "chatRetentionDays" : "maxOrganizationChatRetentionDays";
        Assert.Equal(field, Assert.Single(Assert.IsType<ValidationError>(result.Error).Errors).Key);
        Assert.Equal(0, unitOfWork.Transactions);
    }

    [Theory]
    [InlineData("AR,AR")]
    [InlineData("AR,BR")]
    [InlineData("ZZ")]
    [InlineData("ar")]
    [InlineData("")]
    public async Task Platform_countries_are_enabled_and_not_repeated(string list)
    {
        var countries = list.Split(',', StringSplitOptions.RemoveEmptyEntries);
        using var fixture = new ServiceFixture<PlatformWhatsAppSettingsService>(new UpdatePlatformWhatsAppSettingsRequestValidator(new Countries()));
        var (service, repository, unitOfWork) = Platform(fixture, new Reader());

        var result = await service.UpdateAsync(new(countries, 90, 3650, 0), Ct);

        Assert.Equal("allowedCountries", Assert.Single(Assert.IsType<ValidationError>(result.Error).Errors).Key);
        Assert.Equal(0, unitOfWork.Transactions);
        Assert.Null(repository.Platform);
    }

    [Fact]
    public async Task Platform_saves_the_countries_once_ordered_and_reads_the_configured_ones_without_a_row()
    {
        using var fixture = new ServiceFixture<PlatformWhatsAppSettingsService>(new UpdatePlatformWhatsAppSettingsRequestValidator(new Countries()));
        var (service, repository, _) = Platform(fixture, new Reader());

        var before = await service.GetAsync(Ct);
        var saved = await service.UpdateAsync(new(["UY", "AR"], 60, 365, 0), Ct);

        Assert.Equal(["AR"], before.Value.AllowedCountries);
        Assert.Equal(0u, before.Value.Version);
        Assert.True(saved.IsSuccess);
        Assert.True(repository.PlatformAdded);
        Assert.Equal(["AR", "UY"], repository.Platform!.AllowedCountries);
    }

    [Fact]
    public async Task Platform_with_an_old_version_is_a_concurrency_conflict()
    {
        using var fixture = new ServiceFixture<PlatformWhatsAppSettingsService>(new UpdatePlatformWhatsAppSettingsRequestValidator(new Countries()));
        var (service, repository, _) = Platform(fixture, new Reader());
        repository.Conflict = true;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateAsync(new(["AR"], 90, 3650, 5), Ct));
        Assert.Equal(5u, repository.ExpectedVersion);
        Assert.Null(repository.Platform);
    }

    private static (WhatsAppSettingsService, Repository, FakeUnitOfWork) Organization(
        ServiceFixture<WhatsAppSettingsService> fixture, Reader reader)
    {
        var repository = new Repository();
        var unitOfWork = new FakeUnitOfWork();
        return (new WhatsAppSettingsService(reader, repository, new WhatsAppSettingsResolver(reader, new Channel()), unitOfWork,
            fixture.Validator, fixture.TimeProvider, fixture.Logger), repository, unitOfWork);
    }

    private static (PlatformWhatsAppSettingsService, Repository, FakeUnitOfWork) Platform(
        ServiceFixture<PlatformWhatsAppSettingsService> fixture, Reader reader)
    {
        var repository = new Repository();
        var unitOfWork = new FakeUnitOfWork();
        return (new PlatformWhatsAppSettingsService(repository, new WhatsAppSettingsResolver(reader, new Channel()), unitOfWork,
            fixture.Validator, fixture.TimeProvider, fixture.Logger), repository, unitOfWork);
    }

    private sealed class Reader : IWhatsAppSettingsReader
    {
        public WhatsAppPlatformSettingsRow? Platform { get; set; }
        public WhatsAppOrganizationSettingsRow? Organization { get; set; }

        public Task<WhatsAppPlatformSettingsRow?> FindPlatformAsync(CancellationToken ct) => Task.FromResult(Platform);

        public Task<WhatsAppOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct) => Task.FromResult(Organization);
    }

    private sealed class Repository : IWhatsAppSettingsRepository
    {
        public bool Conflict { get; set; }
        public uint? ExpectedVersion { get; private set; }
        public WhatsAppPlatformSettings? Platform { get; private set; }
        public WhatsAppOrganizationSettings? Organization { get; private set; }
        public bool PlatformAdded { get; private set; }
        public bool OrganizationAdded { get; private set; }

        public Task<WhatsAppPlatformSettings?> GetPlatformForUpdateAsync(uint expectedVersion, CancellationToken ct)
        {
            ExpectedVersion = expectedVersion;
            return Conflict ? throw new ConcurrencyConflictException() : Task.FromResult(Platform);
        }

        public Task<WhatsAppOrganizationSettings?> GetOrganizationForUpdateAsync(uint expectedVersion, CancellationToken ct)
        {
            ExpectedVersion = expectedVersion;
            return Conflict ? throw new ConcurrencyConflictException() : Task.FromResult(Organization);
        }

        public void Add(WhatsAppPlatformSettings settings) => (Platform, PlatformAdded) = (settings, true);
        public void Add(WhatsAppOrganizationSettings settings) => (Organization, OrganizationAdded) = (settings, true);
        public void MarkUpdated(WhatsAppPlatformSettings settings) => Platform = settings;
        public void MarkUpdated(WhatsAppOrganizationSettings settings) => Organization = settings;
    }

    /// <summary>Argentina y Uruguay habilitados; Brasil, en el catálogo pero apagado.</summary>
    private sealed class Countries : ICountryCatalog
    {
        private static readonly CountryCatalogEntry[] Rows =
            [Country("AR", true), Country("UY", true), Country("BR", false)];

        public Task<IReadOnlyList<CountryCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CountryCatalogEntry>>(Rows);

        public Task<CountryCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault(row => row.Code == code));

        private static CountryCatalogEntry Country(string code, bool enabled) =>
            new(code, code + "X", "000", null, null, null, enabled, null, []);
    }

    /// <summary>Solo los países de arranque: el respaldo mientras Plataforma no guarda los suyos.</summary>
    private sealed class Channel : IWhatsAppChannelSettings
    {
        public bool IsEnabled => true;
        public string? PhoneNumberId => null;
        public string? ContactPhone => null;
        public IReadOnlyList<string> ConfiguredCountries => ["AR"];
        public int DailyAuthCodeLimit => 100;
        public Result ValidateNewDestination(LoginCodeDestination destination, IReadOnlyList<string> allowedCountries) =>
            Result.Success();
    }
}
