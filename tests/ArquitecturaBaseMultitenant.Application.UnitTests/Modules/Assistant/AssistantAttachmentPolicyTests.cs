using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantAttachmentPolicyTests
{
    [Theory]
    [InlineData("image/png", "89504E470D0A1A0A00000000", true)]
    [InlineData("image/jpeg", "FFD8FF000000000000000000", true)]
    [InlineData("image/webp", "524946460000000057454250", true)]
    [InlineData("application/pdf", "255044462D312E370A000000", true)]
    [InlineData("image/png", "255044462D312E370A000000", false)]
    [InlineData("text/html", "89504E470D0A1A0A00000000", false)]
    public void Verified_signature_must_match_the_claimed_media_type(string type, string bytes, bool supported)
    {
        Assert.Equal(supported, AssistantAttachmentPolicy.Validate(type, Convert.FromHexString(bytes), Convert.FromHexString(bytes).Length).IsSuccess);
    }

    [Fact]
    public void Exact_limit_is_accepted_and_one_byte_more_is_rejected()
    {
        var header = Convert.FromHexString("89504E470D0A1A0A");
        Assert.True(AssistantAttachmentPolicy.Validate("image/png", header, 10485760).IsSuccess);
        Assert.Equal(AssistantErrors.AttachmentTooLargeCode, AssistantAttachmentPolicy.Validate("image/png", header, 10485761).Error.Code);
    }
}
