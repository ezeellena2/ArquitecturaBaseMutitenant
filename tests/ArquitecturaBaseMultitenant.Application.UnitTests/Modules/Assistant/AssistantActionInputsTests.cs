using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.Validation.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Validation.Roles;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>
/// Las fuentes de acciones corren los validadores del pedido con <c>RequestValidator.ValidateAsync</c>, como la ruta: lo
/// que rechazan llega con el campo JSON del pedido y vuelve al modelo en el campo de la acción, con el código genérico.
/// </summary>
public sealed class AssistantActionInputsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task What_the_role_validator_rejects_returns_on_the_fields_of_the_role_action()
    {
        var failure = await RequestValidator.ValidateAsync(new CreateRoleRequest("", new string('x', 10_000), null, null, ["no.such"]),
            [new CreateRoleRequestValidator()], Ct);

        var returned = AssistantActionInputs.OnActionFields(Assert.IsType<ValidationError>(failure), RoleActionSource.FieldOfProperty);

        Assert.Equal(new[] { RoleActionSource.DescriptionField, RoleActionSource.NameField, RoleActionSource.PermissionsField, RoleActionSource.ScopeField },
            returned.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(returned.Errors.Values, codes => Assert.Equal(new[] { ValidationError.ErrorCode }, codes));
    }

    [Fact]
    public async Task What_the_invitation_validator_rejects_returns_on_the_fields_of_the_user_action()
    {
        var failure = await RequestValidator.ValidateAsync(new InviteOrganizationUserRequest(null, [Guid.Empty], [new(Guid.Empty, [Guid.Empty])]),
            [new InviteOrganizationUserRequestValidator()], Ct);

        var returned = AssistantActionInputs.OnActionFields(Assert.IsType<ValidationError>(failure), OrganizationUserActionSource.FieldOf);

        Assert.Equal(new[] { OrganizationUserActionSource.CompanyRolesField, OrganizationUserActionSource.EmailField, OrganizationUserActionSource.RolesField },
            returned.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(returned.Errors.Values, codes => Assert.Equal(new[] { ValidationError.ErrorCode }, codes));
    }
}
