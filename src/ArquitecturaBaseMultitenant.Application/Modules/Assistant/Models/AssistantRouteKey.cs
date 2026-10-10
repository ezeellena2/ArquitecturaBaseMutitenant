using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantRouteKey
{
    [JsonStringEnumMemberName("business.users")] BusinessUsers,
    [JsonStringEnumMemberName("business.roles")] BusinessRoles,
    [JsonStringEnumMemberName("business.companies")] BusinessCompanies,
    [JsonStringEnumMemberName("business.audit")] BusinessAudit,
    [JsonStringEnumMemberName("business.home")] BusinessHome,
    [JsonStringEnumMemberName("platform.organizations")] PlatformOrganizations,
    [JsonStringEnumMemberName("platform.accounts")] PlatformAccounts,
    [JsonStringEnumMemberName("platform.recoveries")] PlatformRecoveries,
    [JsonStringEnumMemberName("platform.audit")] PlatformAudit,
    [JsonStringEnumMemberName("platform.assistant.usage")] PlatformAssistantUsage,
    [JsonStringEnumMemberName("personal.account")] PersonalAccount,
    [JsonStringEnumMemberName("personal.organizations")] PersonalOrganizations,
}
