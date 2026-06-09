# Self-Evolving-Asset-Management

## Implementation baseline

This repository now includes an initial implementation of the technical design in `docs/Selfevolving Asset Management.md`:

- C# Blazor Web App hybrid (Server + WebAssembly)
- PostgreSQL-focused architecture configuration
- NuGet integration reference for `DevelApp.SelfEvolvingFramework`
- API endpoint: `GET /api/system/blueprint`

## Build and test

```bash
dotnet restore SelfEvolving.AssetManagement.slnx
dotnet build SelfEvolving.AssetManagement.slnx -c Release
dotnet test SelfEvolving.AssetManagement.slnx -c Release
```
