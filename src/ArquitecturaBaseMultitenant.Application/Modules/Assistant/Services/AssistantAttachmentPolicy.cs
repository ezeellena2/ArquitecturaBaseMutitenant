using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal static class AssistantAttachmentPolicy
{
    internal static Result Validate(string? mediaType, ReadOnlySpan<byte> header, long length)
    {
        if (length > AssistantTechnicalLimits.MaxAttachmentBytes) return AssistantErrors.AttachmentTooLarge;
        var valid = AssistantTechnicalLimits.AttachmentMediaTypes.Contains(mediaType) && Domain.Common.FileSignaturePolicy.Matches(mediaType, header);
        return length > 0 && valid ? Result.Success() : AssistantErrors.AttachmentUnsupportedType;
    }
}
