var builder = WebApplication.CreateBuilder(args);

// --- Services --------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The PWA is served from a different origin during development (Vite on :5173),
// so the browser needs an explicit CORS grant. Origins come from configuration
// rather than being hardcoded, because production will be a different host.
const string FrontendCorsPolicy = "MemsFrontend";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

// NOTE: authentication is not wired up yet — the auth method (AD / Entra ID / local)
// is still an open question in /docs/architecture.md. Every endpoint beyond /api/health
// must be authorized before any real claim data exists (CLAUDE.md §10).
app.UseAuthorization();

app.MapControllers();

app.Run();
