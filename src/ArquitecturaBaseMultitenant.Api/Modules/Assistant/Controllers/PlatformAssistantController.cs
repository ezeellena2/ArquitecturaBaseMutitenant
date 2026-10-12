using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[Route("api/platform/assistant")]
[Tags("Assistant Platform")]
[Access(Access.Platform)]
[InstalledModuleRoute(AssistantApiModule.ChatRoute, InstalledModuleRouteCategory.PlatformChat)]
[HasPlatformPermission(PlatformPermissions.Assistant.Use)]
public sealed class PlatformAssistantController(IAssistantConversationService service, IAssistantAttachmentService attachments, IAssistantExportService exports) : AssistantControllerBase(service, attachments, exports);
