# e.l.f-brewery-api

This repository implements a small .NET 8 Web API ("Brewery API") used for demos and exercises. The API
exposes brewery lookup/search capabilities, returns mapped DTOs from the Open Brewery DB, and includes a
simple JWT-based authentication helper used by examples and tests.

This README explains project structure, the authentication flow, configuration and secrets, how to build and
run the project locally, test instructions, API examples, and troubleshooting notes.

Table of contents
- Project overview
- Architecture and key components
- Authentication flow
- Configuration and secrets (user-secrets and env vars)
- Build, run and test (dotnet build / dotnet test)
- API examples (login, validate token, brewery search)
- Troubleshooting and known limitations

Project overview
----------------
This solution contains an ASP.NET Core Web API that:
- Calls the external Open Brewery DB (or accepts mocked/external results)
- Maps external brewery DTOs into internal models
- Supports distance-based sorting (Haversine formula) when coordinates are present
- Offers a simple Login endpoint that issues JWTs for testing and examples
- Includes unit tests that validate mapping and sorting behavior

Architecture and key components
-------------------------------
- Controllers
  - AuthController: issues JWT tokens (Login) and provides a diagnostic Validate endpoint.
  - BreweryController (and services): provides brewery search/listing features.
- Services
  - BreweryService: orchestrates external calls, filtering and sorting.
  - DistanceSorter: implements Haversine distance calculation and ordering.
  - Sorting factory: BrewerySorterFactory now exposes a single canonical GetSorter method; Create forwards to GetSorter.
	This consolidates sorter selection semantics for Name/City/Distance and prevents ambiguous behavior.
- Mapping
  - AutoMapper profiles (AutoMapper/BreweryProfile.cs) map ExternalBrewery -> Brewery and BreweryResponse.
  - ExternalBrewery models match the fields returned by Open Brewery DB (including latitude/longitude strings).
  - Note: Upstream payloads may omit latitude/longitude or provide malformed values. The project models ExternalBrewery.latitude/longitude as nullable strings and maps them defensively to internal double? properties using a safe parser. Tests cover missing and malformed coordinate values.
- Routing and authorization
  - v1 route standardized to: /api/v{version:apiVersion}/breweries (lowercase) to match v2.
  - Both BreweriesController (v1) and BreweriesControllerV2 (v2) enforce [Authorize] for all endpoints.

Logging
 - The application uses Microsoft.Extensions.Logging with structured logging throughout.
 - Program startup and JWT events now log via ILogger and EventIds (no Console.WriteLine calls).
 - Use ILogger<T> and EventIds for structured, searchable logs. Example events: AuthStartup, TokenValidation, TokenValidationFailed, JwtKeyWarning.
- Validation
  - A TokenValidator exists for diagnostic validation; the AuthController uses JwtSecurityTokenHandler for token checks.
- Tests
  - e.l.f. Beauty.Tests contains unit tests for mapping, auth and sorting behavior.

Authentication flow
-------------------
- Login: POST /api/auth/login accepts { username, password }. For local/dev the controller prefers:
  1) IConfiguration values (e.g., user-secrets Auth:Username/Auth:Password)
  2) AUTH_USERNAME / AUTH_PASSWORD environment variables
  3) Default development credentials (admin / password) when nothing else is configured
- On successful login the controller issues a signed JWT (HMAC-SHA256) using the configured Jwt:Key.
- Token validation: the API uses JwtSecurityTokenHandler / TokenValidationParameters for validation; a diagnostic
  endpoint is available to verify token validity and signature.

Configuration and secrets
-------------------------
- Required values (appsettings or user-secrets / env vars):
  - Jwt:Key (Base64 or raw string) — signing key used for JWTs
  - Jwt:Issuer — issuer string for tokens
  - Jwt:Audience — audience string for tokens
  - Auth:Username / Auth:Password (optional) or AUTH_USERNAME / AUTH_PASSWORD env vars

Fresh clone setup (step-by-step, recommended)
---------------------------------------------
Use these commands after cloning so the API can issue and validate JWTs locally.

1. Open a terminal at repository root, then move to the web project:

```powershell
cd "e.l.f. Beauty"
```

2. Initialize user-secrets for this project (one-time per machine/project):

```powershell
dotnet user-secrets init
```

3. Set required secrets (PowerShell):

```powershell
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 32)"
dotnet user-secrets set "Jwt:Issuer" "brewery-api"
dotnet user-secrets set "Jwt:Audience" "brewery-api"
dotnet user-secrets set "Auth:Username" "admin"
dotnet user-secrets set "Auth:Password" "password"
```

