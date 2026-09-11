### Brewery API README

---

## Overview
**Brewery API** is a .NET Web API that provides brewery search and listing features with JWT authentication, Swagger documentation, in‑memory caching for external API responses, structured logging, API versioning, and a global exception handler. The project includes a token helper for manual validation and uses `HttpClient` to fetch data from the Open Brewery DB.

> **From the project:** `app.UseAuthentication();   // validates JWT`  
> **From the project:** `app.UseAuthorization();// Enforce [Authorize] attributes`

---

## Quick start

### Prerequisites
- **.NET 7 SDK** (or the SDK version used by the project)
- Git
- Optional: Docker (if you containerize)
- Postman or curl for testing

### Files to include in the repository
- **Solution and project files**: `*.sln`, `*.csproj`  
- **Source code**: `Program.cs`, `Controllers/`, `Models/`, `Repository/`, `Services/`, `TokenValidator.cs`, `GlobalExceptionMiddleware.cs`, `ConfigureSwaggerOptions.cs`  
- **Configuration**: `appsettings.json`, `launchSettings.json`  
- **Dev helpers**: `.gitignore`, `README.md`  
Do **not** commit `bin/`, `obj/`, `.vs/`, or any files containing secrets.

### Clone and run
```bash
git clone https://github.com/your-org/your-repo.git
cd your-repo
dotnet restore
dotnet run
```
- Swagger UI will be available at `http://localhost:7008/swagger` (per `launchSettings.json`).

---

## Configuration and secrets

### appsettings.json (example)
```json
"Jwt": {
  "Key": "GLEGP2YPK7Dpup+35RatogjSGfyg7o61puVLgv3cX/I=",
  "Issuer": "brewery-api",
  "Audience": "brewery-api"
}
```
**Important**
- The `Jwt:Key` in this project is a Base64 string. When creating or validating tokens the code uses `Convert.FromBase64String` to obtain the signing key bytes.
- **Never** commit production secrets. Use environment variables or `dotnet user-secrets` for local development.

### Environment overrides
ASP.NET Core configuration merges multiple sources. If a value appears unexpected (for example `local-issuer`), check:
- `appsettings.Development.json`
- Environment variables (`Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`)
- User secrets (`dotnet user-secrets list`)

---

## Authentication and token flow
### README update — add local secrets steps

Below is a ready‑to‑paste section you can insert into the **Configuration and secrets** area of your `README.md`. It places the new instructions in the appropriate spot and keeps the existing guidance about Base64 keys and not committing secrets.

> **From the project:** `The Jwt:Key in this project is a Base64 string. When creating or validating tokens the code uses Convert.FromBase64String to obtain the signing key bytes.`  
> **From the project:** `Never commit production secrets. Use environment variables or dotnet user-secrets for local development.`

---

#### Add secrets in local environments

Use the .NET user‑secrets tool to store JWT settings locally (do **not** commit these values). Run the following commands from the API project folder (the folder that contains the `.csproj` file):

```bash
# initialize user-secrets for the project (run once per project)
dotnet user-secrets init

# generate a 256-bit Base64 key and store it
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 32)"

# set issuer and audience
dotnet user-secrets set "Jwt:Issuer" "brewery-api"
dotnet user-secrets set "Jwt:Audience" "brewery-api"
```

**Verify stored secrets**
```bash
dotnet user-secrets list
```

**Notes**
- The `Jwt:Key` must be a Base64 string because the application uses `Convert.FromBase64String(Jwt:Key)` to obtain the signing key bytes.  
- Do **not** commit user‑secrets, `.env` files, or any files containing secrets to source control. Use environment variables or a secrets manager for production.  
- If you use Windows without `openssl`, generate a Base64 key with PowerShell:
```powershell
[Convert]::ToBase64String((New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes(32))
```

---

### How tokens are issued
- `POST /api/auth/login` accepts a `LoginModel` and returns a JWT when credentials are valid.
- Token creation uses:
  - `issuer` = `Jwt:Issuer`
  - `audience` = `Jwt:Audience`
  - signing key = `Convert.FromBase64String(Jwt:Key)`
  - expiry = 30 minutes (use `DateTime.UtcNow` recommended)

