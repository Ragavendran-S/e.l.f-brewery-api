using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using e.l.f.GlobalException;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace e.l.f._Beauty.Tests.Middleware
{
    public class GlobalExceptionMiddlewareTests
    {
        [Fact]
        public async Task Invoke_LogsAndReturnsProblemDetails_OnUnhandledException()
        {
            RequestDelegate next = ctx => throw new InvalidOperationException("boom");

            var env = new TestHostEnvironment(isDevelopment: true);
            var factory = new TestProblemDetailsFactory();
            var testLogger = new e.l.f._Beauty.Tests.Utils.TestLogger<GlobalExceptionMiddleware>();
            var logger = testLogger as Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>;

            var middleware = new GlobalExceptionMiddleware(next, logger, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var json = await reader.ReadToEndAsync();

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(500, context.Response.StatusCode);

            // Ensure the response is parseable problem details
            var doc = JsonSerializer.Deserialize<JsonDocument>(json);
            Assert.NotNull(doc);

            // assert a logged error for 500
            Assert.Contains(testLogger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Exception is InvalidOperationException);
        }

        private class TestProblemDetailsFactory : ProblemDetailsFactory
        {
            public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            {
                return new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Type = type,
                    Detail = detail,
                    Instance = instance
                };
            }

            public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            {
                return new ValidationProblemDetails(modelStateDictionary)
                {
                    Status = statusCode,
                    Title = title,
                    Type = type,
                    Detail = detail,
                    Instance = instance
                };
            }
        }

        private class TestHostEnvironment : IHostEnvironment
        {
            public TestHostEnvironment(bool isDevelopment)
            {
                EnvironmentName = isDevelopment ? "Development" : "Production";
                ContentRootFileProvider = null!;
            }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; } = "test";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
        }
    }
}
