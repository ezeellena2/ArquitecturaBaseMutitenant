using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[Route("api/organization/assistant")]
[Tags("Assistant Business")]
[Access(Access.Business)]
[FeatureGate("assistant")]
[HasPermission(Permissions.Assistant.Use)]
public sealed class BusinessAssistantController(IAssistantConversationService service, IAssistantAttachmentService attachments, IAssistantExportService exports) : AssistantControllerBase(service, attachments, exports);
