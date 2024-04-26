/*
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.OpenApi.Models;
using SAPL.AspNetCore.Security.Authentication;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Extensions;
using SAPL.AspNetCore.Security.Middleware.Exception;
using Web_API_Demo.Constraints;


var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDefaultSaplServices(builder.Configuration, AuthenticationType.Bearer_Token);

builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, LoggingConsumerDelegateConstraintHandler>();
builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, LoggingDelegateConstraintHandler>();
builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, ManipulatePatientIdActionExecutingContextConstraintHandlerProvider>();
builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, FilterPatientsForUserTypedFilterPredicateConstraintHandlerProvider>();
builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, ReplacePatientIdActionExecutingContextConstraintHandlerProvider>();
builder.Services.AddSingleton<IResponsibleConstraintHandlerProvider, FilterPatientsForDepartmentTypedFilterPredicateConstraintHandlerProvider>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web API for SAPL",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Please insert JWT with Bearer into field",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Name = "Bearer",
                In = ParameterLocation.Header,
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            },
            new List<string>()
        }
    });
});

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


//app.MapGet("/test", () =>
//    {
//        app.Logger.LogInformation("Endpoint");
//        return Results.Ok(PatientCollection.Patients);

//    })
//    .AddEndpointFilter(new PostEnforceEndPointFilter("Somesubject", "Patients", "someResource", "SomeEnvironment"));

app.MapControllers();

app.Run();

public partial class Program { }