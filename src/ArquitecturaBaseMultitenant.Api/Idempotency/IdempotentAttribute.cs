namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Marca los POST de creación y envío que el filtro de E2 protegerá con Idempotency-Key.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute;
