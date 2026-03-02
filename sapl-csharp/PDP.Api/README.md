# .NET Policy Decision Point API

The .NET API is the base for a SAPL Policy Decision Point

```xml
   <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="System.Reactive" Version="6.0.0" />
  </ItemGroup>
```

# API

The key interface is the `PolicyDecisionPoint` exposing methods matching the PDP server HTTP SSE API: