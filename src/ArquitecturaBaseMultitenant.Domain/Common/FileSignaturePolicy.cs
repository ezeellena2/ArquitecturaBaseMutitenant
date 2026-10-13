namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Recognizes trusted file headers; each feature chooses its allowed media types.</summary>
public static class FileSignaturePolicy
{
    public static bool Matches(string? mediaType, ReadOnlySpan<byte> header) => mediaType switch
    {
        FileMediaTypes.Png => header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        FileMediaTypes.Jpeg => header.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        FileMediaTypes.Webp => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        FileMediaTypes.Pdf => header.StartsWith("%PDF-"u8),
        _ => false,
    };
}

public static class FileMediaTypes
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";
    public static IReadOnlyList<string> Images { get; } = Array.AsReadOnly(new[] { Png, Jpeg, Webp });
}
