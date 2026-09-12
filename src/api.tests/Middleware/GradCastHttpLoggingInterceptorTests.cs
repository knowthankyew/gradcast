using GradCast.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;

namespace GradCast.Api.Tests.Middleware;

public class GradCastHttpLoggingInterceptorTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/health")]
    [InlineData("/assets/index.js")]
    [InlineData("/favicon.svg")]
    public async Task SuppressesLoggingForHealthAndStaticEndpoints(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var logContext = new HttpLoggingInterceptorContext
        {
            HttpContext = context,
            LoggingFields = HttpLoggingFields.All
        };

        var interceptor = new GradCastHttpLoggingInterceptor();
        await interceptor.OnRequestAsync(logContext);

        Assert.Equal(HttpLoggingFields.None, logContext.LoggingFields);
    }

    [Theory]
    [InlineData("/api/schools/search")]
    [InlineData("/api/finance/simulator")]
    [InlineData("/api/jobs/pulse")]
    public async Task PreservesLoggingForApiEndpoints(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var initialFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath;
        var logContext = new HttpLoggingInterceptorContext
        {
            HttpContext = context,
            LoggingFields = initialFields
        };

        var interceptor = new GradCastHttpLoggingInterceptor();
        await interceptor.OnRequestAsync(logContext);

        Assert.Equal(initialFields, logContext.LoggingFields);
    }
}