4. If OpenSSL is unavailable on Windows, generate Jwt:Key in PowerShell:

```powershell
dotnet user-secrets set "Jwt:Key" "$([Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 } | ForEach-Object { [byte]$_ })))"
```

5. Start the API:

```powershell
dotnet run --project "e.l.f-brewery-api.csproj"
```

Using the included init script
--------------------------------
If you prefer a convenience script, this repository includes a PowerShell helper that initialises
dotnet user-secrets for the web project and can optionally write placeholder values.

From the repository root run (PowerShell):

```powershell
./scripts/init-dev-env.ps1         # interactive prompts
./scripts/init-dev-env.ps1 -NonInteractive  # create placeholder values non-interactively
```

The script will change directory into the "e.l.f. Beauty" project (where UserSecretsId is declared)
before running `dotnet user-secrets`. Do not commit real secrets; replace placeholders with
secure values before using the API for anything other than local testing.

Database-backed paging
-----------------------
This project now supports database-backed paging for the breweries listing. When a local database
is configured (via the project's connection string) the repository implementations will apply
filters, sorting and paging at the database level to avoid loading the entire table into memory.

What changed:
- IBreweryRepository.GetBreweriesAsync now accepts BreweryQueryOptions so EF implementations can
  apply SQL-side Skip/Take and filtering.
- EFCoreBreweryRepository and BreweryRepository (when a DbContext is available) use EF.Functions.Like
  and Skip/Take to perform efficient queries.

When to expect DB-backed behavior:
- If you run the app with a SQLite or other supported connection string the API will query the DB
  and return only the requested page of results. If no DB is present, the repository will fallback
  to upstream lookups and service-level paging remains a fallback.

No action required for local development when using the provided test database. If you run into
performance issues with very large datasets consider adding indexes on Name/City fields in your
database.

Repository wrapper behavior & tests
----------------------------------
The BreweryRepository acts as a wrapper that prefers a local DbContext-backed data path when a
database is available. When BreweryDbContext is injected the repository performs SQL-side
filtering, sorting and paging; if no DbContext is present it falls back to the upstream HTTP
client. Unit tests have been added to validate both behaviors:

- e.l.f. Beauty.Tests/Repository/EfCoreBreweryRepositoryPagingTests.cs — verifies EF paging
- e.l.f. Beauty.Tests/Repository/BreweryRepositoryWrapperTests.cs — verifies DB path and upstream fallback

These tests run as part of the regular test suite and are included in the CI workflow.

Database indexing & migrations
-----------------------------
For production deployments with large breweries tables consider:

- Adding indexes on commonly filtered/sorted columns to improve query performance. For example:

  - CREATE INDEX IX_Breweries_Name ON Breweries(Name);
  - CREATE INDEX IX_Breweries_City ON Breweries(City);

- Applying EF Core migrations to maintain schema. Use:

  ```powershell
  dotnet ef migrations add InitialCreate --project "e.l.f. Beauty" --startup-project "e.l.f. Beauty"
  dotnet ef database update --project "e.l.f. Beauty" --startup-project "e.l.f. Beauty"
  ```

All migration and index changes should be tested on a staging environment before applying to production.

URL encoding tests
------------------
To ensure upstream requests are constructed safely the repository contains unit tests that
record outgoing HttpClient requests and assert query parameters are decoded to the original
values (not just raw percent-encoded strings). These tests guard against regressions when
building request URLs and ensure values with spaces/special characters are handled correctly.

Relevant test files:
- e.l.f. Beauty.Tests/Repository/UpstreamBreweryClientUrlEncodingTests.cs



Alternative (env vars instead of user-secrets)
----------------------------------------------
PowerShell (current shell):

```powershell
$env:Jwt__Key = "<base64-key>"
$env:Jwt__Issuer = "brewery-api"
$env:Jwt__Audience = "brewery-api"
$env:AUTH_USERNAME = "admin"
$env:AUTH_PASSWORD = "password"
```

Bash (macOS/Linux):

```bash
export Jwt__Key="<base64-key>"
export Jwt__Issuer="brewery-api"
export Jwt__Audience="brewery-api"
export AUTH_USERNAME="admin"
export AUTH_PASSWORD="password"
```

Why this is required
--------------------
- Login and token validation depend on Jwt:Key/Jwt:Issuer/Jwt:Audience. Without them, JWT signing or validation will fail.
- user-secrets is the safest local default because secrets stay out of source control.
- Using the commands above ensures a fresh clone can build, run, and authenticate without extra undocumented setup.

Important: Jwt:Key environment variable details
---------------------------------------------
This project requires a signing key for JWT issuance and validation. The configuration key is Jwt:Key.

When running the application you must provide a stable, secret signing key to both the issuer (AuthController)
and any token validation logic. Recommended approaches:

- For local development use dotnet user-secrets as shown above.
- For local process-level env var (PowerShell):

```powershell
$env:Jwt__Key = "<base64-key>"
```

- For Linux/macOS (bash):

```bash
export Jwt__Key="<base64-key>"
```

Notes:
- The double-underscore mapping (Jwt__Key) is required when setting environment variables because IConfiguration
  maps __ to : (colon) on .NET configuration binding.
- The Jwt:Key should be a base64-encoded 32-byte or larger random key. Example generator (OpenSSL):

```bash
openssl rand -base64 32
```

Why this matters
-----------------
If Jwt:Key is not provided the app may start but token issuance and signature validation will not operate correctly.
Some diagnostic code in this repository intentionally skips signature validation in TokenValidator.cs for demo/test
purposes; that behavior is documented only as an inline comment in the source and is easy to miss. The section below
explains the deliberate signature-skip and how to enable full validation.

Deliberate signature-skip in TokenValidator.cs (IMPORTANT)
--------------------------------------------------------
Background
----------
The repository contains a TokenValidator helper used by the diagnostic Validate token endpoint. During early
development and for some automated tests the code contains an intentional shortcut that bypasses signature
validation. This is currently documented only as an inline code comment inside TokenValidator.cs and therefore
may not be visible to a new developer or an external reviewer.

Risks
-----
- Skipping signature validation makes the Validate endpoint and any code that depends on it insecure for real use.
- If Jwt:Key is not configured or you rely on the diagnostic endpoint without checking TokenValidator.cs, a reviewer
  might incorrectly assume the project validates signatures in production.

What to do (recommended)
-------------------------
1. Treat the inline comment as a red flag. Open TokenValidator.cs and review the implementation before using the
   diagnostic endpoint in any security-sensitive scenario.
2. To enable full signature validation ensure Jwt:Key is set (see the section above), then update TokenValidator.cs to
   validate the signature by configuring TokenValidationParameters with IssuerSigningKey set to a SymmetricSecurityKey
   constructed from the Jwt:Key value. Example (high-level):

```csharp
// Pseudocode - adapt to your TokenValidator implementation
var keyBytes = Convert.FromBase64String(configuration["Jwt:Key"]);
var signingKey = new SymmetricSecurityKey(keyBytes);
var tokenParams = new TokenValidationParameters
{
	ValidateIssuerSigningKey = true,
	IssuerSigningKey = signingKey,
	ValidateIssuer = true,
	ValidIssuer = configuration["Jwt:Issuer"],
	ValidateAudience = true,
	ValidAudience = configuration["Jwt:Audience"]
};
// then use JwtSecurityTokenHandler.ValidateToken(..., tokenParams, out _)
```

3. Add a small integration test that asserts Validate returns Unauthorized when a token is signed with a different key.

4. For production deployments ensure Jwt__Key is injected via a secret store or Key Vault; do not use user-secrets.

Documentation and reviewer guidance
----------------------------------
- Make the TokenValidator.cs inline comment visible to reviewers by adding a short note to this README (this section).
- When opening a pull request that touches auth code, include a checklist item verifying Jwt:Key and TokenValidator
  configuration are present and correct.

Production and deployment
-------------------------
- Never store real production secrets (Jwt:Key) in the repository. Use one of the following approaches to provide the secret to your deployed app:
  - Environment variable: set Jwt__Key to the base64 string value (double underscore maps to colon in IConfiguration), e.g. Jwt__Key
  - Secret store / Key Vault: inject the secret at deployment time and populate configuration from the provider
  - CI/CD secret variables: supply Jwt__Key as a protected secret in your pipeline

Example (PowerShell) to set the environment variable for a service host:

```powershell
$env:Jwt__Key = "<base64-key>"
```

Example (Linux):

```bash
export Jwt__Key="<base64-key>"
```

Generating a secure Jwt:Key
---------------------------
- Recommended: generate a 32-byte (or larger) random key and encode it as base64. Example with OpenSSL:

```bash
openssl rand -base64 32
```

- On Windows (PowerShell):

```powershell
[Convert]::ToBase64String((New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes(32))
```

When to use appsettings.Production.json
---------------------------------------
- You can add appsettings.Production.json to supply defaults for production but avoid placing secrets there. Use placeholders like "PLACEHOLDER" and rely on environment variables or a secret store for the actual Jwt:Key.

CI note
-------
- In CI pipelines you can provide Jwt__Key as an environment variable so integration tests and the test host can start with a deterministic key compatible with your signing algorithm. Do not expose the secret in logs or public traces.

Windows environment variable note
--------------------------------
Do NOT rely on generic environment names like `Username`/`Password` because Windows defines a built-in `%USERNAME%`
variable that will collide and cause unexpected behavior. Use explicit keys: `AUTH_USERNAME` and `AUTH_PASSWORD` or
prefer user-secrets (`Auth:Username` / `Auth:Password`).

Build, run and test
-------------------
From the repository root you can run the following commands:

- Restore and build:
  - dotnet restore
  - dotnet build
- Run the API locally:
  - dotnet run --project "e.l.f. Beauty/e.l.f-brewery-api.csproj"
  - Swagger UI (if running): http://localhost:5000/swagger or check the console output for the actual URL
- Run tests:
  - dotnet test

When making configuration changes (user-secrets or env vars) restart the process (Visual Studio / terminal) so the
new values are visible.

API examples
------------
1) Login (issue JWT)
  - POST /api/auth/login
  - Body (application/json):
	{
	  "username": "admin",
	  "password": "password"
	}
  - Response: { "token": "<JWT>", "expiresIn": 1800 }

