using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Common.Logging;

public static partial class OperationLog
{
    public static Task<Result<T>> RunAsync<T>(ILogger logger, TimeProvider timeProvider,
        string operation, Func<Task<Result<T>>> work) => RunCoreAsync(logger, timeProvider, operation, work);

    public static Task<Result> RunAsync(ILogger logger, TimeProvider timeProvider,
        string operation, Func<Task<Result>> work) => RunCoreAsync(logger, timeProvider, operation, work);

    private static async Task<TResult> RunCoreAsync<TResult>(ILogger logger, TimeProvider timeProvider,
        string operation, Func<Task<TResult>> work) where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(work);

        var startedAt = timeProvider.GetTimestamp();
        LogHandling(logger, operation);

        try
        {
            var result = await work();
            if (result.IsFailure)
            {
                LogFailed(logger, operation, result.Error.Code);
            }
            else
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    var elapsedMilliseconds = timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
                    LogHandled(logger, operation, elapsedMilliseconds);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            // Exception.Message can contain values from a request or an external provider.
            if (logger.IsEnabled(LogLevel.Error))
            {
                LogException(logger, operation, exception.GetType().Name);
            }

            throw;
        }
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Handling {Operation}")]
    private static partial void LogHandling(ILogger logger, string operation);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "Handled {Operation} in {ElapsedMilliseconds} ms")]
    private static partial void LogHandled(ILogger logger, string operation, double elapsedMilliseconds);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Failed {Operation}: {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, string operation, string errorCode);

    [LoggerMessage(EventId = 103, Level = LogLevel.Error, Message = "Failed {Operation}: {ExceptionType}")]
    private static partial void LogException(ILogger logger, string operation, string exceptionType);
}
