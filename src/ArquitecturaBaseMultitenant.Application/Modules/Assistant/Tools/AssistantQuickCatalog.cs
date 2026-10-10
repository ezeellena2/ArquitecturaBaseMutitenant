using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

internal sealed record AssistantQuickDefinition(string Key, Access Access, string ToolKey)
{
    internal string LabelKey => "Quick." + Key;
}

internal static class AssistantQuickCatalog
{
    internal static IReadOnlyList<AssistantQuickDefinition> All { get; } =
    [
        new("active_users", Access.Business, "org_users"),
        new("expired_invitations", Access.Business, "org_invitations"),
        new("owners", Access.Business, "org_role_members"),
        new("recent_changes", Access.Business, "org_audit"),
        new("pending_organizations", Access.Platform, "platform_organizations"),
        new("pending_recoveries", Access.Platform, "platform_recoveries"),
        new("today_security", Access.Platform, "platform_security"),
        new("assistant_usage", Access.Platform, "platform_assistant_usage"),
        new("my_organizations", Access.Consumer, "my_organizations"),
        new("my_signin", Access.Consumer, "my_account"),
        new("my_messages", Access.Consumer, "my_interactions"),
    ];
}