2) Validate token (diagnostic)
  - POST /api/auth/validate
  - Body: token string
  - Response: OK with a short message when token is malformed/expired or Unauthorized if signature validation cannot be completed.

3) Brewery search (example)
  - GET /api/brewery?search=lager&page=1&pageSize=10
  - Distance sorting: supply UserLat and UserLng in query options (or call a POST search that accepts options)
	- 

Testing notes and suggestions
----------------------------
- Unit tests include mapping assertions that ensure ExternalBrewery.latitude/longitude are parsed to double? and used
  by DistanceSorter. If you add new fields to the external API model, update AutoMapper/BreweryProfile accordingly.
- If tests fail related to environment variables, ensure user-secrets or process env vars are configured for the test run.
  your Jwt:Key and other secrets are set via user-secrets or environment variables before running tests.

Service behavior: filtering and autocomplete
-----------------------------------------
- BreweryService.SearchBreweriesAsync delegates filtering to the repository implementation and does not apply additional in-memory filtering. This avoids duplicate filtering when the upstream API already returns filtered results. Repository implementations should return results already filtered for the provided query.

- BreweryService.AutocompleteAsync now uses a per-query cache key (autocomplete:{query}) and calls repository.SearchBreweriesAsync(query). Earlier versions used a hardcoded "breweries" cache key, which caused autocomplete results to collide with the main breweries list cache. Tests cover the corrected behavior.

