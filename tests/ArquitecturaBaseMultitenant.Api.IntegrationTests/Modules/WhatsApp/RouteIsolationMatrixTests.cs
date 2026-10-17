using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La parte de WhatsApp en la matriz (E9): el número, la plantilla, el contacto, la difusión, el equipo y los chats de
/// otra organización responden 404, también desde las rutas de Plataforma; el chat vinculado de otra persona, también.
/// </summary>
public sealed partial class RouteIsolationMatrixTests
{
    static partial void AddWhatsAppCases(Dictionary<string, RouteCase> cases)
    {
        const string root = "/api/organization/whatsapp";
        cases[$"GET {root}/numbers/{{id:guid}}"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}"));
        cases[$"PUT {root}/numbers/{{id:guid}}"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}",
            new { companyId = (Guid?)null, version = 1 }));
        cases[$"DELETE {root}/numbers/{{id:guid}}"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}?version=1"));
        cases[$"POST {root}/numbers/{{id:guid}}/check"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}/check", new { version = 1 }));
        cases[$"GET {root}/numbers/{{id:guid}}/bot"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}/bot"));
        cases[$"PUT {root}/numbers/{{id:guid}}/bot"] = Foreign(w => Business($"{root}/numbers/{w.B("whatsapp.number")}/bot", new
        {
            welcome = "Bienvenidos", outOfHours = "Estamos cerrados",
            schedule = new
            {
                timeZoneId = "America/Argentina/Buenos_Aires",
                days = Enum.GetValues<DayOfWeek>().Select(day => new { day = day.ToString(), from = "00:00:00", to = "23:59:59" }).ToArray(),
            },
            menu = Array.Empty<object>(), askPromotions = false, offerAccountLink = false, employeesTeamId = (Guid?)null, version = 1,
        }));
        cases[$"GET {root}/contacts/{{id:guid}}"] = Foreign(w => Business($"{root}/contacts/{w.B("whatsapp.contact")}"));
        cases[$"PUT {root}/contacts/{{id:guid}}"] = Foreign(w => Business($"{root}/contacts/{w.B("whatsapp.contact")}",
            new { name = "Matriz", tags = Array.Empty<string>(), consent = nameof(WhatsAppConsent.NoAnswer), version = 1 }));
        cases[$"GET {root}/contacts/{{id:guid}}/conversations"] = Foreign(w => Business($"{root}/contacts/{w.B("whatsapp.contact")}/conversations"));
        cases[$"GET {root}/contacts/{{id:guid}}/broadcasts"] = Foreign(w => Business($"{root}/contacts/{w.B("whatsapp.contact")}/broadcasts"));
        cases[$"GET {root}/templates/{{id:guid}}"] = Foreign(w => Business($"{root}/templates/{w.B("whatsapp.template")}"));
        cases[$"DELETE {root}/templates/{{id:guid}}"] = Foreign(w => Business($"{root}/templates/{w.B("whatsapp.template")}?version=1"));
        cases[$"GET {root}/broadcasts/{{id:guid}}"] = Foreign(w => Business($"{root}/broadcasts/{w.B("whatsapp.broadcast")}"));
        cases[$"POST {root}/broadcasts/{{id:guid}}/cancel"] = Foreign(w => Business($"{root}/broadcasts/{w.B("whatsapp.broadcast")}/cancel", new { version = 1 }));
        AddTeamCases(cases, root, Caller.Business);
        AddTeamCases(cases, "/api/platform/whatsapp", Caller.Platform);
        AddConversationCases(cases, $"{root}/customers", Caller.Business, "whatsapp.customer-chat", "member");
        AddConversationCases(cases, $"{root}/internal", Caller.Business, "whatsapp.internal-chat", "member");
        AddConversationCases(cases, "/api/platform/whatsapp", Caller.Platform, "whatsapp.customer-chat", "operator");
        cases["GET /api/platform/whatsapp/templates/{id:guid}"] = Foreign(w => Platform($"/api/platform/whatsapp/templates/{w.B("whatsapp.template")}"));
        cases["DELETE /api/platform/whatsapp/templates/{id:guid}"] = Foreign(w => Platform($"/api/platform/whatsapp/templates/{w.B("whatsapp.template")}?version=1"));
        cases["GET /api/me/whatsapp/conversations/{id:guid}"] = Foreign(w => Consumer($"/api/me/whatsapp/conversations/{w.B("whatsapp.link")}"));
        cases["DELETE /api/me/whatsapp/conversations/{id:guid}/link"] = Foreign(w => Consumer($"/api/me/whatsapp/conversations/{w.B("whatsapp.link")}/link"));
    }

    private static void AddTeamCases(Dictionary<string, RouteCase> cases, string root, Caller caller)
    {
        cases[$"GET {root}/teams/{{id:guid}}"] = Foreign(w => new Probe(caller, $"{root}/teams/{w.B("whatsapp.team")}"));
        cases[$"PUT {root}/teams/{{id:guid}}"] = Foreign(w => new Probe(caller, $"{root}/teams/{w.B("whatsapp.team")}",
            new { name = "Matriz", memberIds = Array.Empty<Guid>(), notifyAfterMinutes = (int?)null, version = 1 }));
        cases[$"DELETE {root}/teams/{{id:guid}}"] = Foreign(w => new Probe(caller, $"{root}/teams/{w.B("whatsapp.team")}?version=1"));
    }

    /// <summary>
    /// Las rutas de un chat: las de Plataforma prueban con el chat de una organización, que nunca ve. El traspaso va a una
    /// persona propia: un miembro en la organización, un operador en Plataforma.
    /// </summary>
    private static void AddConversationCases(Dictionary<string, RouteCase> cases, string root, Caller caller, string kind, string person)
    {
        string Chat(World w) => $"{root}/conversations/{w.B(kind)}";
        cases[$"GET {root}/conversations/{{id:guid}}"] = Foreign(w => new Probe(caller, Chat(w)));
        cases[$"POST {root}/conversations/{{id:guid}}/take"] = Foreign(w => new Probe(caller, Chat(w) + "/take"));
        cases[$"POST {root}/conversations/{{id:guid}}/messages"] = Foreign(w => new Probe(caller, Chat(w) + "/messages", new { text = "Hola" }));
        cases[$"POST {root}/conversations/{{id:guid}}/notes"] = Foreign(w => new Probe(caller, Chat(w) + "/notes", new { text = "Nota" }));
        cases[$"POST {root}/conversations/{{id:guid}}/transfer"] = Foreign(w => new Probe(caller, Chat(w) + "/transfer",
            new { teamId = (Guid?)null, memberId = w.A(person), version = 1 }));
        cases[$"POST {root}/conversations/{{id:guid}}/templates"] = Foreign(w => new Probe(caller, Chat(w) + "/templates",
            new { templateId = Guid.CreateVersion7(), variables = Array.Empty<string>() }));
        cases[$"GET {root}/conversations/{{id:guid}}/template-options"] = Foreign(w => new Probe(caller, Chat(w) + "/template-options"));
        cases[$"GET {root}/conversations/{{id:guid}}/transfer-options/people"] = Foreign(w => new Probe(caller, Chat(w) + "/transfer-options/people"));
        cases[$"GET {root}/conversations/{{id:guid}}/transfer-options/teams"] = Foreign(w => new Probe(caller, Chat(w) + "/transfer-options/teams"));
    }

    static partial void AddWhatsAppSeed(List<Func<World, Task>> seeds) => seeds.Add(SeedWhatsAppAsync);

    /// <summary>
    /// Los datos de WhatsApp de B, sembrados directo: un número conectado (con su evento de seguridad), una plantilla, un
    /// contacto con su chat, un chat interno, un equipo, una difusión programada lejos y el vínculo del chat con la persona.
    /// </summary>
    private static async Task SeedWhatsAppAsync(World world)
    {
        var factory = world.Factory;
        await WhatsAppAttentionJourney.EnableAsync(factory, world.OrganizationA, Ct);
        await WhatsAppAttentionJourney.EnableAsync(factory, world.OrganizationB, Ct);
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var number = PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value;
        var channel = WhatsAppChannel.Connect(world.OrganizationB, null, WhatsAppAttentionJourney.NewNumberId(),
            "waba" + Guid.NewGuid().ToString("N")[..12], number, world.OrganizationMark, false, false,
            WhatsAppChannelSource.Signup, "America/Argentina/Buenos_Aires", nowUtc);
        await SaveAsync(world, null, (services, db) =>
        {
            db.Add(channel);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System,
                Guid.Empty, world.OrganizationB, "Matriz de aislamiento", nowUtc));
        });

        var customerPhone = PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value;
        var template = WhatsAppTemplate.Create(world.OrganizationMark, "es-AR", WhatsAppTemplateCategory.Utility, "Hola", []);
        var customer = WhatsAppCustomer.Create(customerPhone, nowUtc);
        customer.Rename(world.OrganizationMark);
        var team = WhatsAppTeam.Create(world.OrganizationMark, WhatsAppInbox.Customers, null, []);
        var customerChat = WhatsAppChat.ForCustomer(channel.Id, customerPhone, customer.Id, nowUtc);
        var internalChat = WhatsAppChat.ForMember(channel.Id, PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value,
            world.OwnerB.UserId, nowUtc);
        var broadcast = WhatsAppBroadcast.Create(world.OrganizationMark, template.Id, template.Name, [],
            WhatsAppAudienceKind.AllAccepting, null, nowUtc.AddDays(30), 1, new Money(0m, CurrencyCode.Create("USD").Value));
        var link = WhatsAppConversationLink.Create(world.PersonalB, customerChat.Id, world.OwnerB.UserId, world.PersonMark,
            "Organización " + world.OrganizationMark, number.Value, nowUtc);
        await SaveAsync(world, world.OrganizationB, (_, db) => db.AddRange(template, customer, team, customerChat, internalChat, broadcast));
        // El vínculo es compartido: se guarda desde la organización, con el acceso clasificado.
        await SaveAsync(world, world.OrganizationB, (_, db) => db.Add(link), TenantKind.Business);

        world.Foreign["whatsapp.number"] = channel.Id;
        world.Foreign["whatsapp.template"] = template.Id;
        world.Foreign["whatsapp.contact"] = customer.Id;
        world.Foreign["whatsapp.team"] = team.Id;
        world.Foreign["whatsapp.customer-chat"] = customerChat.Id;
        world.Foreign["whatsapp.internal-chat"] = internalChat.Id;
        world.Foreign["whatsapp.broadcast"] = broadcast.Id;
        world.Foreign["whatsapp.link"] = link.Id;
        world.OpenDoors.AddRange([(Caller.Business, "/api/organization/whatsapp/numbers"), (Caller.Consumer, "/api/me/whatsapp/conversations")]);
        world.SeededTables.AddRange(["identity.WhatsAppChannels", "tenant.WhatsAppChats", "tenant.WhatsAppCustomers",
            "tenant.WhatsAppBroadcasts", "engagement.WhatsAppConversationLinks"]);
    }

    private static async Task SaveAsync(World world, Guid? tenantId, Action<IServiceProvider, ApplicationDbContext> add,
        TenantKind? kind = null)
    {
        using var scope = world.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        if (kind is { } classified && tenantId is { } accessed)
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(accessed, classified);
        using var tenant = kind is null && tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            add(services, services.GetRequiredService<ApplicationDbContext>());
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }
}
