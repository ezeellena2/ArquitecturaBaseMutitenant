namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantAttachmentSummaryResponse(Guid Id, string FileName, string MediaType, long ByteLength, uint Version);
