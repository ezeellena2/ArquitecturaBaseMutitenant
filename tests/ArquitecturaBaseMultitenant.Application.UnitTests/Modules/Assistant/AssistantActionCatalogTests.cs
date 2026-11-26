using System.Globalization;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Stub = ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant.AssistantAccessPolicyTests.Stub;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>
/// ADR 0068 §6 y §7: el registro filtra por acceso, módulo, requisito, calificación y modelo del turno, con una lectura de
/// permisos por llamada; las tres herramientas devuelven datos sin registros ni personas, y lo rechazado vuelve como dato.
/// </summary>
public sealed class AssistantActionCatalogTests
{
    private const string FakeModel = "fake-chat-model";
    private const string Permission = "things.manage";
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid PersonId = Guid.CreateVersion7();
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-AR");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AssistantActionDefinition Invite = new()
    {
        Id = "things.invite",
        Version = 1,
        Kind = AssistantActionKind.Create,
        Requirement = AssistantActionRequirement.Organization(Permission),
        SaveRoute = "POST api/things",
        Confirmation = AssistantActionConfirmation.Dialog,
        Screen = AssistantActionScreen.InviteUser,
        Fields =
        [
            new() { Name = "email", Type = AssistantActionFieldType.Email, Required = true, MaxLength = 254 },
            new() { Name = "roles", Type = AssistantActionFieldType.Choices, MaxItems = 5 },
            new() { Name = "terms", Type = AssistantActionFieldType.Consent },
        ],
    };

    private static readonly AssistantActionDefinition Modular = Invite with { Id = "things.modular", Module = "things" };

    // Lo armado como dato (0068 «Consecuencias»): las opciones de un catálogo, con los campos de su definición.
    private static readonly AssistantActionDefinition Option = Invite with
    {
        Id = "things.option",
        FieldsFromTarget = true,
        Target = null,
        Fields = [],
    };

    [Fact]
    public async Task Without_qualification_no_action_is_offered()
    {
        var catalog = Catalog([], out _);

        Assert.Empty(await catalog.ListAsync(FakeModel, Ct));
        Assert.Empty(await catalog.ListAsync(null, Ct));
        Assert.Empty(catalog.Seal([Invite], FakeModel));
    }

    [Fact]
    public async Task With_the_fake_model_qualification_the_action_is_offered_only_with_that_model()
    {
        var catalog = Catalog([Qualified(Invite)], out _);

        Assert.Equal(new[] { Invite.Key }, (await catalog.ListAsync(FakeModel, Ct)).Select(action => action.Key));
        Assert.Equal(new[] { Invite.Key }, (await catalog.ListAsync(null, Ct)).Select(action => action.Key));
        Assert.Empty(await catalog.ListAsync("another-model", Ct));
        Assert.Equal(new[] { Invite.Key }, catalog.Seal([Invite], FakeModel));
        Assert.Empty(catalog.Seal([Invite], "another-model"));
    }

    [Theory]
    [InlineData("granted", true)]
    [InlineData("missing", false)]
    [InlineData("consumer", false)]
    [InlineData("module-off", false)]
    public async Task Access_requirement_and_module_filter_with_one_permission_read(string situation, bool offered)
    {
        var catalog = Catalog([Qualified(Invite), Qualified(Modular)], out var reads,
            permissions: situation == "missing" ? [] : [Permission],
            access: situation == "consumer" ? Access.Consumer : Access.Business,
            moduleOn: situation != "module-off");

        var keys = (await catalog.ListAsync(FakeModel, Ct)).Select(action => action.Key).ToArray();

        Assert.Equal(offered, keys.Contains(Modular.Key));
        Assert.Equal(situation is "granted" or "module-off", keys.Contains(Invite.Key));
        Assert.True(reads.Count <= 1, "The catalog reads the permissions once per call.");
    }

    [Fact]
    public void An_action_declared_twice_fails_when_the_catalog_is_built() =>
        Assert.Throws<InvalidOperationException>(() => Catalog([], out _, sources: [new FakeSource(Invite), new FakeSource(Invite)]));

