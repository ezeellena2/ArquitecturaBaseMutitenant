using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class OperationLogTests
{
    [Fact]
    public async Task Successful_operation_logs_start_and_elapsed_time_without_result_data()
    {
        var logger = new FakeLogger<OperationLogTests>();
        var clock = new FakeTimeProvider();
        const string privateEmail = "ana@example.com";

        var result = await OperationLog.RunAsync(logger, clock, "ReadProfile", () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(125));
            return Task.FromResult(Result.Success(privateEmail));
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(privateEmail, result.Value);
        var records = logger.Collector.GetSnapshot();
        Assert.Equal(["Handling ReadProfile", "Handled ReadProfile in 125 ms"],
            records.Select(record => record.Message));
        Assert.All(records, record => Assert.Equal(LogLevel.Information, record.Level));
        Assert.DoesNotContain(privateEmail, string.Join(' ', records.Select(record => record.Message)));
    }

    [Fact]
    public async Task Business_failure_logs_only_stable_error_code()
    {
        var logger = new FakeLogger<OperationLogTests>();
        var clock = new FakeTimeProvider();
        var error = Error.Validation("Users.Email.Invalid", "secret@example.com");

        var result = await OperationLog.RunAsync(logger, clock, "UpdateEmail",
            () => Task.FromResult(Result.Failure(error)));

        Assert.True(result.IsFailure);
        var records = logger.Collector.GetSnapshot();
        Assert.Equal(["Handling UpdateEmail", "Failed UpdateEmail: Users.Email.Invalid"],
            records.Select(record => record.Message));
        Assert.Equal(LogLevel.Warning, records[^1].Level);
        Assert.DoesNotContain("secret@example.com", string.Join(' ', records.Select(record => record.Message)));
    }

    [Fact]
    public async Task Exception_is_rethrown_without_logging_its_sensitive_message()
    {
        var logger = new FakeLogger<OperationLogTests>();
        var clock = new FakeTimeProvider();
        const string secret = "token=12345";

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OperationLog.RunAsync<string>(logger, clock, "SendMessage",
                () => throw new InvalidOperationException(secret)));

        Assert.Equal(secret, thrown.Message);
        var records = logger.Collector.GetSnapshot();
        Assert.Equal(["Handling SendMessage", "Failed SendMessage: InvalidOperationException"],
            records.Select(record => record.Message));
        Assert.Equal(LogLevel.Error, records[^1].Level);
        Assert.DoesNotContain(secret, string.Join(' ', records.Select(record => record.Message)));
    }
}
