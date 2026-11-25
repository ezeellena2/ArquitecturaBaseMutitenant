using System.Linq.Expressions;
using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using Microsoft.EntityFrameworkCore.Query;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// El estado de entrega de una invitación pendiente, derivado al leer del mensaje de la cola que la lleva (B6, ADR 0066 §4),
/// como el de un mensaje en <c>WhatsAppConversationReader</c>: no se guarda en la invitación, porque la confirmación de la
/// cola corre sin organización. Es neutral: el correo y WhatsApp usan la misma regla.
/// </summary>
/// <remarks>
/// La regla vive solo acá, como expresión: <see cref="FromOutbox"/> la evalúa en C# para la fila, y <see cref="InSql{T}"/> la
/// pone en lugar de cada llamada a <see cref="FromOutbox"/> de una consulta, así el filtro y el orden de Personas, Miembros y
/// Cuentas siguen lo que muestra la fila.
/// </remarks>
internal static class InvitationDeliveryProjection
{
    /// <summary>
    /// En la cola, <see cref="InvitationDeliveryStatus.Pending"/>; salió, <see cref="InvitationDeliveryStatus.Sent"/>;
    /// «Sin confirmar», <see cref="InvitationDeliveryStatus.Uncertain"/>; no salió (agotó sus intentos, se canceló o venció),
    /// <see cref="InvitationDeliveryStatus.Failed"/>. Sin fila en la cola (sin id o ya purgada), enviada.
    /// </summary>
    private static readonly Expression<Func<OutboxStatus?, InvitationDeliveryStatus>> Rule = status =>
        status == OutboxStatus.Pending ? InvitationDeliveryStatus.Pending
        : status == OutboxStatus.Uncertain ? InvitationDeliveryStatus.Uncertain
        : status == OutboxStatus.Failed || status == OutboxStatus.Cancelled ? InvitationDeliveryStatus.Failed
        : InvitationDeliveryStatus.Sent;

    private static readonly Func<OutboxStatus?, InvitationDeliveryStatus> Compiled = Rule.Compile();

    private static readonly MethodInfo FromOutboxMethod =
        typeof(InvitationDeliveryProjection).GetMethod(nameof(FromOutbox), BindingFlags.Public | BindingFlags.Static)!;

    public static InvitationDeliveryStatus FromOutbox(OutboxStatus? status) => Compiled(status);

    /// <summary>La misma proyección, con la regla en lugar de cada <see cref="FromOutbox"/>, para que EF la traduzca a SQL.</summary>
    internal static Expression<Func<TInput, TResult>> InSql<TInput, TResult>(Expression<Func<TInput, TResult>> projection) =>
        (Expression<Func<TInput, TResult>>)new Inliner().Visit(projection);

    /// <summary>La misma consulta, con la regla en lugar de cada <see cref="FromOutbox"/>, para que EF la traduzca a SQL.</summary>
    internal static IQueryable<T> InSql<T>(IQueryable<T> query) => query.Provider.CreateQuery<T>(new Inliner().Visit(query.Expression));

    private sealed class Inliner : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method == FromOutboxMethod
            ? ReplacingExpressionVisitor.Replace(Rule.Parameters[0], Visit(node.Arguments[0]), Rule.Body)
            : base.VisitMethodCall(node);
    }
}
