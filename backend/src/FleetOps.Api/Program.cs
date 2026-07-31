using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FleetOps.Api.Authorization;
using FleetOps.Api.BackgroundServices;
using FleetOps.Api.Hubs;
using FleetOps.Api.Middleware;
using FleetOps.Api.Services;
using FleetOps.Application;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Ais;
using FleetOps.Application.Vessels;
using FleetOps.Infrastructure;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger: captures failures that happen before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured JSON logs to stdout — the shape CloudWatch and `kubectl logs` both expect.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service", "fleetops-api")
        .WriteTo.Console(new CompactJsonFormatter()));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.Configure<SimulatorOptions>(
        builder.Configuration.GetSection(SimulatorOptions.SectionName));
    builder.Services.Configure<AisOptions>(
        builder.Configuration.GetSection(AisOptions.SectionName));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
    builder.Services.AddScoped<ITelemetryBroadcaster, SignalRTelemetryBroadcaster>();
    // A telemetry source is an enhancement, not a dependency: the API serves vessels,
    // manifests and auth perfectly well without one. The host default is StopHost, which
    // would let a failing AIS socket take the whole application down.
    builder.Services.Configure<HostOptions>(options =>
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

    // Exactly one telemetry source. Live AIS wins when it is configured; otherwise the
    // simulator runs, so a fresh clone works with no credentials and the demo never
    // depends on a third-party beta service being up.
    var aisEnabled = builder.Configuration.GetValue<bool>($"{AisOptions.SectionName}:Enabled")
                     && !string.IsNullOrWhiteSpace(builder.Configuration[$"{AisOptions.SectionName}:ApiKey"]);

    if (aisEnabled)
    {
        builder.Services.AddHostedService<AisIngestionService>();
    }
    else
    {
        builder.Services.AddHostedService<TelemetrySimulatorService>();
    }

    builder.Services.AddValidatorsFromAssemblyContaining<CreateVesselRequestValidator>();

    builder.Services
        .AddControllers(options => options.Filters.Add<ValidationFilter>())
        .AddJsonOptions(options =>
        {
            // Enums travel as readable strings; the Angular client keys off them directly.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddSignalR();

    // ---- Authentication ----------------------------------------------------
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwt.Key)
                        ? new string('0', 32) // Fails validation loudly rather than throwing at startup.
                        : jwt.Key)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            // Browsers cannot set headers on a WebSocket handshake, so SignalR
            // passes the token as a query-string parameter instead.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments(TelemetryHub.Route, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
            };
        });

    builder.Services.AddAuthorization(options => options.AddFleetPolicies());

    // ---- CORS --------------------------------------------------------------
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:4200"];

    builder.Services.AddCors(options => options.AddPolicy("fleetops-ui", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials() // required for the SignalR WebSocket handshake
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

    // ---- Rate limiting -----------------------------------------------------
    // API Gateway does this in AWS; this keeps the same protection when the API
    // is reached directly (k3d, Render, local Docker).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.PermitLimit = 10;
            limiter.QueueLimit = 0;
        });

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 300,
                    QueueLimit = 0,
                }));
    });

    // ---- Health checks -----------------------------------------------------
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<FleetOpsDbContext>("database", tags: ["ready"]);

    // ---- OpenAPI -----------------------------------------------------------
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "CloudScale FleetOps API",
            Version = "v1",
            Description = "Vessel metrics, IoT telemetry, anomaly detection and cargo manifest ingestion.",
        });

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the token returned by POST /api/auth/login.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
        };

        options.AddSecurityDefinition("Bearer", scheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });

    var app = builder.Build();

    // ---- Pipeline ----------------------------------------------------------
    // Correlation id first so every later log line carries it.
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseSerilogRequestLogging(options =>
        options.GetLevel = (httpContext, _, ex) =>
            ex is not null || httpContext.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Error
                : httpContext.Request.Path.StartsWithSegments("/health")
                    ? Serilog.Events.LogEventLevel.Verbose // health probes would drown the log
                    : Serilog.Events.LogEventLevel.Information);

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FleetOps API v1"));
    }

    app.UseCors("fleetops-ui");
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<TelemetryHub>(TelemetryHub.Route);

    // Liveness: is the process up. Readiness: can it reach its dependencies.
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    await MigrateAndSeedAsync(app);

    Log.Information("FleetOps API starting in {Environment}", app.Environment.EnvironmentName);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "FleetOps API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task MigrateAndSeedAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

    try
    {
        // Until the first migration is scaffolded (scripts/create-migration.sh), fall back to
        // EnsureCreated so a fresh clone still boots with a working schema.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            Log.Warning("No EF Core migrations found; creating the schema with EnsureCreated. "
                        + "Run scripts/create-migration.sh before deploying anywhere real.");
            await db.Database.EnsureCreatedAsync();
        }

        if (app.Configuration.GetValue("Database:SeedDemoData", false))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            var demoPassword = app.Configuration["Database:DemoPassword"];

            if (string.IsNullOrWhiteSpace(demoPassword))
            {
                Log.Warning("Database:SeedDemoData is on but Database:DemoPassword is unset; skipping seed");
            }
            else
            {
                await seeder.SeedAsync(demoPassword);
            }
        }
    }
    catch (Exception ex)
    {
        // Do not take the pod down: Kubernetes readiness will hold traffic back
        // until the database is actually reachable.
        Log.Error(ex, "Database migration/seed failed; the API will start unready");
    }
}

/// <summary>Exposed so the integration test host can reference this assembly.</summary>
public partial class Program;
