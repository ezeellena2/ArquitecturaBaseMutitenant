using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// «Cargar teléfonos de empleados» en Usuarios (ADR 0038 y decisión 11 de la E11): a cada número que se invita le llega la
/// invitación por WhatsApp, con la empresa y los roles elegidos, y se acepta con ese teléfono verificado. Todo o nada: una
/// sola transacción para la lista.
/// </summary>
internal sealed class WhatsAppPhoneImportService(IRequestValidator validator, ICurrentUser currentUser,
    WhatsAppPhoneImportResolver resolver, OrganizationUserAssignmentWriter assignments,
    OrganizationUserInvitationWriter invitations, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<WhatsAppPhoneImportService> logger) : IWhatsAppPhoneImportService
{
    public Task<Result<WhatsAppPhoneImportPreviewResponse>> PreviewAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "WhatsAppPhoneImportPreview", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var rows = await resolver.ResolveAsync(request.Phones!, cancellationToken);
            return Result.Success(new WhatsAppPhoneImportPreviewResponse([.. rows.Select(row => row.Response)],
                Count(rows, WhatsAppPhoneImportOutcome.AlreadyMember), Count(rows, WhatsAppPhoneImportOutcome.Invite),
                Count(rows, WhatsAppPhoneImportOutcome.Invalid)));
        });
    }

    public Task<Result<WhatsAppPhoneImportResponse>> ImportAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "WhatsAppPhoneImport", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
            if (!resolver.CanInvite) return InvitationErrors.ChannelUnavailableForNow();
            var desired = await assignments.ValidateAsync([],
                request.CompanyId is { } companyId ? [new InviteCompanyRolesRequest(companyId, request.RoleIds)] : [], cancellationToken);
            if (desired.IsFailure) return desired.Error;
            var rows = await resolver.ResolveAsync(request.Phones!, cancellationToken);
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    foreach (var row in rows.Where(row => row.Response.Outcome == WhatsAppPhoneImportOutcome.Invite))
                    {
                        var issued = await invitations.InviteByPhoneAsync(row.Phone!, WhatsAppInvitationChannel.ChannelKey,
                            desired.Value, inviterId, ct);
                        if (issued.IsFailure) return Result.Failure<WhatsAppPhoneImportResponse>(
                            issued.Error.Code == InvitationErrors.ChannelUnavailableCode ? InvitationErrors.ChannelUnavailableForNow() : issued.Error);
                    }
                    return new WhatsAppPhoneImportResponse(Count(rows, WhatsAppPhoneImportOutcome.AlreadyMember),
                        Count(rows, WhatsAppPhoneImportOutcome.Invite), Count(rows, WhatsAppPhoneImportOutcome.Invalid));
                }, CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (ForeignKeyViolationException exception) when (exception.ConstraintName == ForeignKeys.RoleAssignmentRole)
            {
                // Otro pedido borró un rol después de controlarlo y antes del commit.
                return RoleErrors.NotFound;
            }
        });
    }

    private static int Count(IReadOnlyList<WhatsAppPhoneImportResolver.Row> rows, WhatsAppPhoneImportOutcome outcome) =>
        rows.Count(row => row.Response.Outcome == outcome);
}
