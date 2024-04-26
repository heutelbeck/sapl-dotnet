# .NET Policy Decision Point Client

The .NET Client implements the PDP API in the form of a client library for a dedicated SAPL Server. It can be used to in Policy Enforcement Points (PEPs) on .NET and framework integrations like
      SAPL.ASPNetCore.Security.

```xml
   <ItemGroup>
    <PackageReference Include="System.Reactive" Version="6.0.0" />
    <PackageReference Include="System.Reactive.Linq" Version="6.0.0" />
  </ItemGroup>
```

# API

The key class is the `PolicyDecisionPoint` exposing methods matching the PDP server like 'Decide'