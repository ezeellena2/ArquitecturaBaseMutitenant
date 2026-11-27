using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Las fuentes de los catálogos (E12, C2, C12, §4 y §4b) con la base real: los miembros salen del directorio del núcleo y los
/// contactos del canal por el puerto neutral, siempre de la organización del claim, sin correos ni teléfonos; 201 contactos dan
/// <c>exceedsLimit</c> con el total exacto y las etiquetas como detalle; sin WhatsApp, un catálogo de contactos queda
/// <c>ChannelUnavailable</c>; y una lista que espera corta a los 3 s con un error en lugar de quedarse colgada.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task Members_come_only_from_the_organization_without_emails_and_a_foreign_sample_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, tenantA) = await OrganizationAsync(ct);
        var (_, tenantB) = await OrganizationAsync(ct);
        var mark = Guid.NewGuid().ToString("N")[..8];
        var ana = await OrganizationJourney.AddNamedMemberAsync(factory, tenantA, "Ana " + mark, ct);
        var beto = await OrganizationJourney.AddNamedMemberAsync(factory, tenantA, "Beto " + mark, ct);
        var foreign = await OrganizationJourney.AddNamedMemberAsync(factory, tenantB, "Ajena " + mark, ct);

        var preview = Value(await RunAsync(tenantA, service => service.PreviewAsync(new FormCatalogPreviewRequest(FormCatalogSource.Members,
            new FormCatalogFiltersInput(MembersCatalogSource.Version, [], true, null, null), null, null), ct)));
        Assert.Equal(["Ana " + mark, "Beto " + mark], preview.Options.Select(option => option.Text).Where(text => text.EndsWith(mark, StringComparison.Ordinal)));
        Assert.Equal(preview.Options.Count, preview.Total);
        Assert.False(preview.ExceedsLimit);
        Assert.All(preview.Options, option => Assert.DoesNotContain('@', option.Text + option.Detail));

        var samples = Value(await RunAsync(tenantA, service => service.ListSampleMembersAsync(new ListFormCatalogLookupsRequest { PageSize = 100 }, ct)));
        Assert.Contains(samples.Items, row => row.MemberId == ana);
        Assert.Contains(samples.Items, row => row.MemberId == beto);
        Assert.DoesNotContain(samples.Items, row => row.MemberId == foreign);
        Assert.All(samples.Items, row => Assert.DoesNotContain('@', row.Name));

        // La persona de ejemplo de otra organización no existe para esta, aunque el id sea real.
        var active = new FormCatalogFiltersInput(MembersCatalogSource.Version,
            [new FormCatalogConditionInput(MembersCatalogSource.Active, FormCatalogOperators.Is, ["true"])], true, null, null);
        var foreignSample = await RunAsync(tenantA, service => service.PreviewAsync(
            new FormCatalogPreviewRequest(FormCatalogSource.Members, active, foreign, null), ct));
        Assert.Equal(FormCatalogErrors.SampleMemberNotFoundCode, foreignSample.Error.Code);
        var excluded = Value(await RunAsync(tenantA, service => service.PreviewAsync(
            new FormCatalogPreviewRequest(FormCatalogSource.Members, active, ana, null), ct)));
        Assert.DoesNotContain(excluded.Options, option => option.Text == "Ana " + mark);
        Assert.Contains(excluded.Options, option => option.Text == "Beto " + mark);
    }

    [Fact]
    public async Task Contacts_come_from_the_channel_without_numbers_with_the_exact_total_past_200_and_their_tags_as_detail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerA, tenantA) = await OrganizationAsync(ct);
        var (_, tenantB) = await OrganizationAsync(ct);
        await WhatsAppAttentionJourney.EnableAsync(factory, tenantA, ct);
        await WhatsAppAttentionJourney.EnableAsync(factory, tenantB, ct);
        var mark = Guid.NewGuid().ToString("N")[..8];
        List<(string? Name, string[] Tags)> contacts = [.. Enumerable.Range(1, 201).Select(number => ((string?)$"Cliente {number:000}", new[] { "vip", "Mayorista" }))];
        contacts.Add(("Sin etiqueta", ["otro"]));
        // Un contacto sin nombre no se ofrece (C14), aunque tenga la etiqueta.
        contacts.Add((null, ["vip"]));
        var ownContacts = await AddContactsAsync(tenantA, contacts, ct);
        var foreign = Assert.Single(await AddContactsAsync(tenantB, [("Ajeno " + mark, ["vip"])], ct));

        var filters = new FormCatalogFiltersInput(ContactsCatalogSource.Version,
            [new FormCatalogConditionInput(ContactsCatalogSource.Tags, FormCatalogOperators.HasAny, ["VIP"])], true, ContactsCatalogSource.Tags, null);
        var preview = Value(await AsBusinessAsync(ownerA, tenantA, service => service.PreviewAsync(
            new FormCatalogPreviewRequest(FormCatalogSource.Contacts, filters, null, null), ct)));
        Assert.Equal(201, preview.Total);
        Assert.True(preview.ExceedsLimit);
        Assert.Equal(FormCatalogLimits.PreviewOptions, preview.Options.Count);
        Assert.Equal(new FormCatalogPreviewOption("Cliente 001", "Mayorista, vip"), preview.Options[0]);
        Assert.Equal("Cliente 200", preview.Options[^1].Text);
        var shown = string.Join('\n', preview.Options.Select(option => option.Text + " " + option.Detail));
        Assert.DoesNotContain("Ajeno", shown, StringComparison.Ordinal);
        foreach (var (_, phone) in ownContacts.Append(foreign)) Assert.DoesNotContain(phone[^8..], shown, StringComparison.Ordinal);

        var samples = Value(await AsBusinessAsync(ownerA, tenantA, service => service.ListSampleContactsAsync(
            new ListFormCatalogLookupsRequest { PageSize = 100 }, ct)));
        Assert.Equal(202, samples.TotalCount);
        var tags = Value(await AsBusinessAsync(ownerA, tenantA, service => service.ListContactTagsAsync(new ListFormCatalogLookupsRequest(), ct)));
        Assert.Equal([("Mayorista", 201), ("otro", 1), ("vip", 201)], tags.Items.Select(tag => (tag.Name, tag.Contacts)));

        var foreignSample = await AsBusinessAsync(ownerA, tenantA, service => service.PreviewAsync(
            new FormCatalogPreviewRequest(FormCatalogSource.Contacts, filters, null, foreign.Id), ct));
        Assert.Equal(FormCatalogErrors.SampleContactNotFoundCode, foreignSample.Error.Code);
    }

    [Fact]
    public async Task Without_WhatsApp_a_contacts_catalog_is_unusable_with_the_channel_unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, tenantId) = await OrganizationAsync(ct);
        var created = Value(await RunCatalogsAsync(tenantId, service => service.CreateAsync(new CreateFormCatalogRequest("Clientes", null,
            FormCatalogSource.Contacts, null, null, new FormCatalogFiltersInput(ContactsCatalogSource.Version, [], true, null, null), null, null), ct)));

        // Con un contacto guardado, sin WhatsApp prendido no hay contactos ni catálogo que se pueda usar.
        await AddContactsAsync(tenantId, [("Cliente", [])], ct);
        var read = Value(await AsBusinessAsync<IFormCatalogService, Result<FormCatalogResponse>>(owner, tenantId,
            service => service.GetAsync(created.Catalog.Id, ct)));
        Assert.False(read.Usable);
        Assert.Equal(FormCatalogUnusableReason.ChannelUnavailable, read.UnusableReason);
        var preview = Value(await AsBusinessAsync(owner, tenantId, service => service.PreviewAsync(new FormCatalogPreviewRequest(
            FormCatalogSource.Contacts, new FormCatalogFiltersInput(ContactsCatalogSource.Version, [], true, null, null), null, null), ct)));
        Assert.Empty(preview.Options);
        Assert.Equal(0, preview.Total);

        await WhatsAppAttentionJourney.EnableAsync(factory, tenantId, ct);
        var enabled = Value(await AsBusinessAsync<IFormCatalogService, Result<FormCatalogResponse>>(owner, tenantId,
            service => service.GetAsync(created.Catalog.Id, ct)));
        Assert.True(enabled.Usable);
        Assert.Null(enabled.UnusableReason);
    }

    [Fact]
    public async Task A_member_list_that_waits_on_a_lock_fails_after_three_seconds_instead_of_hanging()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, tenantId) = await OrganizationAsync(ct);
        await using var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(ct);
        await using var transaction = await admin.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("""LOCK TABLE tenant."Members" IN ACCESS EXCLUSIVE MODE""", admin, transaction))
            await command.ExecuteNonQueryAsync(ct);
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var started = TimeProvider.System.GetTimestamp();
            // EF envuelve el corte de Npgsql como una falla pasajera; adentro está el timeout del comando.
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                services.GetRequiredService<IMemberDirectoryReader>().ListAsync(new MemberDirectoryQuery(10), ct));
            var elapsed = TimeProvider.System.GetElapsedTime(started);
            Assert.IsType<TimeoutException>(failure.GetBaseException());
            Assert.InRange(elapsed, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15));
        }
        finally
        {
            await transaction.RollbackAsync(ct);
        }
    }

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    /// <summary>Guarda los contactos de la organización, con su nombre y sus etiquetas; devuelve el id y el número de cada uno.</summary>
    private async Task<IReadOnlyList<(Guid Id, string Phone)>> AddContactsAsync(Guid tenantId, IReadOnlyList<(string? Name, string[] Tags)> contacts,
        CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var created = contacts.Select(contact =>
        {
            var customer = WhatsAppCustomer.Create(PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value, nowUtc);
            customer.Rename(contact.Name);
            customer.ReplaceTags(contact.Tags);
            return customer;
        }).ToArray();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var repository = services.GetRequiredService<IWhatsAppCustomerRepository>();
            foreach (var customer in created) repository.Add(customer);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        return [.. created.Select(customer => (customer.Id, customer.WaId))];
    }

    private async Task<(Guid OwnerUserId, Guid TenantId)> OrganizationAsync(CancellationToken ct)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, ct);
        return (owner.UserId, await OrganizationJourney.StageAsync(factory, owner, "Fuentes " + Guid.NewGuid().ToString("N")[..8], ct));
    }

    private async Task<T> RunAsync<T>(Guid tenantId, Func<IFormCatalogSourceService, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await run(services.GetRequiredService<IFormCatalogSourceService>());
    }

    private async Task<T> RunCatalogsAsync<T>(Guid tenantId, Func<IFormCatalogService, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await run(services.GetRequiredService<IFormCatalogService>());
    }

    /// <summary>
    /// Como una persona que entra como empresa: <c>IFeatureService</c> decide si WhatsApp está prendido con el acceso y la clase de
    /// la organización, que un alcance técnico solo no tiene.
    /// </summary>
    private Task<T> AsBusinessAsync<T>(Guid ownerUserId, Guid tenantId, Func<IFormCatalogSourceService, Task<T>> run) =>
        AsBusinessAsync<IFormCatalogSourceService, T>(ownerUserId, tenantId, run);

    private async Task<T> AsBusinessAsync<TService, T>(Guid ownerUserId, Guid tenantId, Func<TService, Task<T>> run)
        where TService : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var actor = services.GetRequiredService<IExecutionActorScope>().Enter(new ExecutionActor(ownerUserId, Access.Business, tenantId,
            "es-AR", "America/Argentina/Buenos_Aires") { TenantKind = TenantKind.Business });
        return await run(services.GetRequiredService<TService>());
    }
}
