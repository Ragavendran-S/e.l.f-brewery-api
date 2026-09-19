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
    throw new InvalidOperationException("Jwt:Key must be configured.");
}

var jwtKey = ResolveJwtKey(builder.Configuration) ?? throw new InvalidOperationException("Configuration value 'Jwt:Key' is required. In Development use: dotnet user-secrets set \"Jwt:Key\" \"<base64-key>\" or set environment variable 'Jwt__Key'.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
// Override appsettings.json with GitHub secrets
// 🔒 Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($" Token failed: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                Console.WriteLine(" Token validated successfully");
                return Task.CompletedTask;
            }
        };
        // Validate jwtKey early with a clear error message so startup fails fast if misconfigured.
        // Normalize key bytes using JwtKeyHelper so runtime uses the same bytes as token issuance
        var keyBytes = e.l.f._Beauty.Security.JwtKeyHelper.GetKeyBytes(jwtKey);
        options.TokenValidationParameters = new TokenValidationParameters

        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
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
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
builder.Services.AddDbContext<BreweryDbContext>(options =>
    options.UseSqlite(defaultConn));
// Add services to the container.
builder.Services.AddScoped<TokenValidator>();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
// Register in-memory brewery cache implementation
builder.Services.AddScoped<IBreweryCache, MemoryBreweryCache>();
builder.Services.AddHttpClient<IBreweryRepository, BreweryRepository>();
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
    db.Database.Migrate(); // applies pending migrations
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

