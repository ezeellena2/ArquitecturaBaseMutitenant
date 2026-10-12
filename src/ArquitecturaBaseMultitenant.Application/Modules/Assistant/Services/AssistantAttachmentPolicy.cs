using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal static class AssistantAttachmentPolicy
{
    internal static Result Validate(string? mediaType, ReadOnlySpan<byte> header, long length)
    {
        if (length > 10485760) return AssistantErrors.AttachmentTooLarge;
        var valid = mediaType switch
        {
            "image/png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/jpeg" => header.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            "image/webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" => header.StartsWith("%PDF-"u8),
            _ => false,
        };
        return length > 0 && valid ? Result.Success() : AssistantErrors.AttachmentUnsupportedType;
    }
}
