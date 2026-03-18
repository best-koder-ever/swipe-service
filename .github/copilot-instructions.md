# swipe-service
.NET 8 swipe ingestion service.
## Build & Test
```bash
dotnet restore SwipeService.csproj && dotnet build && dotnet test SwipeService.Tests/SwipeService.Tests.csproj
```
## Architecture
- Swipe recording (like/pass/superlike)
- Match detection hooks to MatchmakingService
- CQRS via MediatR, EF Core 8 with MySQL
## Rules
- All new code must have unit tests
