Here’s a fully restructured **README.md** that incorporates all the missing sections you called out — architecture decisions, cache strategy rationale, EF Core/SQLite explanation, JWT/Base64 rationale, and known limitations. It’s written to be clear, professional, and developer‑friendly.

---

## Security: Token Validation Fix

Previously the manual token validation used by the diagnostic endpoint `/api/auth/validate` skipped signature verification. That allowed a token with forged claims (iss/aud/exp) but an invalid or missing signature to pass manual validation and be reported as valid by the diagnostic endpoint. The main ASP.NET JWT bearer pipeline (the `[Authorize]` middleware) was not affected, but the diagnostic endpoint still represented a potential security hole.

What changed
- The manual validator now verifies the JWT signature using HMAC-SHA256 with the configured `Jwt:Key` (supports Base64 or raw string keys). If the signature does not match the computed HMAC, validation fails with a `SecurityTokenInvalidSignatureException`.
- Issuer (`iss`), Audience (`aud`) and Expiry (`exp`) checks are still performed after signature verification.

Why this is safe
- The diagnostic endpoint is intended for operational verification; it now performs equivalent signature checks as the runtime authentication pipeline, preventing forged tokens from being mistakenly accepted.
- The implementation uses only built-in crypto primitives (no new package dependencies) and is deterministic for the test environment.

Developer notes / example
```csharp
// TokenValidator now computes HMAC-SHA256 over header + '.' + payload and compares
// it to the token signature using a constant-time comparison. Example (simplified):
using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
var computed = hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload));
// Use constant-time comparison to avoid timing attacks
if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(computed, signatureBytes))
    throw new SecurityTokenInvalidSignatureException("Signature validation failed");
```

If you want the diagnostic endpoint to remain permissive for debugging, call the main `JwtBearer` pipeline instead or add a separate admin-only endpoint that returns token details instead of claiming full validation.


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
- .NET 8 SDK (or project’s SDK version)  
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

---

## Configuration and Secrets

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
### Reading credentials in AuthController

The Login endpoint prefers values from IConfiguration (for example, user-secrets) and falls back to environment variables. The controller uses both common configuration keys and a small helper that reads from process/user/machine environment scopes.

Example code used in AuthController.Login:

```csharp
<<<<<<< HEAD
// The controller prefers IConfiguration (user-secrets) and AUTH_* env vars.
// It intentionally avoids falling back to generic 'Username'/'Password'
// environment variable names because Windows defines a built-in %USERNAME%
// that would unintentionally override developer defaults.
string? expectedUsername = _config["Auth:Username"]
                ?? _config["AUTH_USERNAME"]
                ?? ReadEnv("AUTH_USERNAME")
                ?? "admin";
string? expectedPassword = _config["Auth:Password"]
                ?? _config["AUTH_PASSWORD"]
                ?? ReadEnv("AUTH_PASSWORD")
                ?? "password";
```

Note: On Windows the environment variable %USERNAME% contains the current
user account name. Using a lookup for `Username` would cause the application
to accept the OS account as the credential instead of the intended default
`admin`. For predictable behavior prefer `Auth:Username` via user-secrets or
the explicit `AUTH_USERNAME`/`AUTH_PASSWORD` environment variables.

=======
string? expectedUsername = _config["Auth:Username"]
                ?? _config["AUTH_USERNAME"]
                ?? _config["Username"]
                ?? ReadEnv("AUTH_USERNAME")
                ?? ReadEnv("Username");
string? expectedPassword = _config["Auth:Password"]
                ?? _config["AUTH_PASSWORD"]
                ?? _config["Password"]
                ?? ReadEnv("AUTH_PASSWORD")
                ?? ReadEnv("Password");
```

>>>>>>> 08529c0 (Docs: document AuthController credential resolution and examples for user-secrets/env vars)
Set these values with user-secrets (recommended for local development):

```bash
dotnet user-secrets set "Auth:Username" "admin"
dotnet user-secrets set "Auth:Password" "password"
```

Or set environment variables (user-level persistent):

PowerShell:
```powershell
setx AUTH_USERNAME "admin"
setx AUTH_PASSWORD "password"
```

Note: setx writes to the user or machine environment and requires restarting the process (or Visual Studio) to be visible to the running application. For immediate effect in the current PowerShell session, use:

```powershell
$env:AUTH_USERNAME = "admin"; $env:AUTH_PASSWORD = "password"
```

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
###Perfect — let’s add a **clear, step‑by‑step EF Core migrations section** to your project’s README so anyone on your team can reliably initialize and update the database.  

---

## 📌 Where to Add in README
Place this section **after your “Setup Instructions”** and before “Running the API.” That way, developers see it right after cloning and restoring packages, ensuring they don’t miss DB initialization.

---

## 📌 README Section: Database Initialization & Migrations

```markdown
## Database Initialization & Migrations


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