# Courier - Suggested Commands

## System: Windows 11 (Git Bash shell)
Use Unix-style paths and commands (forward slashes, `/dev/null`, etc.).

## Build
```bash
dotnet build Courier.slnx
```

## Run (Local Dev - Aspire)
```bash
cd src/Courier.AppHost && dotnet run
```
This starts Postgres, Seq, API, Worker, and Frontend together.

## Frontend Setup (first time)
```bash
cd src/Courier.Frontend && npm install
```

## Testing
```bash
# All tests
dotnet test Courier.slnx

# Unit tests only (fast, no Docker)
dotnet test tests/Courier.Tests.Unit

# Architecture tests (dependency rule enforcement)
dotnet test tests/Courier.Tests.Architecture

# Integration tests (requires Docker)
dotnet test tests/Courier.Tests.Integration

# Single test by name
dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~JobServiceTests.CreateAsync"

# Tests in a specific class
dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~JobServiceTests"
```

## Utility Commands
```bash
# Git
git status
git log --oneline -10
git diff

# File system (Git Bash on Windows)
ls -la
find . -name "*.cs" -type f
grep -r "pattern" src/
```

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for integration tests and Aspire dev)
- Node.js 20+
