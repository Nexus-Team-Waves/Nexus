using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Mems.Api.Middleware;
using Mems.Application.Auth;
using Mems.Application.Claims;
using Mems.Application.Entitlement;
using Mems.Application.Notifications;
using Mems.Application.Receipts;
using Mems.Application.Workflow;
using Mems.Infrastructure.Pdf;
using Mems.Infrastructure.Persistence;
using Mems.Infrastructure.Push;

var builder = WebApplication.CreateBuilder(args);

// --- Services --------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        // Accept/emit enum members by name ("Opd", "Self") so the TS and C# enums line up.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DEMO persistence: a single-file SQLite database under App_Data (durable across app-pool
// recycles). Production swaps to SQL Server 2019 (CLAUDE.md §3) — change the registration below to
// UseSqlServer(connectionString) and use migrations instead of EnsureCreated().
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "mems-demo.db");
builder.Services.AddDbContext<MemsDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

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

// Receipt images + the per-line PDF for approvers.
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddSingleton<IReceiptPdfRenderer, ReceiptPdfRenderer>(); // stateless

// Grade-routed Finance stage (Approval:Finance*Email) + stage-wise notifications with Web Push.
builder.Services.AddSingleton<FinanceRoutingService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IClaimNotifier, NotificationService>();
builder.Services.AddSingleton(VapidKeyStore.LoadOrCreate(dataDir, builder.Configuration));
builder.Services.AddSingleton<IPushSender, WebPushSender>();

// Entitlement.
builder.Services.AddSingleton<IEmployeeEntitlementProfileProvider, DemoEmployeeProfileProvider>();
builder.Services.AddScoped<IEntitlementLedger, ClaimEntitlementLedger>();
builder.Services.AddScoped<EntitlementService>();

// CORS is only needed if the SPA is served from a DIFFERENT origin than the API. In this build the
// API serves the SPA itself (same origin), so this is a no-op unless you split them. Origins come
// from config; a dev default keeps `npm run dev` working out of the box.
const string FrontendCorsPolicy = "MemsFrontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:5173", "http://localhost:5174", "http://localhost:5180" };
}
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Create the database (if absent) and seed demo data. Seeding is skipped when data already exists,
// so restarts/recycles keep whatever was submitted during the demo.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MemsDbContext>();
    db.Database.EnsureCreated();
    DemoSeed.Seed(db);
}

// --- Pipeline --------------------------------------------------------------
app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the built SPA (wwwroot). ".webmanifest" needs an explicit MIME type for the PWA manifest.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseCors(FrontendCorsPolicy);
app.UseMiddleware<CurrentUserMiddleware>();

app.MapControllers();

// Any non-API, non-file route (client-side navigation) falls back to the SPA shell.
app.MapFallbackToFile("index.html");

app.Run();
