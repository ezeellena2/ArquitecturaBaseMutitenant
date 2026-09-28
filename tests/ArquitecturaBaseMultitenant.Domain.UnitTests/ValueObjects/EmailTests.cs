using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

public sealed class EmailTests
{
    [Fact]
    public void Email_is_trimmed_and_lowercased()
    {
        var result = Email.Create("  Ana.Perez@Example.COM ");

        Assert.True(result.IsSuccess);
        Assert.Equal("ana.perez@example.com", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@@example.com")]
    [InlineData("ana@example")]
    [InlineData("ana maria@example.com")]
    [InlineData("ana@example.com.")]
    public void Invalid_emails_are_rejected(string? value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailErrors.InvalidCode, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Emails_longer_than_the_limit_are_rejected()
    {
        var value = new string('a', Email.MaxLength - "@example.com".Length + 1) + "@example.com";

        Assert.True(Email.Create(value).IsFailure);
    }

    [Fact]
    public void A_local_part_longer_than_64_characters_is_rejected()
    {
        Assert.Equal(EmailErrors.Invalid, Email.Create(new string('a', 65) + "@example.com").Error);
        Assert.True(Email.Create(new string('a', 64) + "@example.com").IsSuccess);
    }

    [Fact]
    public void The_total_limit_is_checked_after_idn_conversion()
    {
        var acceptedDomain = $"{new string('a', 63)}.{new string('b', 63)}.{new string('c', 61)}";
        var rejectedDomain = $"{new string('a', 63)}.{new string('b', 63)}.{new string('c', 62)}";

        Assert.Equal(Email.MaxLength, Email.Create(new string('a', 64) + "@" + acceptedDomain).Value.Value.Length);
        Assert.Equal(EmailErrors.Invalid, Email.Create(new string('a', 64) + "@" + rejectedDomain).Error);
    }

    [Fact]
    public void A_unicode_domain_is_stored_as_idn_ascii_and_displayed_legibly()
    {
        var result = Email.Create("  JUAN@ÑANDÚ.com.ar  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("juan@xn--and-6ma2c.com.ar", result.Value.Value);
        Assert.Equal("juan@ñandú.com.ar", result.Value.DisplayValue);
        Assert.Equal(result.Value, Email.Create("juan@xn--and-6ma2c.com.ar").Value);
        Assert.Equal("juan@ñandú.com.ar", Email.Create(result.Value.Value).Value.DisplayValue);
    }

    [Fact]
    public void Canonical_unicode_composition_is_used_for_comparison()
    {
        var composed = Email.Create("josé@ñandú.com.ar").Value;
        var decomposed = Email.Create("  JOSE\u0301@N\u0303ANDU\u0301.com.ar  ").Value;

        Assert.Equal(composed.Value, decomposed.Value);
        Assert.Equal(composed.DisplayValue, decomposed.DisplayValue);
        Assert.Equal(composed, decomposed);
    }

    [Theory]
    [InlineData("a@.example.com")]
    [InlineData("a@example..com")]
    [InlineData("a@bad domain.com")]
    [InlineData("a@host_name.com")]
    [InlineData("a@host:443.com")]
    [InlineData("a@host/path.com")]
    [InlineData("a@\ud800.com")]
    public void Malformed_domains_return_a_validation_error(string value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailErrors.Invalid, result.Error);
    }

    [Fact]
    public void Shared_email_cases_use_the_canonical_display_text()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "docs", "contracts", "format-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cases = document.RootElement.GetProperty("cases").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "email")
            .ToArray();

        Assert.NotEmpty(cases);
        foreach (var item in cases)
        {
            var result = Email.Create(item.GetProperty("input").GetString());
            Assert.True(result.IsSuccess, item.GetProperty("id").GetString());
            Assert.Equal(item.GetProperty("expected").GetString(), result.Value.DisplayValue);
        }
    }

    [Fact]
    public void Emails_with_the_same_normalized_value_are_equal()
    {
        Assert.Equal(Email.Create("ANA@example.com").Value, Email.Create("ana@example.com").Value);
    }
}
