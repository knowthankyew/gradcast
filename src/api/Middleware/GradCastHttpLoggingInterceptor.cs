using Microsoft.AspNetCore.HttpLogging;

namespace GradCast.Api.Middleware;

public class GradCastHttpLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        var path = logContext.HttpContext.Request.Path.Value ?? string.Empty;

        // Suppress noisy health checks and static frontend assets from polluting API logs
        if (path == "/health" ||
            path == "/api/health" ||
            path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.svg", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            logContext.LoggingFields = HttpLoggingFields.None;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        return ValueTask.CompletedTask;
    }
}
