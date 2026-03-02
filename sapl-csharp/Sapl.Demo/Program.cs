using Sapl.AspNetCore.Extensions;
using Sapl.Demo.Handlers;
using Sapl.Demo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSapl(options =>
{
    options.BaseUrl = builder.Configuration.GetValue("SAPL_PDP_URL", "http://localhost:8443")!;
    options.AllowInsecureConnections = true;
});

builder.Services.AddSingleton<AuditTrailHandler>();
builder.Services.AddSingleton<Sapl.Core.Constraints.Api.IConsumerConstraintHandlerProvider>(
    sp => sp.GetRequiredService<AuditTrailHandler>());
builder.Services.AddSaplConstraintHandler<LogAccessHandler>();
builder.Services.AddSaplConstraintHandler<RedactFieldsHandler>();
builder.Services.AddSaplConstraintHandler<ClassificationFilterHandler>();
builder.Services.AddSaplConstraintHandler<InjectTimestampHandler>();
builder.Services.AddSaplConstraintHandler<CapTransferHandler>();
builder.Services.AddSaplConstraintHandler<NotifyOnErrorHandler>();
builder.Services.AddSaplConstraintHandler<EnrichErrorHandler>();

builder.Services.AddScoped<PatientService>();

var app = builder.Build();

app.UseSaplAccessDenied();
app.MapControllers();

var port = builder.Configuration.GetValue("PORT", 3000);
app.Run($"http://0.0.0.0:{port}");
