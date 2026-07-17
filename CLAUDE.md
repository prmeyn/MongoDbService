# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

MongoDbService is a small open-source C# class library wrapping the official MongoDB.Driver to simplify MongoDB integration in .NET applications via DI. Targets `net10.0` (single TFM — not multi-targeted, so consumers on older .NET cannot reference the package).

## Commands

- Build: `dotnet build`
- Run all tests: `dotnet test` — **requires a real MongoDB instance**; tests are integration tests, not mocked. Defaults to `mongodb://localhost:27017`; override with the `MONGODB_CONNECTION_STRING` env var.
- Run a single test: `dotnet test --filter "FullyQualifiedName~Should_Create_And_Retrieve_Data"`
- Packing/publishing to NuGet is not a local dev task — see Release process below.

## Architecture

- Two projects wired by `MongoDbService.sln`: the library (`MongoDbService/`) and its integration tests (`MongoDbService.Tests/`), which references the library via `ProjectReference`.
- Core type is `MongoService` (`MongoDbService/MongoService.cs`), a sealed class registered as a DI singleton via the `AddMongoDbServices(this IServiceCollection)` extension in `ServiceCollectionExtensions.cs`.
- `MongoService`'s constructor reads the `MongoDbSettings` config section via `IConfiguration`:
  - `ConnectionString` — required; throws `ArgumentException` if missing/blank.
  - `DatabaseName` — optional; falls back to `"Untitled-MongoDbService"` with a logged warning if missing/blank.
- It exposes `MongoClient` directly and a computed `Database` property (`MongoClient.GetDatabase(DatabaseName)`). There is no repository/unit-of-work abstraction by design — consumers call `mongoService.Database.GetCollection<T>(...)` directly to get an `IMongoCollection<T>`.
- On construction, `MongoService` also inserts a `ConnectionRecord` (`DTOs/ConnectionRecord.cs`: id, machine name, timestamp) into a `ConnectionRecord` collection — this is the documented "Connection Tracking" feature (see README), letting an operator see which compute instances have connected to a shared MongoDB instance. The write runs via a private async helper that is fire-and-forget from the constructor's perspective but internally awaits the insert and catches/logs any failure as a warning — a broken/unreachable Mongo connection must never throw out of `MongoService`'s constructor or fail silently.

## Release process

Releases happen only through `.github/workflows/release.yml`, triggered by pushing a tag matching `v[0-9]+.[0-9]+.[0-9]+`. The workflow requires the tagged commit to exist on `origin/main`, runs `dotnet test` against a `mongo:latest` service container, then builds, packs, and pushes to NuGet using the tag as the package version.

## Repo/remote note

The canonical upstream is `github.com/prmeyn/MongoDbService` (README badges and links point there intentionally). A `meyntony` remote seen in some working copies is a fork, not the canonical repo — don't "fix" README links that point to `prmeyn`.
