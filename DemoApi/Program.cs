using SAPL.AspNetCore.Security.Authentication;
using SAPL.AspNetCore.Security.Authentication.Metadata;
using SAPL.AspNetCore.Security.Extensions;
using SAPL.AspNetCore.Security.Middleware.Exception;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDefaultSaplServices(builder.Configuration, AuthenticationType.Bearer_Token);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<AccessDeniedExceptionMiddleware>();

app.MapControllers();

app.Run();
