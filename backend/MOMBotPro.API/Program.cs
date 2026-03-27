using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MOMBotPro.API.Controllers;
using MOMBotPro.API.Data;
using MOMBotPro.API.Middleware;
using MOMBotPro.API.Services;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

// ── Serilog — configure before builder so startup errors are captured ─
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.json", rollingInterval: RollingInterval.Day, outputTemplate: "{@l}")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── JSON + Controllers ────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MOMBotPro API", Version = "v1" });
});

// ── JWT Authentication ────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MOMBotProSuperSecretKey2025!@#$%";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"]   ?? "MOMBotPro",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "MOMBotProClient",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        // OAuth /auth endpoints are browser navigations — the frontend can't set an
        // Authorization header on a window.location.href, so we accept the JWT via
        // the ?token= query string exclusively for the /api/oauth/* initiation routes.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/oauth") &&
                    ctx.Request.Query.TryGetValue("token", out var qToken) &&
                    !string.IsNullOrEmpty(qToken))
                {
                    ctx.Token = qToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// ── EF Core ───────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Polly: shared retry + circuit-breaker policies ────────
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == (HttpStatusCode)429)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryAttempt, context) =>
        {
            Log.Warning("Retry {Retry} after {Delay}s — {Url}",
                retryAttempt, timespan.TotalSeconds,
                outcome.Result?.RequestMessage?.RequestUri);
        });

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

// ── Application services ──────────────────────────────────
builder.Services.AddHttpClient<ClaudeService>();
builder.Services.AddHttpClient<OpenAIService>()
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient<RecallService>();
builder.Services.AddHttpClient("github")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient("jira")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient("atlassian")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient("zoom")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient("gmail")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
builder.Services.AddHttpClient();           // Unnamed client for backward compat
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<SlackService>();
builder.Services.AddScoped<PipelineRepository>();
builder.Services.AddScoped<IJiraService, RealJiraService>();
builder.Services.AddScoped<ZoomSessionRepository>();
builder.Services.AddScoped<PipelineOrchestrator>();
builder.Services.AddSingleton<AudioChunkingService>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<AesEncryptor>();
builder.Services.AddScoped<ITokenProvider, TokenProvider>();
builder.Services.AddScoped<ZoomSyncService>();
builder.Services.AddHostedService<ScheduledZoomService>();
builder.Services.AddHostedService<ZoomSyncBackgroundService>();
builder.Services.AddHostedService<DataRetentionService>();

// ── OpenTelemetry tracing ─────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MOMBotPro.Pipeline"));

// ── Health checks ─────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// ── CORS ──────────────────────────────────────────────────
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// ── Startup: backfill Integration columns from ConfigJson ─
// Handles rows saved before explicit columns existed.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stale = await db.Integrations
            .Where(i => i.IsConnected && i.ConfigJson != null &&
                        i.Domain == null && i.AccessToken == null)
            .ToListAsync();

        if (stale.Count > 0)
        {
            var log = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
            foreach (var integ in stale)
                IntegrationController.BackfillFromConfigJson(integ, log);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Startup] Backfilled {stale.Count} integration row(s) from ConfigJson.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Integration backfill skipped: {ex.Message}");
    }
}

// ── Startup: seed accountId for existing Jira integration ─
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jiraInteg = await db.Integrations
            .FirstOrDefaultAsync(i =>
                i.Type == "Jira" &&
                i.AccountId == null &&
                i.AccessToken != null);

        if (jiraInteg != null)
        {
            jiraInteg.AccountId = "712020:f91eb13a-59e5-4720-bf8c-fac71487d40c";
            jiraInteg.Email     = "ramurunja91@gmail.com";
            await db.SaveChangesAsync();
            Console.WriteLine("[Startup] Jira accountId populated.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Jira accountId seed skipped: {ex.Message}");
    }
}

// ── Middleware pipeline ───────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

// ── Security headers ──────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ZoomWebhookVerificationMiddleware>();
var routes = app.MapControllers();
app.MapHealthChecks("/healthz");

// ── Startup: log registered OAuth routes ──────────────────
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("[Routes] OAuth controller mapped at: /api/oauth/{{provider}}/auth, /api/oauth/{{provider}}/callback");
startupLogger.LogInformation("[Routes] GitHub callback: {Url}",
    app.Configuration["GitHub:CallbackUrl"] ??
    (app.Configuration["App:BackendUrl"] ?? "http://localhost:5000") + "/api/oauth/github/callback");

app.Run();
