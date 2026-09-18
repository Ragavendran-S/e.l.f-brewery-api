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
- Mapping
  - AutoMapper profiles (AutoMapper/BreweryProfile.cs) map ExternalBrewery -> Brewery and BreweryResponse.
  - ExternalBrewery models match the fields returned by Open Brewery DB (including latitude/longitude strings).
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

Local development with user-secrets (recommended):
1. dotnet user-secrets init
2. dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 32)"  # or set a base64 string on Windows accordingly
3. dotnet user-secrets set "Jwt:Issuer" "brewery-api"
4. dotnet user-secrets set "Jwt:Audience" "brewery-api"
5. dotnet user-secrets set "Auth:Username" "admin"
6. dotnet user-secrets set "Auth:Password" "password"

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

Testing notes and suggestions
----------------------------
- Unit tests include mapping assertions that ensure ExternalBrewery.latitude/longitude are parsed to double? and used
  by DistanceSorter. If you add new fields to the external API model, update AutoMapper/BreweryProfile accordingly.
- If tests fail related to environment variables, ensure user-secrets or process env vars are configured for the test run.

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
Creating Repository for e.l.f-brewery-api
