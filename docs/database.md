# Database: EF Core + SQLite

This project supports an optional SQLite-backed local database using EF Core 8. When configured, the application will use the local DB for brewery reads and the repository layer will apply SQL-side filtering, sorting and paging. If the local DB is empty the EF Core repository will fetch from the upstream Open Brewery DB API on first read and seed the local database (DB-first behavior).

## Connection string
Set the connection string in `appsettings.json` or environment variables under `ConnectionStrings:DefaultConnection`.

Example for a file-based SQLite DB (recommended for local development):

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=brewery.db"
}
```

## Migrations
Migrations are included under the `Migrations/` folder. To create a new migration (when the model changes):

```bash
cd "e.l.f. Beauty"
dotnet ef migrations add YourMigrationName
```

To apply migrations locally (normally the app will apply them during startup when a relational provider is configured):

```bash
cd "e.l.f. Beauty"
dotnet ef database update
```

## Startup behavior
- If `DefaultConnection` is configured the app will register `BreweryDbContext` and apply pending migrations at startup.
- If `DefaultConnection` is empty or missing the app will not register `BreweryDbContext` and will instead route repository reads to the upstream API.

## DB-first seeding
When the EF-backed repository sees the `Breweries` table is empty it will attempt to read from the upstream API for the requested query and seed the local table with returned entries. This provides an offline cache and allows development without first running a seeder script.

## Developer tips
- Use the `scripts/init-dev-env.ps1` helper to initialize user-secrets for JWT keys.
- Use SQLite browser tools to inspect `brewery.db` file after running the app.
- For CI consider using an in-memory provider for fast isolated tests.
