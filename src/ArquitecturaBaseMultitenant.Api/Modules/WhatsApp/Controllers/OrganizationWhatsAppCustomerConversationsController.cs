using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;

[Route("api/organization/whatsapp/customers")]
[Tags("WhatsApp Business inboxes")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Customers)]
public sealed class OrganizationWhatsAppCustomerConversationsController(IWhatsAppConversationService service) : WhatsAppConversationsControllerBase(service)
{
    protected override WhatsAppInbox? Inbox => WhatsAppInbox.Customers;
}

[Route("api/organization/whatsapp/internal")]
[Tags("WhatsApp Business inboxes")]
[Access(Access.Business)]
[FeatureGate(WhatsAppFeature.Key)]
[HasPermission(Permissions.WhatsApp.Internal)]
public sealed class OrganizationWhatsAppInternalConversationsController(IWhatsAppConversationService service) : WhatsAppConversationsControllerBase(service)
{
    protected override WhatsAppInbox? Inbox => WhatsAppInbox.Internal;
}

[Route("api/platform/whatsapp")]
[Tags("WhatsApp Platform inquiries")]
[Access(Access.Platform)]
[ArquitecturaBaseMultitenant.Api.Features.InstalledModuleRoute(WhatsAppApiModule.ManagementRoute, ArquitecturaBaseMultitenant.Api.Features.InstalledModuleRouteCategory.PlatformManagement)]
[HasPlatformPermission(PlatformPermissions.WhatsApp.Attend)]
public sealed class PlatformWhatsAppConversationsController(IPlatformWhatsAppConversationService service) : WhatsAppConversationsControllerBase(service)
{
    protected override WhatsAppInbox? Inbox => null;
}
