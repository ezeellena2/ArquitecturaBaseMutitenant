using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.PublicSite;

/// <summary>Contenido editable de una página pública empresarial y su estado de publicación.</summary>
public sealed class PublicPage : Entity, IPublishedByBusiness, IVersioned
{
    public const int MaximumLogoBytes = 512 * 1024;

    private PublicPage()
    {
        Name = string.Empty;
    }

    public Guid BusinessTenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public byte[]? LogoBytes { get; private set; }
    public string? LogoContentType { get; private set; }
    public Email? ContactEmail { get; private set; }
    public PhoneNumber? ContactPhone { get; private set; }
    public string? Address { get; private set; }
    public string? Website { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? PublishBlockedAtUtc { get; private set; }
    public string? PublishBlockedReason { get; private set; }
    public Guid? PublishBlockedByUserId { get; private set; }
    public uint Version { get; private set; }

    public static PublicPage Create() => new();

    public Result UpdateContent(string? name, string? description, Email? contactEmail,
        PhoneNumber? contactPhone, string? address, string? website)
    {
        if (string.IsNullOrWhiteSpace(name)) return PublicPageErrors.NameRequired;
        if (name.Trim().Length > TextLimits.OrganizationName) return PublicPageErrors.NameTooLong;
        if (TooLong(description, TextLimits.Description)) return PublicPageErrors.DescriptionTooLong;
        if (TooLong(address, 500) || TooLong(website, 2048)) return PublicPageErrors.ContactTooLong;

        Name = name.Trim();
        Description = Normalize(description);
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Address = Normalize(address);
        Website = Normalize(website);
        return Result.Success();
    }

    public Result SetLogo(byte[]? bytes, string? contentType)
    {
        if (bytes is null)
        {
            LogoBytes = null;
            LogoContentType = null;
            return Result.Success();
        }

        if (bytes.Length is 0 or > MaximumLogoBytes || !HasSupportedImageSignature(bytes, contentType))
            return PublicPageErrors.InvalidLogo;

        LogoBytes = bytes.ToArray();
        LogoContentType = contentType;
        return Result.Success();
    }

    public Result Publish(DateTime publishedAtUtc)
    {
        if (publishedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The publication instant must be UTC.", nameof(publishedAtUtc));
        if (PublishBlockedAtUtc is not null) return PublicPageErrors.PublishBlocked;
        if (string.IsNullOrWhiteSpace(Name)) return PublicPageErrors.NameRequired;
        if (!IsPublished) PublishedAtUtc = publishedAtUtc;
        IsPublished = true;
        return Result.Success();
    }

    public Result Unpublish()
    {
        IsPublished = false;
        return Result.Success();
    }

    /// <summary>Registra una sanción de plataforma; el levantamiento pertenece al flujo de plataforma de E5.</summary>
    public Result ApplyModerationBlock(string? reason, Guid platformUserId, DateTime blockedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason)) return PublicPageErrors.BlockReasonRequired;
        if (reason.Trim().Length > TextLimits.Description) return PublicPageErrors.DescriptionTooLong;
        if (platformUserId == Guid.Empty || blockedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Moderation actor and UTC timestamp are required.");

        IsPublished = false;
        PublishBlockedAtUtc = blockedAtUtc;
        PublishBlockedReason = reason.Trim();
        PublishBlockedByUserId = platformUserId;
        return Result.Success();
    }

    public Result AllowPublish()
    {
        PublishBlockedAtUtc = null;
        PublishBlockedReason = null;
        PublishBlockedByUserId = null;
        return Result.Success();
    }

    private static bool TooLong(string? value, int maximum) => value?.Trim().Length > maximum;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasSupportedImageSignature(byte[] bytes, string? contentType) => contentType switch
    {
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false,
    };
}
