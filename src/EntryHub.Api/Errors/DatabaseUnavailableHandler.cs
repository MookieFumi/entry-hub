using Microsoft.AspNetCore.Diagnostics;

using Npgsql;

namespace EntryHub.Api.Errors;

/// <summary>
/// Answers 503 with a <c>Retry-After</c> header when PostgreSQL cannot be reached or times out, so clients know the
/// request may work if they try again. Any other exception falls through to the generic 500.
/// </summary>
public sealed partial class DatabaseUnavailableHandler(
    IProblemDetailsService problemDetails,
    ILogger<DatabaseUnavailableHandler> logger) : IExceptionHandler
{
    private const int RetryAfterSeconds = 5;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsTransientDatabaseError(exception))
        {
            return false;
        }

        // Since .NET 10 the middleware does not log exceptions that a handler handles, so the handler logs its own.
        LogDatabaseUnavailable(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Database unavailable",
                Detail = $"The booking database did not answer. Try again in {RetryAfterSeconds} seconds.",
            },
        });
    }

    // EF Core's Npgsql execution strategy wraps transient errors in an InvalidOperationException, even with retries
    // off, so the Npgsql exception can be the inner one.
    private static bool IsTransientDatabaseError(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is NpgsqlException { IsTransient: true })
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The database did not answer {Method} {Path}")]
    private static partial void LogDatabaseUnavailable(ILogger logger, Exception exception, string method, string path);
}