namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Excluye datos sensibles del diff de auditoría.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