    [Fact]
    public async Task An_action_outside_the_seal_is_rejected_as_data()
    {
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _));

        var data = await tools.ProposeAsync(Propose(Invite.Key), new AssistantActionTurn([], FakeModel, false), Ct);

        Assert.Equal((AssistantActionToolData.Rejected, AssistantErrors.ActionNotAvailableCode), (data.Outcome, data.Code));
        Assert.Null(data.ProposalId);
    }

    [Fact]
    public async Task A_second_proposal_in_the_turn_is_rejected_as_data()
    {
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _));

        var data = await tools.ProposeAsync(Propose(Invite.Key), new AssistantActionTurn([Invite.Key], FakeModel, true), Ct);

        Assert.Equal((AssistantActionToolData.Rejected, AssistantErrors.ActionOnePerTurnCode), (data.Outcome, data.Code));
    }

    [Fact]
    public async Task What_goes_to_the_provider_has_no_records_or_people()
    {
        var source = new FakeSource(Invite);
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _, sources: [source]));
        var turn = new AssistantActionTurn([Invite.Key], FakeModel, false);

        var accepted = await tools.ProposeAsync(Propose(Invite.Key), turn, Ct);
        var list = await tools.ListAsync(turn, Spanish, Ct);
        var fields = await tools.ReadAsync(Json(new { action = Invite.Key }), turn, Spanish, Ct);
        var choices = await tools.ReadAsync(Json(new { action = Invite.Key, field = "roles" }), turn, Spanish, Ct);

        Assert.Equal(AssistantActionToolData.Accepted, accepted.Outcome);
        Assert.NotNull(accepted.ProposalId);
        Assert.NotNull(accepted.Resolution);
        Assert.Equal(new[] { "Vendedora" }, choices.Choices!);
        Assert.Equal(new[] { Invite.Key }, list.Actions!.Select(row => row.Action));
        Assert.DoesNotContain(fields.Action!.Fields, field => field.Type == AssistantActionFieldType.Consent);
        foreach (var data in new[] { accepted, list, fields, choices })
        {
            var sent = Serialize(data);
            Assert.DoesNotContain(PersonId.ToString(), sent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(FakeSource.RoleId.ToString(), sent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Ana Pérez", sent, StringComparison.Ordinal);
            Assert.DoesNotContain("ana@example.com", sent, StringComparison.Ordinal);
        }
    }

    /// <summary>ADR 0068 §12: 20 opciones por página, de una constante del núcleo, aunque la fuente traiga más.</summary>
    [Fact]
    public async Task Read_action_gives_at_most_the_choices_of_one_page_even_if_the_source_brings_more()
    {
        var source = new FakeSource(Invite) { ChoiceCount = AssistantActionLimits.ChoicesPerPage + 5 };
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _, sources: [source]));

        var choices = await tools.ReadAsync(Json(new { action = Invite.Key, field = "roles" }), new AssistantActionTurn([Invite.Key], FakeModel, false), Spanish, Ct);

        Assert.Equal(AssistantActionLimits.ChoicesPerPage, choices.Choices!.Count);
        Assert.True(choices.HasMore);
    }

    [Fact]
    public async Task A_rule_of_the_source_comes_back_with_its_path_without_cutting_the_turn()
    {
        var source = new FakeSource(Invite)
        {
            Validation = new ValidationError(new Dictionary<string, string[]> { ["email"] = ["Users.Invitation.AlreadyMember"] }),
        };
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _, sources: [source]));

        var data = await tools.ProposeAsync(Propose(Invite.Key), new AssistantActionTurn([Invite.Key], FakeModel, false), Ct);

        Assert.Equal((AssistantActionToolData.Rejected, AssistantErrors.ActionInvalidCode), (data.Outcome, data.Code));
        Assert.Equal(new[] { new AssistantActionToolError("email", "Users.Invitation.AlreadyMember") }, data.Errors!);
    }

    [Fact]
    public async Task The_schema_rejects_unknown_fields_consents_and_missing_required_ones_with_their_paths()
    {
        var tools = new AssistantActionTools(Catalog([Qualified(Invite)], out _));
        var arguments = Json(new { action = Invite.Key, values = new object[] { new { field = "terms", value = "sí" }, new { field = "tenantId", value = "x" } } });

        var data = await tools.ProposeAsync(arguments, new AssistantActionTurn([Invite.Key], FakeModel, false), Ct);

        Assert.Equal(AssistantErrors.ActionInvalidCode, data.Code);
        Assert.Equal(new[] { "terms", "tenantId", "email" }, data.Errors!.Select(error => error.Path));
    }

    [Fact]
    public async Task Fields_from_the_target_come_with_their_type_and_limits()
    {
        var source = new FakeSource(Option);
        var tools = new AssistantActionTools(Catalog([Qualified(Option)], out _, sources: [source]));
        var turn = new AssistantActionTurn([Option.Key], FakeModel, false);

        var withTarget = await tools.ReadAsync(Json(new { action = Option.Key, target = "Talles" }), turn, Spanish, Ct);
        var withoutTarget = await tools.ReadAsync(Json(new { action = Option.Key }), turn, Spanish, Ct);

        var field = Assert.Single(withTarget.Action!.Fields);
        Assert.Equal(("size", AssistantActionFieldType.Text, 20), (field.Name, field.Type, field.MaxLength));
        Assert.True(withTarget.Action.HasTarget);
        Assert.Equal("Talles", source.TargetRead);
        Assert.Equal(AssistantErrors.ActionNotAvailableCode, withoutTarget.Code);
    }

    [Fact]
    public void Action_tool_data_is_a_closed_projection_without_subjects()
    {
        Assert.Equal(Array.Empty<Guid>(), AssistantTurnToolBuilder.SubjectsOf(new AssistantActionToolData()));
        Assert.Equal(Array.Empty<Guid>(), AssistantTurnToolBuilder.SubjectsOf(AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode)));
        Assert.Null(AssistantTurnToolBuilder.SubjectsOf(new UnknownToolData()));
    }

    [Fact]
    public void The_qualified_actions_file_is_an_empty_list_with_its_shape()
    {
        Assert.Empty(AssistantActionQualifications.Load().Rows);
        var row = Assert.Single(AssistantActionQualifications.Parse(Stream("""
            [{"action":"users.invite","version":1,"providerModelId":"claude-x","qualifiedOn":"2026-12-01","measuredCost":{"amount":0.0123,"currency":"USD"}}]
            """)));
        Assert.Equal(("users.invite@1", "claude-x", 0.0123m), (row.Key, row.ProviderModelId, row.MeasuredCost.Amount));
        Assert.Throws<InvalidOperationException>(() => AssistantActionQualifications.Parse(Stream("""
            [{"action":"users.invite","version":1,"providerModelId":"claude-x","qualifiedOn":"2026-12-01"}]
            """)));
    }

    private sealed record UnknownToolData : AssistantToolData;

    private static AssistantActionQualification Qualified(AssistantActionDefinition action) =>
        new(action.Id, action.Version, FakeModel, new DateOnly(2026, 11, 26), new Money(0.01m, CurrencyCode.Create("USD").Value));

    private static AssistantActionCatalog Catalog(IReadOnlyList<AssistantActionQualification> qualified, out List<Guid> reads,
        IReadOnlyList<IAssistantActionSource>? sources = null, IReadOnlyList<string>? permissions = null,
        Access access = Access.Business, bool moduleOn = true)
    {
        var counted = new List<Guid>();
        reads = counted;
        var granted = new BusinessPermissions(permissions ?? [Permission], new Dictionary<Guid, IReadOnlyList<string>>(), []);
        return new AssistantActionCatalog(sources ?? [new FakeSource(Invite, Modular, Option)], new AssistantActionQualifications(qualified),
            Stub.Of<ICurrentUser>(new()
            {
                ["get_UserId"] = _ => (Guid?)UserId,
                ["get_Access"] = _ => (Access?)access,
            }),
            Stub.Of<IFeatureService>(new() { ["IsEnabledAsync"] = _ => Task.FromResult(moduleOn) }),
            Stub.Of<IPermissionService>(new()
            {
                ["GetBusinessPermissionsAsync"] = arguments =>
                {
                    counted.Add((Guid)arguments[0]!);
                    return Task.FromResult(granted);
                },
            }),
            Stub.Of<IPlatformPermissionService>(new()));
    }

    private static JsonElement Propose(string action) => Json(new
    {
        action,
        values = new object[] { new { field = "email", value = "ana@example.com" }, new { field = "roles", value = new[] { "Vendedora" } } },
    });

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string Serialize(AssistantToolData data) => JsonSerializer.Serialize(data, data.GetType(), AssistantTurnJson.Options);

    /// <summary>Una fuente de prueba: resuelve a ids y nombres de personas, que nunca tienen que llegar al proveedor.</summary>
    private sealed class FakeSource(params AssistantActionDefinition[] actions) : IAssistantActionSource
    {
        internal static readonly Guid RoleId = Guid.CreateVersion7();
        internal Error? Validation { get; init; }
        internal string? TargetRead { get; private set; }
        internal int ChoiceCount { get; init; } = 1;

        public IReadOnlyList<AssistantActionDefinition> Definitions => actions;

        public Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new PagedResult<AssistantActionChoiceRow>(
                [.. Enumerable.Range(0, ChoiceCount).Select(index => new AssistantActionChoiceRow(RoleId.ToString(), index == 0 ? "Vendedora" : $"Vendedora {index}"))],
                1, ChoiceCount, ChoiceCount)));

        public Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new AssistantActionResolution(draft.Action, PersonId, 3,
                draft.Values.Select(value => new AssistantActionResolvedValue(value.Field, value.Value,
                    JsonSerializer.SerializeToElement(new { id = PersonId, name = "Ana Pérez" }), null)).ToArray())));

        public Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target, CancellationToken cancellationToken)
        {
            TargetRead = target;
            return Task.FromResult(Result.Success<IReadOnlyList<AssistantActionField>>(
                [new AssistantActionField { Name = "size", Type = AssistantActionFieldType.Text, MaxLength = 20 }]));
        }

        public Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new AssistantActionReview(null, [])));

        public Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken) =>
            Task.FromResult(Validation is { } error ? Result.Failure(error) : Result.Success());
    }
}
