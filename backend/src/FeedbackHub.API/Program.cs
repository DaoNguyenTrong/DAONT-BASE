using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using FeedbackHub.API.Common;
using FeedbackHub.API.Extensions;
using FeedbackHub.API.Middleware;
using FeedbackHub.API.Json;
using FeedbackHub.Application;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Infrastructure;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Seeding;
using Serilog;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console());

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApiWithAuth();
builder.Services.AddAuthorization();
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
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
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

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseCors();
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
app.UseMiddleware<TenantMiddleware>();
app.MapControllers();

app.Run();
