using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>
/// Orden privada <c>delivery</c> del recorrido para una invitación (I3 de la base 2, B-2): deja el último envío de la cola de
/// la invitación pendiente de un correo o un teléfono de prueba en la cola (<c>Pending</c>), «Sin confirmar»
/// (<c>Uncertain</c>) o sin salir (<c>Failed</c>), con un motivo del correo o de WhatsApp. En el entorno aislado el correo
/// sale enseguida y WhatsApp nunca, así que sin la orden esos estados no se ven. Solo sobre el PostgreSQL efímero de
/// Testing, como dejaría la fila el despacho (<c>OutboxRows</c>); nunca es una ruta.
/// </summary>
public static class RecorridoInvitationDelivery
{
    /// <summary>Lo que vuelve a la cola espera un día: no sale de nuevo mientras dura el recorrido.</summary>
    internal static readonly TimeSpan Hold = TimeSpan.FromDays(1);

    /// <summary>Los motivos del correo que dejan un envío sin salir (<see cref="EmailChannelSender"/>).</summary>
    private static readonly string[] EmailFailures =
    [
        EmailChannelSender.NotSentCode, EmailChannelSender.CredentialsRejectedCode, EmailChannelSender.RecipientRejectedCode,
        EmailChannelSender.RejectedCode,
    ];

    /// <summary>
    /// <c>{ kind: "invitation", email | phone, outcome: "Pending" | "Uncertain" | "Failed", code?, tenantId? }</c>, en la
    /// organización de la orden (la del recorrido, que invita en la suya) o, sin ella, en <paramref name="tenantId"/>. El motivo
    /// va solo con <c>Failed</c>: uno de <see cref="EmailFailures"/> por correo (si no, el destinatario rechazado), o uno de
    /// <c>WhatsAppSendFailures</c> por WhatsApp.
    /// </summary>
    public static async Task<object> ExecuteAsync(IServiceProvider services, JsonElement command, Guid tenantId, CancellationToken ct)
    {
        RecorridoCommandArguments.Allow(command, "kind", "email", "phone", "outcome", "code", "tenantId");
        tenantId = RecorridoCommandArguments.OptionalGuid(command, "tenantId") ?? tenantId;
        if (RecorridoCommandArguments.Text(command, "kind") != "invitation")
            throw new InvalidOperationException("Unsupported isolated delivery command.");
        var email = RecorridoCommandArguments.OptionalText(command, "email");
        var phone = RecorridoCommandArguments.OptionalText(command, "phone");
        if ((email is null) == (phone is null)) throw new InvalidOperationException("An invitation goes to an email or a phone.");
        var destination = email is not null
            ? InvitationIdentity.ForEmail(RecorridoCommandArguments.SyntheticEmail(email))
            : InvitationIdentity.ForPhone(RecorridoCommandArguments.SyntheticPhone(phone!));
        var outcome = Outcome(RecorridoCommandArguments.Text(command, "outcome"));
        var code = Code(outcome, destination.IsPhone, RecorridoCommandArguments.OptionalText(command, "code"));

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        RecorridoCommandArguments.RequireTesting(provider);
        var db = provider.GetRequiredService<ApplicationDbContext>();
        Guid? outboxId;
        using (provider.GetRequiredService<ITenantScope>().Enter(tenantId))
            outboxId = await db.Invitations.AsNoTracking()
                .Where(invitation => invitation.Destination == destination && invitation.Status == InvitationStatus.Pending
                    && invitation.OutboxMessageId != null)
                .OrderByDescending(invitation => invitation.IssuedAtUtc)
                .Select(invitation => invitation.OutboxMessageId).FirstOrDefaultAsync(ct);
        if (outboxId is not { } id) throw new InvalidOperationException("The isolated invitation has no queued delivery.");
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (await SetAsync(db, id, outcome, code, nowUtc, ct) != 1)
            throw new InvalidOperationException("The isolated invitation delivery could not be changed.");
        return new { outcome = outcome.ToString() };
    }

    /// <summary>
    /// Deja la fila de la cola como la dejaría el despacho: «Sin confirmar» o sin salir, sin contenido ni reserva, o de nuevo en
    /// la cola con el próximo intento en un día. Lo usa también el de los mensajes del chat.
    /// </summary>
    internal static Task<int> SetAsync(ApplicationDbContext db, Guid outboxId, OutboxStatus status, string? code, DateTime nowUtc,
        CancellationToken ct)
    {
        var row = db.OutboxMessages.Where(message => message.Id == outboxId);
        return status == OutboxStatus.Pending
            ? row.ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, OutboxStatus.Pending)
                .SetProperty(message => message.LastErrorCode, (string?)null)
                .SetProperty(message => message.NextAttemptAtUtc, nowUtc + Hold)
                .SetProperty(message => message.SentAtUtc, (DateTime?)null)
                .SetProperty(message => message.LeaseId, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresAtUtc, (DateTime?)null), ct)
            : row.ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, status)
                .SetProperty(message => message.LastErrorCode, code)
                .SetProperty(message => message.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(message => message.SentAtUtc, (DateTime?)null)
                .SetProperty(message => message.EncryptedPayload, string.Empty)
                .SetProperty(message => message.LeaseId, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresAtUtc, (DateTime?)null), ct);
    }

    private static OutboxStatus Outcome(string outcome) => outcome switch
    {
        "Pending" => OutboxStatus.Pending,
        "Uncertain" => OutboxStatus.Uncertain,
        "Failed" => OutboxStatus.Failed,
        _ => throw new InvalidOperationException("Unsupported isolated delivery outcome."),
    };

    private static string? Code(OutboxStatus outcome, bool whatsApp, string? code)
    {
        if (outcome != OutboxStatus.Failed)
        {
            if (code is not null) throw new InvalidOperationException("Only a failed delivery takes a reason.");
            return outcome == OutboxStatus.Uncertain ? whatsApp ? OutboxMessage.InterruptedCode : EmailChannelSender.UncertainCode : null;
        }
        if (whatsApp)
            return code is { Length: > 14 and <= 60 } && code.StartsWith("WhatsApp.Send.", StringComparison.Ordinal) && code[14..].All(char.IsAsciiLetter)
                ? code : throw new InvalidOperationException("A WhatsApp failure takes one of its send reasons.");
        code ??= EmailChannelSender.RecipientRejectedCode;
        return EmailFailures.Contains(code, StringComparer.Ordinal) ? code : throw new InvalidOperationException("Unsupported email failure.");
    }
}
