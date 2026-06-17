# Self-Evolving-Asset-Management

## Implementation baseline

This repository now includes an initial implementation of the technical design in `docs/Selfevolving Asset Management.md`:

- C# Blazor Web App hybrid (Server + WebAssembly)
- PostgreSQL-focused architecture configuration
- NuGet integration reference for `DevelApp.SelfEvolvingFramework` `1.3.0`
- Development profile enables local multi-agent orchestration for showcase scenarios
- API endpoint: `GET /api/system/blueprint`

## Build and test

```bash
dotnet nuget add source "https://nuget.pkg.github.com/DevelApp-ai/index.json" --name github --username "<github-username>" --password "<github-pat-with-read:packages>" --store-password-in-clear-text
dotnet restore SelfEvolving.AssetManagement.slnx
dotnet build SelfEvolving.AssetManagement.slnx -c Release
dotnet test SelfEvolving.AssetManagement.slnx -c Release
```
