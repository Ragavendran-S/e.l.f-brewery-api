using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using e.l.f.Validation;
using ElfValidationException = e.l.f.Validation.ValidationException;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace e.l.f.GlobalException
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly ProblemDetailsFactory _problemDetailsFactory;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            ProblemDetailsFactory problemDetailsFactory,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _problemDetailsFactory = problemDetailsFactory;
            _env = env;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                sw.Stop();
                await HandleExceptionAsync(httpContext, ex, sw.ElapsedMilliseconds);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, long elapsedMs)
        {
            var traceId = context.TraceIdentifier;
            var (status, type, title, detail, extensions) = MapExceptionToProblem(exception);

            // Add common extensions
            extensions["traceId"] = traceId;
            extensions["elapsedMs"] = elapsedMs;

            var pd = _problemDetailsFactory.CreateProblemDetails(
                context,
                status,
                title,
                type,
                detail);

            // Merge extensions into ProblemDetails.Extensions
            foreach (var kv in extensions)
            {
                pd.Extensions[kv.Key] = kv.Value;
            }

            // Log with category and structured data
            var logLevel = status >= 500 ? LogLevel.Error : LogLevel.Warning;
            _logger.Log(logLevel, exception, "Handled exception. Category: {Category}, Status: {Status}, TraceId: {TraceId}, ElapsedMs: {ElapsedMs}",
                pd.Type ?? "server-error", status, traceId, elapsedMs);

            // In non-development, avoid exposing exception details
            if (!_env.IsDevelopment())
            {
                pd.Extensions["detail"] = null;
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = status;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await JsonSerializer.SerializeAsync(context.Response.Body, pd, pd.GetType(), options);
        }

        private (int status, string type, string title, string detail, Dictionary<string, object?> extensions)
            MapExceptionToProblem(Exception ex)
        {
            var extensions = new Dictionary<string, object?>();

            // Validation errors (use your validation exception type)
            if (ex is ElfValidationException vex)
            {
                extensions["errors"] = vex.Errors;
                return (StatusCodes.Status400BadRequest,
                        "https://example.com/probs/validation",
                        "Validation error",
                        vex.Message,
                        extensions);
            }

            // Domain not found
            if (ex is NotFoundException nf)
            {
                return (StatusCodes.Status404NotFound, "https://example.com/probs/not-found", "Resource not found", nf.Message, extensions);
            }

            // Authentication / Authorization
            if (ex is UnauthorizedAccessException)
            {
                return (StatusCodes.Status401Unauthorized, "https://example.com/probs/unauthorized", "Unauthorized", ex.Message, extensions);
            }

            // Upstream HTTP errors (HttpRequestException with status)
            if (ex is UpstreamApiException uae)
            {
                // UpstreamApiException should carry StatusCode and optionally Body
                extensions["upstreamStatus"] = (int?)uae.StatusCode;
                extensions["upstreamMessage"] = uae.Message;
                var status = uae.StatusCode >= 500 ? StatusCodes.Status502BadGateway : StatusCodes.Status502BadGateway;
                return (status, "https://example.com/probs/upstream-error", "Upstream API error", uae.Message, extensions);
            }

            // Timeouts and cancellations
            if (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
            {
                return (StatusCodes.Status504GatewayTimeout, "https://example.com/probs/timeout", "Request timed out", ex.Message, extensions);
            }

            // Fallback: unexpected server error
            return (StatusCodes.Status500InternalServerError, "https://example.com/probs/internal", "An unexpected error occurred", _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred", extensions);
        }
    }
}