Troubleshooting and known limitations
------------------------------------
- The solution is intentionally small and uses in-memory data for some tests. For production use, add resilient HTTP
  clients, caching (Redis), and secure secret storage (Key Vault, AWS Secrets Manager, etc.).
- The diagnostic token endpoint is helpful for ops but should be secured or removed in production.
- The Open Brewery DB returns latitude/longitude as strings; the mapper safely parses them using invariant culture and
  ignores invalid values.

Contributing and pushing changes
--------------------------------
- Typical workflow:
  - Create a feature branch
  - dotnet build && dotnet test
  - Commit and push your changes
  - Open a pull request to the main branch

If you want me to rewrite or expand any section (run examples, sample curl commands, or change default ports), tell me
which parts to emphasize and I'll update the README and push the changes.

CLI examples, curl requests and sample responses
-----------------------------------------------
Below are concrete curl commands you can use against a running local instance (adjust host/port if different).

1) Login (obtain JWT)

```bash
curl -s -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'
```

Sample successful response:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 1800
}
```

2) Validate token (diagnostic)

```bash
curl -s -X POST "http://localhost:5000/api/auth/validate" \

Integration tests and developer test endpoint
--------------------------------------------
This repository includes an end-to-end integration test that verifies the full authentication flow (issue JWT via the real /api/auth/login and call a protected endpoint that enforces [Authorize]). The test uses Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) and runs as part of the standard test suite.

- Test file: e.l.f. Beauty.Tests/Integration/AuthenticationIntegrationTests.cs
- What it does: POSTs to /api/auth/login with the development credentials (admin/password), extracts the returned token, and calls the protected endpoint below with an Authorization: Bearer <token> header. The test asserts the protected endpoint returns HTTP 200.

