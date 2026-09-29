using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Aplica el filtro de reserva y replay a cada POST que crea o envía.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute() : ServiceFilterAttribute(typeof(IdempotencyFilter));
