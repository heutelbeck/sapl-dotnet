using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapl.Core.Attributes;

namespace Sapl.EntityFrameworkCore.Tests;

/// <summary>The enforcement path under test: the controller filter or the service DispatchProxy.</summary>
public enum EnforcementPath
{
    Controller,
    Service,
}

/// <summary>A persisted row the protected methods write, used to observe commit versus rollback.</summary>
public sealed class Widget
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

public sealed record WidgetDto(string Name);

/// <summary>A service whose methods are enforced at the domain layer via the SAPL DispatchProxy.</summary>
public interface IWidgetService
{
    [PreEnforce]
    Task<WidgetDto> CreatePermitAsync();

    [PreEnforce]
    Task<WidgetDto> CreatePreAsync();

    [PostEnforce]
    Task<WidgetDto> CreatePostAsync();
}

public sealed class WidgetService(WidgetDbContext dbContext) : IWidgetService
{
    public Task<WidgetDto> CreatePermitAsync() => InsertAsync();

    public Task<WidgetDto> CreatePreAsync() => InsertAsync();

    public Task<WidgetDto> CreatePostAsync() => InsertAsync();

    private async Task<WidgetDto> InsertAsync()
    {
        dbContext.Widgets.Add(new Widget { Name = "service" });
        await dbContext.SaveChangesAsync();
        return new WidgetDto("service");
    }
}

/// <summary>Controller whose actions carry the enforcement attributes (the filter path).</summary>
[ApiController]
[Route("controller")]
public sealed class WidgetController(WidgetDbContext dbContext) : ControllerBase
{
    [HttpPost("permit")]
    [PreEnforce]
    public Task<WidgetDto> CreatePermit() => Insert();

    [HttpPost("pre")]
    [PreEnforce]
    public Task<WidgetDto> CreatePre() => Insert();

    [HttpPost("post")]
    [PostEnforce]
    public Task<WidgetDto> CreatePost() => Insert();

    private async Task<WidgetDto> Insert()
    {
        dbContext.Widgets.Add(new Widget { Name = "controller" });
        await dbContext.SaveChangesAsync();
        return new WidgetDto("controller");
    }
}

/// <summary>Controller that delegates to the enforced service (the DispatchProxy path).</summary>
[ApiController]
[Route("service")]
public sealed class ServiceController(IWidgetService service) : ControllerBase
{
    [HttpPost("permit")]
    public Task<WidgetDto> CreatePermit() => service.CreatePermitAsync();

    [HttpPost("pre")]
    public Task<WidgetDto> CreatePre() => service.CreatePreAsync();

    [HttpPost("post")]
    public Task<WidgetDto> CreatePost() => service.CreatePostAsync();
}
