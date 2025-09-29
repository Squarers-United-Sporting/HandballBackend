using System.Globalization;
using HandballBackend.EndpointHelpers;

namespace HandballBackend.ErrorTypes;

public class ExceptionLoggingMiddleware(RequestDelegate next) {
    public async Task InvokeAsync(HttpContext context) {
        var originalBody = context.Response.Body;
        await using var memStream = new MemoryStream();
        context.Response.Body = memStream;
        try {
            await next(context);
        } catch (Exception ex) {
            await ExceptionLoggingHelper.Write(ex, context);
            throw;
        }

        memStream.Seek(0, SeekOrigin.Begin);
        var bodyText = await new StreamReader(memStream).ReadToEndAsync();
        if (context.Response.StatusCode >= 400) {
            await ExceptionLoggingHelper.Write(context, bodyText);
        }

        // Reset stream and copy response back
        memStream.Seek(0, SeekOrigin.Begin);
        await memStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }
}

public static class ExceptionLoggingMiddlewareExtensions {
    public static IApplicationBuilder UseExceptionLogging(
        this IApplicationBuilder builder) {
        return builder.UseMiddleware<ExceptionLoggingMiddleware>();
    }
}