Developer-provided protected endpoint
------------------------------------
For local verification the project exposes a small test controller:

- GET /api/test/protected — an endpoint decorated with [Authorize] that returns a small JSON payload when the request contains a valid JWT.

This endpoint exists purely for integration test coverage and developer convenience. Remove or secure it in production deployments if you do not want a dedicated test endpoint.

Program entry point and test host notes
--------------------------------------
Because the project uses top-level statements in Program.cs, a public partial class Program is added to the file so WebApplicationFactory<Program> can locate and start the application assembly for integration tests. The class is harmless at runtime and required for a robust test host.

Integration test instructions
-----------------------------
1. Ensure Jwt:Key, Jwt:Issuer, and Jwt:Audience are configured (user-secrets or environment variables) for the test run. For local development the defaults (admin/password) are used for credentials if no Auth config is set.
2. From repository root run:

   dotnet test --configuration Release

   This will run both unit and the integration tests. The integration test spins up an in-memory TestServer and exercises the actual controllers and middleware.

Jwt key normalization
---------------------
The codebase now includes a JwtKeyHelper that normalizes the configured Jwt:Key into deterministic signing bytes used by both token issuance and JwtBearer validation. The helper accepts Base64 or raw strings and expands short inputs using SHA-256 so local dev keys and Base64 secrets behave consistently. Ensure the same Jwt:Key value is available to your running process and tests.

Security reminder
-----------------
The diagnostic token validate endpoint and the developer protected test endpoint are intended for local development and testing. For production, restrict access or remove these endpoints and store secrets in a secure location (Key Vault, secret manager, etc.).
  -H "Content-Type: application/json" \
  -d '"<JWT_TOKEN_HERE>"'
```

Example responses:

- Valid token or diagnostic validation failures (malformed/expired):
  - HTTP 200 OK
  - Body: "Check console logs for validation result"
- Signature/key problem (misconfiguration):
  - HTTP 401 Unauthorized
  - Body: "Signature validation failed"

3) Brewery search (example)

Simple query by name:

```bash
curl -s "http://localhost:5000/api/brewery?search=ale&page=1&pageSize=10"
```

Distance-sorted search (POST or query options depending on controller):

```bash
curl -s "http://localhost:5000/api/brewery?sortBy=distance&userLat=34.05&userLng=-118.24"
```

Sample BreweryResponse (truncated)

```json
[
  {
	"id": "la",
	"name": "LA Brewery",
	"city": "Los Angeles",
	"state": "California",
	"country": "USA",
	"latitude": 34.0522,
	"longitude": -118.2437
  }
]
```

Deployment: Docker and Azure
---------------------------
Docker (simple)

1) Create a Dockerfile at the API project root (example):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["e.l.f. Beauty/e.l.f-brewery-api.csproj", "e.l.f. Beauty/"]
RUN dotnet restore "e.l.f. Beauty/e.l.f-brewery-api.csproj"
COPY . .
WORKDIR "/src/e.l.f. Beauty"
RUN dotnet publish "e.l.f-brewery-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "e.l.f. Beauty.dll"]
```

2) Build and run locally via Docker:

```bash
docker build -t elf-brewery-api .
docker run -p 5000:80 -e "Jwt__Key=<base64key>" -e "Auth__Username=admin" -e "Auth__Password=password" elf-brewery-api
```

Azure App Service (quick steps)

1) Ensure you have the Azure CLI installed and are logged in (az login).
2) Create a resource group and app service plan:

```bash
az group create -n elf-brewery-rg -l eastus
az appservice plan create -n elf-brewery-plan -g elf-brewery-rg --sku B1
```

3) Create a web app and deploy with `az webapp` (Linux container or run via zip deploy for a self-contained app):

```bash
az webapp create -n elf-brewery-app -g elf-brewery-rg -p elf-brewery-plan --runtime "DOTNET|8.0"
az webapp config appsettings set -n elf-brewery-app -g elf-brewery-rg --settings "Jwt:Key=<base64key>" "Jwt:Issuer=brewery-api" "Jwt:Audience=brewery-api" "Auth:Username=admin" "Auth:Password=password"
az webapp deploy -n elf-brewery-app -g elf-brewery-rg --src-path ./e.l.f. Beauty/bin/Release/net8.0/publish
```

Notes
- Replace <base64key> with a secure Base64-encoded 256-bit key. For production, use a secret manager instead of app settings.
- Adjust ports and hostnames as appropriate for your environment.

Creating Repository for e.l.f-brewery-api
