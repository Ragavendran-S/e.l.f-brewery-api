using e.l.f._Beauty.Repository;
using e.l.f._Beauty.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using e.l.f._Beauty.ConfigureSwaggerOptions;
using e.l.f._Beauty.JwtOptions;
using e.l.f.GlobalException;
using Microsoft.Extensions.Logging;
using e.l.f.Logging;

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
var key = Environment.GetEnvironmentVariable("Jwt:Key");
var jwtKey = builder.Configuration["Jwt:Key"];
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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtKey))
        };
    });

builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddAutoMapper(typeof(BreweryProfile));

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddOptions<JwtOptionsAuth>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(options => !string.IsNullOrEmpty(options.Key), "JWT Key must be provided")
    .ValidateOnStart();
// Register DbContext with SQLite
builder.Services.AddDbContext<BreweryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// Add services to the container.
builder.Services.AddScoped<TokenValidator>();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
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
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();


// Use global exception handler

app.MapControllers();

app.Run();

