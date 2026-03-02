# SAPL for .NET

[![CI](https://github.com/heutelbeck/sapl-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/heutelbeck/sapl-dotnet/actions/workflows/ci.yml)
[![NuGet Sapl.Core](https://img.shields.io/nuget/v/Sapl.Core?label=Sapl.Core)](https://www.nuget.org/packages/Sapl.Core)
[![NuGet Sapl.AspNetCore](https://img.shields.io/nuget/v/Sapl.AspNetCore?label=Sapl.AspNetCore)](https://www.nuget.org/packages/Sapl.AspNetCore)

.NET implementation of [SAPL](https://sapl.io) (Streaming Attribute-Based Access Control).

## Packages

| Package | Description |
|---------|-------------|
| **Sapl.Core** | Framework-agnostic core: PDP client, authorization models, constraint handling, enforcement engine |
| **Sapl.AspNetCore** | ASP.NET Core integration: filters, attributes, middleware, subscription builder |

## Quick Start

```bash
dotnet add package Sapl.Core
dotnet add package Sapl.AspNetCore
```

## Demo

See the [`demo/`](demo/) directory for a full ASP.NET Core example with Keycloak integration.

## License

[Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0)
