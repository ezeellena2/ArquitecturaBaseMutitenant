using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

[JsonDerivedType(typeof(AssistantPeopleQuickDataResponse))]
[JsonDerivedType(typeof(AssistantInvitationsQuickDataResponse))]
[JsonDerivedType(typeof(AssistantAuditQuickDataResponse))]
[JsonDerivedType(typeof(AssistantOrganizationsQuickDataResponse))]
[JsonDerivedType(typeof(AssistantRecoveriesQuickDataResponse))]
[JsonDerivedType(typeof(AssistantSecurityQuickDataResponse))]
[JsonDerivedType(typeof(AssistantUsageQuickDataResponse))]
[JsonDerivedType(typeof(AssistantAccountQuickDataResponse))]
[JsonDerivedType(typeof(AssistantInteractionsQuickDataResponse))]
public abstract record AssistantQuickDataResponse;
