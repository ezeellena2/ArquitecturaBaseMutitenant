using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Personal;

internal sealed class MyAccountTool(AssistantAccessPolicy policy, IProfileService profiles, IAccountLoginMethodsService accounts,
    ILoginMethodReader methods, LoginMethodAvailabilityPolicy availability) : AssistantTool(policy)
{
    public override string Key => "my_account";
    public override Access? Access => Domain.Users.Access.Consumer;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        if (profile.IsFailure) return profile.Error;
        var account = await accounts.ListAsync(ct);
        if (account.IsFailure) return account.Error;
        var stored = await methods.ListByUserIdAsync(profile.Value.Id, ct);
        var rows = new List<AssistantMaskedLoginMethodResponse>();
        foreach (var method in account.Value.Methods.Take(AssistantTechnicalLimits.QuickRows))
        {
            var source = stored.SingleOrDefault(row => row.Id == method.Id);
            rows.Add(new AssistantMaskedLoginMethodResponse(method.Type.ToString(),
                PersonalDataMask.LoginMethod(method.Type, method.Value ?? string.Empty, method.Type == LoginMethodType.Google ? method.Value : null),
                method.IsVerified, source is not null && await availability.IsAvailableAsync(profile.Value.Id, source, ct)));
        }
        return new AssistantQuickToolData(new AssistantAccountQuickDataResponse(profile.Value.DisplayName, rows, []));
    }
}