### How middleware validates tokens
- `Program.cs` configures JWT validation with `AddJwtBearer` and `TokenValidationParameters`:
  - `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` are enabled
  - `IssuerSigningKey` is created from the Base64 key
- The pipeline must include:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```
so `[Authorize]` attributes are enforced.

### Test token issuance and protected endpoints
1. Get token:
```bash
curl -X POST http://localhost:7008/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'
```
2. Call protected endpoint:
```bash
curl -X GET "http://localhost:7008/api/v1/Breweries/autocomplete?query=lag" \
  -H "Authorization: Bearer <token>"
```

### Manual validation helper
The project includes `TokenValidator` service that uses `JwtSecurityTokenHandler.ValidateToken` with the same `TokenValidationParameters` as the middleware. Use the debug endpoint:
```http
POST /api/auth/validate
Body: "<your_jwt_here>"
```
This prints validation results to the console and helps diagnose signature, issuer, audience, or expiry issues.

---

## Logging caching and versioning

### Logging
- The project uses the built‑in ASP.NET Core logging configuration in `appsettings.json`.  
- Controllers and services log important events and warnings (for example cache misses and external API failures).  
- Global exception handling is implemented in `GlobalExceptionMiddleware` which logs unhandled exceptions and returns a standardized JSON error response.

### Caching
- `BreweryRepository` caches autocomplete responses using `IMemoryCache` with a 10‑minute expiration:
  - Cache key pattern: `brewery_autocomplete_{query}`
  - This reduces calls to the external Open Brewery DB and improves response times.

### API versioning
- API versioning is enabled via `AddApiVersioning` and `AddVersionedApiExplorer`.  
- Controllers are versioned:
  - `api/v1/Breweries` (v1)
  - `api/v2/breweries` (v2)
- Swagger is configured to expose each API version using `ConfigureSwaggerOptions`.

---

## Endpoints summary

**Authentication**
- `POST /api/auth/login` — returns JWT for valid credentials
- `POST /api/auth/validate` — manual token validation (debug)

**Breweries**
- `GET /api/v{version}/Breweries` — list breweries (protected)
- `GET /api/v{version}/Breweries/autocomplete?query=...` — autocomplete suggestions (protected controller; method may be public depending on attribute)

---

## Troubleshooting and tips

- **401 Unauthorized**  
  - Ensure `app.UseAuthentication()` and `app.UseAuthorization()` are present and in that order.  
  - Confirm `Jwt:Issuer` and `Jwt:Audience` in `appsettings.json` match the token `iss` and `aud`.  
  - If the token signature fails, verify the signing key encoding. For a Base64 key use `Convert.FromBase64String` both when issuing and validating.

- **Token appears expired immediately**  
  - Issue tokens with `DateTime.UtcNow.AddMinutes(30)` to avoid timezone issues.

- **Unexpected config values**  
  - Run `dotnet user-secrets list` and inspect environment variables for `Jwt__*` overrides.

- **Swagger Authorization**  
  - Use the Swagger UI “Authorize” button and paste `Bearer <token>` to test protected endpoints.

---

## Contributing and deployment

- Use feature branches and open pull requests.  
- For production, provide secrets via environment variables or a secrets manager.  
- Optionally add a `Dockerfile` and `docker-compose.yml` for containerized deployment.

---

## Appendix Files and locations
- `Program.cs` — application startup, authentication, Swagger, DI registrations  
- `AuthController.cs` — login and token validation endpoints  
- `TokenValidator.cs` — manual token validation helper  
- `BreweryRepository.cs` — external API calls and caching logic  
- `BreweryService.cs` — business logic, filtering, sorting, paging  
- `GlobalExceptionMiddleware.cs` — centralized error handling  
- `ConfigureSwaggerOptions.cs` — Swagger per-version configuration

---
