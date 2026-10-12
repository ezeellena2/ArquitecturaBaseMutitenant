using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Controllers;

[Route("api/me/assistant")]
[Tags("Assistant Consumer")]
[Access(Access.Consumer)]
[FeatureGate("assistant")]
public sealed class ConsumerAssistantController(IAssistantConversationService service, IAssistantAttachmentService attachments, IAssistantExportService exports) : AssistantControllerBase(service, attachments, exports);
