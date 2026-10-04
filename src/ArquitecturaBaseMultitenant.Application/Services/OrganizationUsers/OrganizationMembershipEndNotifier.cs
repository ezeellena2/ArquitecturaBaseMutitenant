using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Notifica una salida usando solo métodos de ingreso que la persona conserva disponibles.</summary>
internal sealed class OrganizationMembershipEndNotifier(
    OrganizationMembershipEndNoticeBuilder contexts,
    OrganizationAvailableLoginMethodsResolver loginMethods,
    AccountNoticeIssuer accountNotices)
{
    internal async Task<Result> NotifyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var prepared = await contexts.PrepareAsync(userId, cancellationToken);
        if (prepared.IsFailure) return prepared.Error;
        if (prepared.Value is not { } context) return Result.Success();

        var availableMethods = await loginMethods.ResolveAsync(userId, context.Tenant.Id, cancellationToken);
        await accountNotices.EnqueueAsync(userId, availableMethods,
            new AccountNotice.ReviewLoginMethods(context.Tenant.Name, context.AccountUrl)
            {
                RecipientName = context.Account.DisplayName,
            }, context.Account.Culture, principalOnly: false, cancellationToken);
        return Result.Success();
    }
}
