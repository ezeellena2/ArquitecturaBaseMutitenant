using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Validation.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.PublicSite;

/// <summary>
/// Cada regla del pedido de Página pública muestra su propio texto. Protege que el slug y el logo no reciclen el
/// mensaje del paginado y que el nombre use la regla común de nombres de organización.
/// </summary>
public sealed class PublicPageValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_rule_reports_its_own_message()
    {
        var validator = new UpdatePublicPageRequestValidator();

        var invalid = await validator.ValidateAsync(Request() with
        {
            Slug = "grupo delta",
            Name = " ",
            Website = "ftp://delta.example.test",
            LogoBase64 = "no es base64",
            LogoContentType = "image/gif",
        }, Ct);
        var errors = invalid.Errors.ToLookup(error => RequestValidator.ToFieldName(error.PropertyName),
            error => error.ErrorMessage);

        Assert.Equal([ValidationTexts.SlugInvalid], errors["slug"]);
        Assert.Equal([ValidationTexts.Required], errors["name"]);
        Assert.Equal([ValidationTexts.WebsiteInvalid], errors["website"]);
        Assert.Equal([ValidationTexts.LogoInvalid], errors["logoBase64"]);
        Assert.Equal([ValidationTexts.LogoInvalid], errors["logoContentType"]);
        Assert.DoesNotContain(invalid.Errors, error => error.ErrorMessage == ValidationTexts.PageInvalid);
    }

    [Fact]
    public async Task Lengths_use_the_common_message_and_removing_the_logo_excludes_uploading_one()
    {
        var validator = new UpdatePublicPageRequestValidator();

        var invalid = await validator.ValidateAsync(Request() with
        {
            Name = new string('n', TextLimits.OrganizationName + 1),
            Description = new string('d', TextLimits.Description + 1),
            ContactPhoneNumber = new string('1', TextLimits.PhoneNumber + 1),
            Address = new string('a', TextLimits.Description + 1),
            RemoveLogo = true,
            LogoBase64 = "iVBORw0KGgo=",
            LogoContentType = "image/png",
            Version = null,
        }, Ct);
        var errors = invalid.Errors.ToLookup(error => RequestValidator.ToFieldName(error.PropertyName),
            error => error.ErrorMessage);

        Assert.Equal([ValidationTexts.MaxLength.Replace("{MaxLength}", TextLimits.OrganizationName.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)],
            errors["name"]);
        Assert.All(new[] { "description", "contactPhoneNumber", "address" }, field =>
            Assert.StartsWith(ValidationTexts.MaxLength[..ValidationTexts.MaxLength.IndexOf('{', StringComparison.Ordinal)],
                Assert.Single(errors[field]), StringComparison.Ordinal));
        Assert.Equal([ValidationTexts.LogoRemoveConflict], errors["logoBase64"]);
        Assert.Equal([ValidationTexts.Required], errors["version"]);
    }

    [Fact]
    public async Task A_complete_request_is_valid()
    {
        var result = await new UpdatePublicPageRequestValidator().ValidateAsync(Request(), Ct);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

    private static UpdatePublicPageRequest Request() => new("grupo-delta", "Grupo Delta", "Atención al público",
        null, "AR", "11 2345-6789", "Av. Siempreviva 742", "https://delta.example.test",
        "iVBORw0KGgo=", "image/png", false, 0);
}
