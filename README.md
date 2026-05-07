# swipe-service

Swipe ingestion and interaction tracking service for the DatingApp platform.

## What It Does

- Records swipe actions (like/pass/super-like variants)
- Emits or triggers match-check workflows
- Stores swipe behavior data for analytics and ranking feedback loops

## Why It Is Interesting

This repo shows event-like behavior handling in a microservice:
- High-frequency write paths
- Separation of interaction capture from matchmaking decision logic
- Integration boundaries with MatchmakingService

## Stack

- .NET 8
- ASP.NET Core Web API
- EF Core 8 + MySQL
- MediatR-based command patterns

## Project Layout

```text
swipe-service/
  Controllers/
  Commands/
  Services/
  Data/
  Models/
  DTOs/
  SwipeService.Tests/
```

## Build and Test

```bash
dotnet restore SwipeService.csproj
dotnet build SwipeService.csproj
dotnet test SwipeService.Tests/SwipeService.Tests.csproj
```

## Run Locally

```bash
dotnet run --project SwipeService.csproj
```

## Related Repositories

- `best-koder-org/MatchmakingService`
- `best-koder-org/UserService`
- `best-koder-org/dejting-yarp`

## Status

Active development repository tied to discovery and matchmaking flows.
