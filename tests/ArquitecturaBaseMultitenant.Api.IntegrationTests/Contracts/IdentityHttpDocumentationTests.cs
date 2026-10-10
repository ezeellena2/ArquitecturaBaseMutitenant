using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

[Collection(ApiTestGroup.Name)]
public sealed class IdentityHttpDocumentationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Identity_errors_document_only_the_states_reachable_by_each_operation()
    {
        var doc = await DocumentAsync();
        foreach (var path in new[] { "/api/auth/login-code/verify", "/api/auth/signup/verify", "/api/auth/business-signup/verify" })
        {
            AssertError(doc, path, "Identity.Account.Suspended", 403);
            AssertError(doc, path, "Identity.Account.LockedOut", 429);
            AssertError(doc, path, "Identity.Account.PendingDeletion", 403);
            AssertError(doc, path, "Tenancy.Access.PlatformOnly", 403);
        }
        AssertError(doc, "/api/auth/login-code/verify", "Legal.AccountDeletion.GraceExpired", 409);
        AssertError(doc, "/api/me/login-methods/{methodId}/verify", "Identity.Account.Suspended", 403);
        AssertError(doc, "/api/me/login-methods/{methodId}/verify", "Identity.Account.PendingDeletion", 403);
        AssertError(doc, "/api/me/login-methods/{methodId}/verify", "Identity.LoginMethod.AlreadyVerified", 409);
        AssertMissing(doc, "/api/me/login-methods/{methodId}/verify", "Identity.Account.LockedOut", "Tenancy.Access.PlatformOnly");
        AssertMissing(doc, "/api/account-recovery/verify", "Identity.Account.Suspended", "Identity.Account.LockedOut", "Identity.Account.PendingDeletion", "Tenancy.Access.PlatformOnly");
        foreach (var action in new[] { "preview", "redeem" })
        {
            var path = "/api/auth/login-link/" + action;
            AssertError(doc, path, "Auth.LoginLink.Invalid", 400);
            AssertError(doc, path, "Identity.Account.Suspended", 403);
            AssertError(doc, path, "Identity.Account.LockedOut", 429);
            AssertMissing(doc, path, "Auth.LoginLink.Expired", "Auth.LoginLink.AlreadyUsed", "Auth.LoginLink.TooManyRequests");
        }
        AssertMissing(doc, "/api/auth/login-link/preview", "Identity.Account.PendingDeletion");
        AssertError(doc, "/api/auth/login-link/redeem", "Identity.Account.PendingDeletion", 403);
        AssertError(doc, "/api/platform/recoveries/{id}/approve", "Platform.AccountRecovery.MethodAlreadyOnAccount", 409);
        AssertError(doc, "/api/platform/recoveries/{id}/approve", "Platform.AccountRecovery.MethodInUse", 409);
        AssertMissing(doc, "/api/platform/recoveries/{id}/approve", "Platform.AccountRecovery.PhoneMethodInUse");
        AssertMissing(doc, "/api/platform/recoveries/{id}/reject", "Platform.AccountRecovery.PhoneMethodInUse", "Platform.AccountRecovery.MethodInUse", "Platform.AccountRecovery.MethodAlreadyOnAccount", "Platform.AccountRecovery.NoAccount", "Platform.AccountRecovery.OperatorAccount");
        foreach (var path in doc["paths"]!.AsObject().Where(path => IdentityPaths.Contains(path.Key)))
        foreach (var operation in path.Value!.AsObject().Where(item => item.Value is JsonObject))
        foreach (var error in operation.Value?["x-error-codes"]?.AsArray() ?? [])
        {
            var code = error!["code"]!.GetValue<string>();
            Assert.DoesNotContain(code, new[] { "Auth.Account.Disabled", "Auth.Account.LockedOut" });
            var status = error["status"]!.GetValue<int>();
            if (code == "Request.IdempotencyKeyReused") Assert.Equal(422, status);
            foreach (var language in new[] { "es", "en" })
            {
                var example = operation.Value!["responses"]![status.ToString(System.Globalization.CultureInfo.InvariantCulture)]!["content"]!["application/problem+json"]!["examples"]![code + "." + language]!["value"]!;
                Assert.Equal(status, example["status"]!.GetValue<int>());
                Assert.Equal(error[language]!.GetValue<string>(), example["detail"]!.GetValue<string>());
                if (code == "Identity.Account.LockedOut") Assert.Null(example["retryAfter"]);
            }
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Reused_link_key_error_matches_the_documented_status_and_localized_example(string language)
    {
        var doc = await DocumentAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var key = Guid.NewGuid().ToString();
        using var first = await SendAsync("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        using var reused = await SendAsync("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        var problem = await reused.Content.ReadFromJsonAsync<JsonObject>(Ct);
        var documented = doc["paths"]!["/api/auth/login-link/redeem"]!["post"]!["responses"]!["422"];
        Assert.NotNull(documented);
        var example = documented["content"]!["application/problem+json"]!["examples"]!["Request.IdempotencyKeyReused." + language]!["value"]!;
        Assert.Equal(problem!["status"]!.GetValue<int>(), example["status"]!.GetValue<int>());
        Assert.Equal(problem["code"]!.GetValue<string>(), example["code"]!.GetValue<string>());
        Assert.Equal(problem["detail"]!.GetValue<string>(), example["detail"]!.GetValue<string>());
        Assert.False(reused.Headers.Contains("Set-Cookie"));

        async Task<HttpResponseMessage> SendAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-link/redeem") { Content = JsonContent.Create(new { token, access = "consumer" }) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, Ct);
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Recovery_code_without_an_active_proof_has_no_invented_field_or_attempt_count(string language)
    {
        var doc = await DocumentAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account-recovery/verify")
        {
            Content = JsonContent.Create(new { email = "documentation-" + Guid.NewGuid().ToString("N") + "@example.test", code = "123456", nonce = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("Auth.LoginCode.Invalid", problem!["code"]!.GetValue<string>());
        Assert.Null(problem["errors"]);
        Assert.Null(problem["attemptsLeft"]);
        var example = doc["paths"]!["/api/account-recovery/verify"]!["post"]!["responses"]!["400"]!["content"]!["application/problem+json"]!["examples"]!["Auth.LoginCode.Invalid." + language]!["value"]!;
        Assert.Equal(problem["detail"]!.GetValue<string>(), example["detail"]!.GetValue<string>());
        Assert.Null(example["errors"]);
        Assert.Null(example["attemptsLeft"]);
    }

    private async Task<JsonObject> DocumentAsync()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseDevelopmentTestOwner());
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
    }

    private static void AssertError(JsonObject doc, string path, string code, int status)
    {
        var error = Assert.Single(doc["paths"]![path]!["post"]!["x-error-codes"]!.AsArray(), value => value?["code"]?.GetValue<string>() == code);
        Assert.Equal(status, error!["status"]!.GetValue<int>());
    }

    private static void AssertMissing(JsonObject doc, string path, params string[] codes) =>
        Assert.DoesNotContain(doc["paths"]![path]!["post"]!["x-error-codes"]!.AsArray(), value => codes.Contains(value?["code"]?.GetValue<string>(), StringComparer.Ordinal));

    private static readonly HashSet<string> IdentityPaths =
    [
        "/api/auth/methods", "/api/auth/login-code", "/api/auth/login-code/verify", "/api/auth/signup", "/api/auth/signup/verify",
        "/api/auth/business-signup", "/api/auth/business-signup/code", "/api/auth/business-signup/verify",
        "/api/me/login-methods", "/api/me/login-methods/{methodId}/code", "/api/me/login-methods/{methodId}/verify",
        "/api/me/login-methods/{methodId}/primary", "/api/me/login-methods/{methodId}",
        "/api/account-recovery/preview", "/api/account-recovery/code", "/api/account-recovery/verify", "/api/account-recovery",
        "/api/auth/login-link/preview", "/api/auth/login-link/redeem", "/api/platform/recoveries/{id}/approve", "/api/platform/recoveries/{id}/reject",
    ];
}
