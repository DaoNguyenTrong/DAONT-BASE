using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StarterKit.API.Authorization;
using StarterKit.API.Common;
using StarterKit.API.Extensions;
using StarterKit.API.Middleware;
using StarterKit.API.Json;
using StarterKit.Application;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Infrastructure;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Seeding;
using Prometheus;
using Serilog;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddLocalization();
builder.Services.AddControllers()
    .AddNewtonsoftJson()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(Messages));
    });
builder.Services.ConfigureOptions<ConfigureNewtonsoftJsonOptions>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        IStringLocalizer<Messages> localizer =
            context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        Dictionary<string, string[]> errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        CodedValidationProblemDetails problemDetails =
            ApiProblemDetailsFactory.CreateValidation(localizer, errors);

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] supportedCultures = [new("vi"), new("en")];

    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});
CorsSettings corsSettings = builder.Configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>()
    ?? throw new InvalidOperationException("CorsSettings configuration is missing.");
if (corsSettings.AllowedOrigins.Length == 0)
{
    throw new InvalidOperationException("CorsSettings:AllowedOrigins must contain at least one origin.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsSettings.AllowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
ForwardedHeadersSettings forwardedHeadersSettings = builder.Configuration
    .GetSection(nameof(ForwardedHeadersSettings)).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (string proxy in forwardedHeadersSettings.KnownProxies)
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    foreach (string network in forwardedHeadersSettings.KnownNetworks)
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApiWithAuth();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationResultHandler>();
RateLimiterSettings rateLimiterSettings = builder.Configuration
    .GetSection(nameof(RateLimiterSettings)).Get<RateLimiterSettings>() ?? new RateLimiterSettings();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        IStringLocalizer<Messages> localizer =
            context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
            localizer,
            StatusCodes.Status429TooManyRequests,
            "Too Many Requests",
            ApplicationMessages.TooManyRequests);

        await ApiProblemDetailsFactory.WriteAsync(context.HttpContext, problemDetails, cancellationToken);
    };
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiterSettings.AuthPermitLimit,
                Window = TimeSpan.FromMinutes(rateLimiterSettings.AuthWindowMinutes),
                QueueLimit = 0
            }));
});
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

using (IServiceScope seedScope = app.Services.CreateScope())
{
    AppDbContext seedDbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SystemSettingSeeder.SeedAsync(seedDbContext);
}

app.UseBackgroundJobs();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseRequestLocalization();
app.UseCors();
// Registered before ExceptionHandlingMiddleware so it observes the final status code
// after exception-to-status-code translation, not the pre-exception one.
app.UseHttpMetrics();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<UserTimeZoneMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

StorageSettings storageSettings = app.Services.GetRequiredService<IOptions<StorageSettings>>().Value;
string storagePath = Path.IsPathRooted(storageSettings.BasePath)
    ? storageSettings.BasePath
    : Path.Combine(app.Environment.ContentRootPath, storageSettings.BasePath);

Directory.CreateDirectory(storagePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePath),
    RequestPath = storageSettings.PublicUrlBase
});

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantAccessMiddleware>();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Access is restricted at the network layer (internal LAN only, not internet-exposed) —
    // the default LocalRequestsOnlyAuthorizationFilter would incorrectly block real LAN
    // clients once UseForwardedHeaders() rewrites RemoteIpAddress to the real client IP.
    Authorization = []
});

app.MapControllers();

// Not under /api, so it already skips UserTimeZoneMiddleware/rate limiting/auth like /hangfire —
// restrict at the network layer (internal only) the same way, not exposed to the internet.
app.MapMetrics();

app.Run();
