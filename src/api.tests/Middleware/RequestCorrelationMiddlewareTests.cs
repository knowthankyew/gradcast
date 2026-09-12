using GradCast.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GradCast.Api.Tests.Middleware;

public class RequestCorrelationMiddlewareTests
{
    [Fact]
    public async Task GeneratesCorrelationIdWhenMissingFromRequest()
    {
        var context = new DefaultHttpContext();
        var middleware = new RequestCorrelationMiddleware(
            next: (innerContext) => Task.CompletedTask,
            logger: NullLogger<RequestCorrelationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey(RequestCorrelationMiddleware.CorrelationIdHeader));
        var headerValue = context.Response.Headers[RequestCorrelationMiddleware.CorrelationIdHeader].ToString();
        Assert.False(string.IsNullOrWhiteSpace(headerValue));
    }

    [Fact]
    public async Task PropagatesExistingCorrelationIdFromRequest()
    {
        var context = new DefaultHttpContext();
        const string existingId = "test-correlation-12345";
        context.Request.Headers[RequestCorrelationMiddleware.CorrelationIdHeader] = existingId;

        var middleware = new RequestCorrelationMiddleware(
            next: (innerContext) => Task.CompletedTask,
            logger: NullLogger<RequestCorrelationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(existingId, context.Response.Headers[RequestCorrelationMiddleware.CorrelationIdHeader]);
    }
}
