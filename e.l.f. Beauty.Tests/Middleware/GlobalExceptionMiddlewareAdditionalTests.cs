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
            var logger = new NullLogger<GlobalExceptionMiddleware>();

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
        }

        [Fact]
        public async Task Invoke_NotFoundException_MapsTo404()
        {
            RequestDelegate next = ctx => throw new NotFoundException("missing");

            var env = new TestHostEnvironment(isDevelopment: false);
            var factory = new TestProblemDetailsFactory();
            var logger = new NullLogger<GlobalExceptionMiddleware>();

            var middleware = new GlobalExceptionMiddleware(next, logger, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var pd = JsonSerializer.Deserialize<JsonDocument>(json);

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(404, context.Response.StatusCode);
            Assert.NotNull(pd);
        }

        [Fact]
        public async Task Invoke_TimeoutException_MapsTo504()
        {
            RequestDelegate next = ctx => throw new TimeoutException("timeout");

            var env = new TestHostEnvironment(isDevelopment: false);
            var factory = new TestProblemDetailsFactory();
            var logger = new NullLogger<GlobalExceptionMiddleware>();

            var middleware = new GlobalExceptionMiddleware(next, logger, factory, env);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var pd = JsonSerializer.Deserialize<JsonDocument>(json);

            Assert.Equal("application/problem+json", context.Response.ContentType);
            Assert.Equal(504, context.Response.StatusCode);
            Assert.NotNull(pd);
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
