using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Input interno compuesto por configuración/binding confiables. No es un contrato HTTP.</summary>
public sealed record AssistantFinanceAdmission(Guid OperationId, AssistantFinancialBinding Binding,
    AssistantMode Mode, int DailyLimit, Money? ConfigurationBudget, string OrganizationTimeZoneId,
    AssistantPriceSnapshot Price, AssistantPriceSnapshot? EligibleTitlePrice);
public sealed record AssistantCallAdmission(AssistantFinancialCall Call, bool IsNew);
public sealed record AssistantDailyQuotaRow(int Consumed, int Reserved, int Limit, int? Remaining, DateTime ResetAtUtc);
