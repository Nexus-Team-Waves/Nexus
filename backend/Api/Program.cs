using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mems.Api.Middleware;
using Mems.Application.Auth;
using Mems.Application.Claims;
using Mems.Application.Entitlement;
using Mems.Application.Workflow;
using Mems.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Services --------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        // Accept/emit enum members by name ("Opd", "Self") so the TS and C# enums line up.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DEMO persistence: EF Core InMemory. Production swaps to SQL Server 2019 (CLAUDE.md §3).
builder.Services.AddDbContext<MemsDbContext>(o => o.UseInMemoryDatabase("mems-demo"));

// Approval tuning (Top-Level threshold) from configuration, bound to a plain singleton so the
// Application layer needs no Options package.
builder.Services.AddSingleton(_ =>
{
    var options = new ApprovalOptions();
    builder.Configuration.GetSection(ApprovalOptions.SectionName).Bind(options);
    return options;
});

// Auth + per-request identity.
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Claim workflow + persistence.
builder.Services.AddScoped<IClaimRepository, ClaimRepository>();
builder.Services.AddScoped<ClaimWorkflowService>();
builder.Services.AddScoped<IValidator<SubmitClaimRequest>, SubmitClaimRequestValidator>();

// Entitlement.
builder.Services.AddSingleton<IEmployeeEntitlementProfileProvider, DemoEmployeeProfileProvider>();
builder.Services.AddScoped<IEntitlementLedger, ClaimEntitlementLedger>();
builder.Services.AddScoped<EntitlementService>();

// CORS. With the Vite dev proxy the browser calls are same-origin, but a direct dev origin is
// allowed too. Origins come from config; a dev default keeps the demo working out of the box.
const string FrontendCorsPolicy = "MemsFrontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[]
    {
        "http://localhost:5173", "http://localhost:5174", "http://localhost:5180",
    };
}
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Seed demo data into the fresh InMemory database.
using (var scope = app.Services.CreateScope())
{
    DemoSeed.Seed(scope.ServiceProvider.GetRequiredService<MemsDbContext>());
}

// --- Pipeline --------------------------------------------------------------
app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // No HTTPS redirect in development: the Vite proxy talks plain HTTP to :5046, and a redirect
    // to :7112 would break it. Production terminates TLS in front (CLAUDE.md §10).
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);
app.UseMiddleware<CurrentUserMiddleware>();

app.MapControllers();

app.Run();
