using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using e.l.f.GlobalException;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using e.l.f.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace e.l.f._Beauty.Tests.Middleware
{
    public class GlobalExceptionMiddlewareAdditionalTests
    {
        [Fact]
        public async Task Invoke_ValidationException_MapsToBadRequest()
        {
            RequestDelegate next = ctx => throw new e.l.f.Validation.ValidationException("bad", new System.Collections.Generic.Dictionary<string, string[]> { { "field", new[] { "err" } } });

            var env = new TestHostEnvironment(isDevelopment: true);
            var factory = new TestProblemDetailsFactory();
            var testLogger = new e.l.f._Beauty.Tests.Utils.TestLogger<GlobalExceptionMiddleware>();
            var logger = testLogger as Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>;

            var middleware = new GlobalExceptionMiddleware(next, logger, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var pd = JsonSerializer.Deserialize<JsonDocument>(json);

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(400, context.Response.StatusCode);
            Assert.NotNull(pd);

            // Assert a warning log entry with the original ValidationException
            Assert.Contains(testLogger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && e.Exception is e.l.f.Validation.ValidationException);
        }

        [Fact]
        public async Task Invoke_NotFoundException_MapsTo404()
        {
            RequestDelegate next = ctx => throw new NotFoundException("missing");

            var env = new TestHostEnvironment(isDevelopment: false);
            var factory = new TestProblemDetailsFactory();
            var testLogger2 = new e.l.f._Beauty.Tests.Utils.TestLogger<GlobalExceptionMiddleware>();
            var logger2 = testLogger2 as Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>;

            var middleware = new GlobalExceptionMiddleware(next, logger2, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var pd = JsonSerializer.Deserialize<JsonDocument>(json);

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(404, context.Response.StatusCode);
            Assert.NotNull(pd);

            // Assert a warning log entry with the original NotFoundException
            Assert.Contains(testLogger2.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && e.Exception is e.l.f.Validation.NotFoundException);
        }

        [Fact]
        public async Task Invoke_TimeoutException_MapsTo504()
        {
            RequestDelegate next = ctx => throw new TimeoutException("timeout");

            var env = new TestHostEnvironment(isDevelopment: false);
            var factory = new TestProblemDetailsFactory();
            var testLogger3 = new e.l.f._Beauty.Tests.Utils.TestLogger<GlobalExceptionMiddleware>();
            var logger3 = testLogger3 as Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware>;

            var middleware = new GlobalExceptionMiddleware(next, logger3, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var pd = JsonSerializer.Deserialize<JsonDocument>(json);

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(504, context.Response.StatusCode);
            Assert.NotNull(pd);

            // Assert an error log entry with the original TimeoutException
            Assert.Contains(testLogger3.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Exception is System.TimeoutException);
        }

        // reuse test helpers from other test file
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
    }
}
