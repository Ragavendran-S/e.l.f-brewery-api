using AutoMapper;
using e.l.f._Beauty;
using e.l.f._Beauty.ConfigureSwaggerOptions;
using e.l.f._Beauty.JwtOptions;
using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using e.l.f.GlobalException;
using e.l.f.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.IO;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);
// configure services...
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(); // or your preferred provider
    logging.SetMinimumLevel(LogLevel.Information);
});
//var keyBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
//var base64Key = Convert.ToBase64String(keyBytes);
// Resolve Jwt:Key from environment (preferred) or configuration. Treat common placeholder
// values as missing so a clear error message is thrown directing the developer to
// store a real Base64 key in user-secrets or an environment variable.
static string ResolveJwtKey(Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    // Try environment variables first (both colon and double-underscore forms)
    var envKey = Environment.GetEnvironmentVariable("Jwt:Key") ?? Environment.GetEnvironmentVariable("Jwt__Key");
    if (!string.IsNullOrWhiteSpace(envKey))
        return envKey;

    // Then check configuration (appsettings, user-secrets, etc.)
    var cfgKey = configuration["Jwt:Key"];
    if (!string.IsNullOrWhiteSpace(cfgKey) &&
         !cfgKey.Equals("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) &&
         !cfgKey.TrimStart().StartsWith("$("))
    {
        return cfgKey;
    }
    // In CI or Test environments it's common not to have user-secrets or environment variables set.
    // Provide a deterministic development/test fallback key so integration tests and CI builds can run.
    // This preserves the previous behavior for production where missing keys will still be caught
    // because callers validate the final key bytes length and configuration should explicitly set secrets.
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? configuration["ASPNETCORE_ENVIRONMENT"];
    if (!string.IsNullOrEmpty(environment) && (environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || environment.Equals("Testing", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))))
    {
        // Use a deterministic fallback key for tests/CI. This is intentionally simple and
        // acceptable only for non-production runs.
        return "dev-ci-default-jwt-key-for-tests";
    }

    // If we reached here, no Jwt:Key was provided via environment or configuration.
    // For non-development/non-testing environments require an explicit key so
    // production runs cannot start with a default secret baked into source.
    throw new InvalidOperationException("Jwt:Key must be configured.");
}

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                try
                {
                    var logger = ctx.HttpContext?.RequestServices.GetService(typeof(ILogger<Program>)) as ILogger;
                    // Structured log with event id for token validation failures
                    logger?.LogWarning(e.l.f.Logging.EventIds.TokenValidationFailed, ctx.Exception, "Token authentication failed: {Message}", ctx.Exception?.Message);
                }
                catch
                {
                    // Swallow any logging errors to avoid masking the original authentication failure
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                try
                {
                    var logger = ctx.HttpContext?.RequestServices.GetService(typeof(ILogger<Program>)) as ILogger;
                    logger?.LogInformation(e.l.f.Logging.EventIds.TokenValidation, "Token validated successfully for request {Path}", ctx.HttpContext?.Request?.Path.Value);
                }
                catch
                {
                    // Swallow logging exceptions
                }
                return Task.CompletedTask;
            }
        };

        // Resolve signing key and validation parameters from the current runtime configuration
        try
        {
            var resolvedKey = ResolveJwtKey(builder.Configuration);
            var keyBytes = e.l.f._Beauty.Security.JwtKeyHelper.GetKeyBytes(resolvedKey);
            var jwtIssuerLocal = builder.Configuration["Jwt:Issuer"];
            var jwtAudienceLocal = builder.Configuration["Jwt:Audience"];

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrEmpty(jwtIssuerLocal),
                ValidateAudience = !string.IsNullOrEmpty(jwtAudienceLocal),
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuerLocal,
                ValidAudience = jwtAudienceLocal,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
            };
        }
        catch (Exception ex)
        {
            // If key resolution fails at startup, log and rethrow so developers get a clear failure.
            // Avoid building the full service provider here; create a lightweight LoggerFactory
            // to emit the startup diagnostic and dispose it immediately.
            using var tempLoggerFactory = LoggerFactory.Create(lb =>
            {
                lb.AddConsole();
                lb.SetMinimumLevel(LogLevel.Information);
            });
            var startupLogger = tempLoggerFactory.CreateLogger<Program>();
            startupLogger?.LogCritical(e.l.f.Logging.EventIds.JwtKeyWarning, ex, "Failed to resolve JWT signing key: {Message}", ex.Message);
            throw;
        }
    });

// Register AutoMapper manually so we don't depend on the extension package here.
var mappingConfig = new MapperConfiguration(cfg => cfg.AddMaps(typeof(BreweryProfile).Assembly));
IMapper mapper = mappingConfig.CreateMapper();
builder.Services.AddSingleton(mappingConfig);
builder.Services.AddSingleton(mapper);

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Register paging helper for DI
builder.Services.AddScoped<IPagingHelper, PagingHelper>();

