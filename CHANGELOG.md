# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
- Refactor: Consolidated BrewerySorterFactory to a single GetSorter method and forwarded Create to it to remove
  duplicated semantics and make sorter selection consistent across the codebase. (tests updated)
- Fix: Removed duplicate DbContext-backed GET /breweries endpoint to ensure all listing requests flow
  through the IBreweryService pipeline (caching, filtering, sorting).
- Upgrade: EF Core and Microsoft.EntityFrameworkCore.Sqlite packages upgraded to 8.0.0 to match .NET 8 runtime
  and resolve provider/runtime mismatches seen in integration tests.

## [2026-09-19]
- Initial repository baseline and previous changes (see commit history)
