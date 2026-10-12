using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Common.Logging;

/// <summary>Conserva el diagnóstico técnico sin entregar mensajes, Data ni objetos de excepción al proveedor de logs.</summary>
public static partial class ExceptionLog
{
    public static void Write(ILogger logger, string operation, Exception exception)
    {
        if (!logger.IsEnabled(LogLevel.Error)) return;
        LogFailure(logger, operation, exception.GetType().Name, exception.StackTrace ?? string.Empty);
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions) Write(logger, operation, inner);
        }
        else if (exception.InnerException is { } inner) Write(logger, operation, inner);
    }

    [LoggerMessage(EventId = 103, Level = LogLevel.Error, Message = "Failed {Operation}: {ExceptionType}\n{StackTrace}")]
    private static partial void LogFailure(ILogger logger, string operation, string exceptionType, string stackTrace);
}