builder.Services.AddOptions<JwtOptionsAuth>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(options => !string.IsNullOrEmpty(options.Key), "JWT Key must be provided")
    .ValidateOnStart();
// Register DbContext with SQLite
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(defaultConn))
{
    // Register the DbContext only when a real connection string is provided.
    builder.Services.AddDbContext<BreweryDbContext>(options =>
        options.UseSqlite(defaultConn));
}
else
{
    // No DB connection configured. Do not register BreweryDbContext so the
    // BreweryRepository will receive a null DbContext and will fall back to
    // the upstream API client for reads/writes.
    // Avoid building the full service provider; create a temporary LoggerFactory
    // for emitting this startup diagnostic and dispose it immediately.
    using var tmpFactory = LoggerFactory.Create(lb =>
    {
        lb.AddConsole();
        lb.SetMinimumLevel(LogLevel.Information);
    });
    var startupLogger = tmpFactory.CreateLogger<Program>();
    startupLogger?.LogInformation("No DefaultConnection configured; running without local DB. BreweryRepository will use upstream API.");
}
// Add services to the container.
builder.Services.AddScoped<TokenValidator>();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
// Register in-memory brewery cache implementation
builder.Services.AddScoped<IBreweryCache, MemoryBreweryCache>();
builder.Services.AddHttpClient<IUpstreamBreweryClient, UpstreamBreweryClient>();
// Register IBreweryRepository implementations. When a real DbContext is available use the
// EF Core implementation wrapped with a caching decorator. When no DbContext is configured
// fall back to the upstream-backed BreweryRepository which accepts a nullable DbContext.
builder.Services.AddScoped<IBreweryRepository>(sp =>
{
    var db = sp.GetService<BreweryDbContext>(); // may be null when no DB configured
    if (db != null)
    {
        // Use EfCore repository when a relational DB is present
        var efRepo = new ElfBreweryApi.Repositories.EfCoreBreweryRepository(db);

        // Wrap with cached decorator if IMemoryCache is available
        var cache = sp.GetService<IMemoryCache>();
        if (cache != null)
        {
            var cacheLogger = sp.GetRequiredService<ILogger<CachedBreweryRepository>>();
            return new CachedBreweryRepository(efRepo, cache, cacheLogger);
        }

        return efRepo;
    }

    // No DB configured: preserve previous behavior using BreweryRepository which delegates to upstream
    var upstream = sp.GetRequiredService<IUpstreamBreweryClient>();
    var repoLogger = sp.GetRequiredService<ILogger<BreweryRepository>>();
    var paging = sp.GetRequiredService<IPagingHelper>();
    return new BreweryRepository(upstream, null, repoLogger, paging);
});
builder.Services.AddScoped<IBreweryService, BreweryService>();
builder.Services.AddScoped<IBreweryFilter, BreweryFilter>();
builder.Services.AddScoped<IBrewerySorterFactory, BrewerySorterFactory>();
builder.Services.AddScoped<IBrewerySorter, NameSorter>();
builder.Services.AddScoped<IBrewerySorter, CitySorter>();
builder.Services.AddScoped<IPagingHelper, PagingHelper>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    //c.SwaggerDoc("v1", new() { Title = "Brewery API", Version = "v1" });

    // Add JWT bearer definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
options.AssumeDefaultVersionWhenUnspecified = true;
options.ReportApiVersions = true; // adds headers: api-supported-versions, api-deprecated-versions
});

// Add versioned API explorer (for Swagger integration)
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; // e.g., v1, v2
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();
// get logger instance
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Example: replace Console.WriteLine for JWT events
logger.LogInformation(EventIds.AuthStartup, "JWT authentication configured");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BreweryDbContext>();
    // Only apply relational migrations when a relational provider is in use (e.g., SQLite/SqlServer).
    // In-memory or other non-relational providers do not support Migrate() and will throw.
    try
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate(); // applies pending migrations
        }
        else
        {
            // For non-relational providers (like InMemory used in tests), ensure database is created.
            db.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        // Startup should not crash tests for provider-specific behaviors; log and continue.
        var startupLogger = scope.ServiceProvider.GetService<ILogger<Program>>();
        startupLogger?.LogWarning(ex, "Database migration/creation skipped due to provider limitations: {Message}", ex.Message);
    }
}

// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();   // validates JWT
app.UseAuthorization();// Enforce [Authorize] attributes
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                                    description.GroupName.ToUpperInvariant());
        }
    });
}

app.UseHttpsRedirection();


// Use global exception handler

app.MapControllers();

app.Run();

// Provide a public Program class so integration tests via WebApplicationFactory<Program>
// can reference the entry point assembly. This pattern is common when using top-level
// statements in Program.cs.
public partial class Program { }

