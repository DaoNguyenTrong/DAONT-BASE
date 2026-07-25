using Microsoft.OpenApi;
using StarterKit.API.OpenApi;

namespace StarterKit.API.Extensions;

internal static class OpenApiExtensions
{
    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "StarterKit API";
                document.Info.Version = "v1";
                document.Info.Description = """
                    REST API for the StarterKit template.

                    **Authentication:** All endpoints except `/auth/login` and `/auth/refresh` require a JWT Bearer token in the `Authorization` header, or the `access_token` cookie.

                    **Required header:** Every request must include the `X-TimeZone` header with an IANA timezone identifier (e.g. `Asia/Ho_Chi_Minh`).

                    **Roles:**
                    - `User` — access own resources and profile
                    - `Admin` — full management of accounts and system configuration
                    """;

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Bearer token. Enter the token only — no 'Bearer ' prefix required. Obtain a token from POST /api/auth/login.",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
                });

                return Task.CompletedTask;
            });

            options.AddOperationTransformer<OperationIdTransformer>();
            options.AddOperationTransformer<JsonOnlyRequestBodyTransformer>();
            options.AddOperationTransformer<QueryParameterCasingTransformer>();
            options.AddOperationTransformer<TimeZoneHeaderOperationTransformer>();
        });

        return services;
    }
}
