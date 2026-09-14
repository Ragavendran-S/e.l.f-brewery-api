Here’s a fully restructured **README.md** that incorporates all the missing sections you called out — architecture decisions, cache strategy rationale, EF Core/SQLite explanation, JWT/Base64 rationale, and known limitations. It’s written to be clear, professional, and developer‑friendly.

---

# Brewery API

## Overview
**Brewery API** is a .NET Web API that provides brewery search and listing features with JWT authentication, Swagger documentation, in‑memory caching for external API responses, structured logging, API versioning, and a global exception handler. The project includes a token helper for manual validation and uses `HttpClient` to fetch data from the Open Brewery DB.

---

## Architecture Decisions
- **Service vs Repository split**:  
  - `BreweryRepository` handles external API calls and data access.  
  - `BreweryService` orchestrates filtering, sorting, and paging.  
- **Caching strategy**: Centralized in a repository decorator (`CachedBreweryRepository`) to avoid duplicate caching layers.  
- **Single Responsibility Principle (SRP)**: Filters, sorters, and paging helpers are separated into their own classes.  
- **Global exception handling**: Implemented via `GlobalExceptionMiddleware` for consistent error responses.  
- **API versioning**: Controllers are versioned (`v1`, `v2`) and exposed in Swagger.

---

## Technology Rationale
- **EF Core/SQLite**: Present but commented out. Initially used for local persistence experiments, later replaced by external API calls. Kept in the codebase for potential offline mode or future database integration.  
- **JWT Authentication**: Chosen for stateless, industry‑standard API security.  
- **Base64 secrets**: The JWT key is stored as Base64 to simplify configuration. While convenient, this is not ideal for production — a secure secrets manager should be used.  
- **Swagger/OpenAPI**: Provides interactive documentation and testing.  

---

## Cache Strategy
- **Repository caching**: Implemented via `IMemoryCache` with a 10‑minute expiration.  
- **Why centralized caching**: Avoids duplication between service and repository layers.  
- **Expiration policy**: Short‑lived cache balances performance with freshness of external API data.  
- **Production note**: For distributed deployments, replace `IMemoryCache` with Redis or another distributed cache.  

---

## Quick Start

### Prerequisites
- .NET 7 SDK (or project’s SDK version)  
- Git  
- Optional: Docker  
- Postman or curl for testing  

### Clone and Run
```bash
git clone https://github.com/your-org/your-repo.git
cd your-repo
dotnet restore
dotnet run

Swagger UI: `http://localhost:7008/swagger`


### Configuration and Secrets

### appsettings.json (example)
appsettings.json contains logging and JWT settings.

JWT expiry is 30 minutes (hardcoded).

Secrets should be stored via user‑secrets or environment variables.

- 'Jwt:Key' must be Base64.  
- Never commit production secrets. Use environment variables or `dotnet user-secrets`.  

### Local Secrets Setup
Bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 32)"
dotnet user-secrets set "Jwt:Issuer" "brewery-api"
dotnet user-secrets set "Jwt:Audience" "brewery-api"

Verify:B
ash
dotnet user-secrets list

---
### Token Generation
Use the below api to generate the token
POST /api/auth/token
Content-Type: application/json

{
  "username": "testuser",
  "password": "P@ssw0rd!"
}
Response
=========
{
  "token": "<JWT_TOKEN>",
  "expiresIn": 1800
}
### Database Initialization & Migrations

This project uses **Entity Framework Core** with SQLite. To ensure the database schema is created and updated reliably, follow these steps:

### 1. Install EF Core Tools
Make sure you have the EF Core CLI tools installed:
```bash
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef
```

### 2. Add EF Core Packages
Ensure the following NuGet packages are installed:
```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

### 3. Configure DbContext
In `Program.cs`, register the DbContext:
```csharp
builder.Services.AddDbContext<BreweryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=brewery.db"
  }
}
```

### 4. Create Initial Migration
Generate the first migration:
```bash
dotnet ef migrations add InitialCreate
```

This creates a `Migrations` folder with schema snapshots.

### 5. Apply Migration
Update the database:
```bash
dotnet ef database update
```

This creates the `brewery.db` file with the schema.

### 6. Apply Migrations at Startup
To ensure migrations run automatically, add this to `Program.cs` after building the app:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BreweryDbContext>();
    db.Database.Migrate(); // applies pending migrations automatically
}
```

---

### Example Requests
Using curl:

bash
curl -X GET "https://localhost:5001/api/breweries" \
  -H "Authorization: Bearer <JWT_TOKEN>"
Using Postman:

Add Authorization header → Bearer <JWT_TOKEN>.

Test endpoints like /api/breweries, /api/breweries/{id}.

## Authentication and Token Flow
- `POST /api/auth/login` → issues JWT.  
- `POST /api/auth/validate` → manual token validation.  
- Middleware configured with `AddJwtBearer` and `TokenValidationParameters`.  
- Pipeline requires:
csharp
app.UseAuthentication();
app.UseAuthorization();

### Authorization Notes
Protected endpoints: /api/breweries/*, /api/orders/*.

Unprotected endpoints: /api/health, /api/docs.

Token expiry: 30 minutes. Refresh requires re‑authentication.
---

## Logging, Caching, and Versioning
- **Logging**: Structured logging via `ILogger`.  
- **Caching**: Repository caching with `IMemoryCache`.  
- **Versioning**: `api/v1/Breweries`, `api/v2/Breweries`. Swagger exposes both versions.  

---

## Endpoints Summary
- **Authentication**  
  - `POST /api/auth/login`  
  - `POST /api/auth/validate`  
- **Breweries**  
  - `GET /api/v{version}/Breweries`  
  - `GET /api/v{version}/Breweries/autocomplete?query=...`  

---

## Known Limitations
- Distance calculation uses Haversine formula (approximate, not road distance).  
- EF Core/SQLite integration is commented out and not production‑ready.  
- JWT secret stored in config (should use secure vault in production).  
- IMemoryCache is not distributed — unsuitable for multi‑instance scaling.  
- Autocomplete results are simulated for demo purposes.  

---

## Troubleshooting
- **401 Unauthorized** → Check `app.UseAuthentication()` and `app.UseAuthorization()`.  
- **Token expired immediately** → Use `DateTime.UtcNow.AddMinutes(30)`.  
- **Unexpected config values** → Run `dotnet user-secrets list`.  
- **Swagger Authorization** → Use “Authorize” button with `Bearer <token>`.  

---

## Contributing and Deployment
- Use feature branches and pull requests.  
- Secrets via environment variables or secrets manager.  
- Optional Dockerfile and docker-compose for containerization.  

---

## Appendix: File Locations
- `Program.cs` → startup, DI, Swagger, auth.  
- `Controllers/AuthController.cs` → login & validation endpoints.  
- `Validator/TokenValidator.cs` → manual token validation.  
- `Repository/BreweryRepository.cs` → external API calls.  
- `Services/BreweryService.cs` → filtering, sorting, paging.  
- `Exceptions/GlobalExceptionMiddleware.cs` → error handling.  
- `Configuration/ConfigureSwaggerOptions.cs` → Swagger setup.  
- `Models/PagedResult.cs` → paging model.  

---